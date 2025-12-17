/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: handles spawning on AR planes and runtime NavMesh baking
 */

using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore.Reference
{
    /// <summary>
    ///     manages spawning objects onto detected ar planes and runtime navmesh generation
    /// </summary>
    public class ReferencePlaneHandler : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("ReferencePlaneDetection component to subscribe to plane events.")]
        [SerializeField]
        private ReferencePlaneDetection referencePlaneDetection;

        [Tooltip("NavMeshSurface for runtime baking (from AI Navigation package).")] [SerializeField]
        private NavMeshSurface navMeshSurface;

        [Header("NavMesh Baking")] [Tooltip("Cooldown between NavMesh rebakes in seconds.")] [SerializeField]
        private float navMeshBakeCooldownSeconds = 2f;

        [Tooltip("Automatically bake NavMesh when planes update.")] [SerializeField]
        private bool autoBakeOnPlaneUpdate = true;

        private Coroutine _bakeCooldownRoutine;
        private bool _canBake = true;

        private ARPlaneManager _planeManager;

        /// <summary>
        ///     whether the navmesh has been baked at least once
        /// </summary>
        public bool navMeshReady { get; private set; }

        private void Awake()
        {
            if (!referencePlaneDetection)
            {
                Debug.LogError("ReferencePlaneHandler: ReferencePlaneDetection reference is missing");
                enabled = false;
                return;
            }

            _planeManager = referencePlaneDetection.planeManager;
        }

        private void OnEnable()
        {
            if (!referencePlaneDetection) return;
            referencePlaneDetection.OnPlaneReady += OnReferencePlaneReady;
            if (autoBakeOnPlaneUpdate)
                referencePlaneDetection.OnPlanesUpdated += OnReferencePlanesUpdated;
        }

        private void OnDisable()
        {
            if (referencePlaneDetection)
            {
                referencePlaneDetection.OnPlaneReady -= OnReferencePlaneReady;
                if (autoBakeOnPlaneUpdate)
                    referencePlaneDetection.OnPlanesUpdated -= OnReferencePlanesUpdated;
            }

            if (_bakeCooldownRoutine == null) return;
            StopCoroutine(_bakeCooldownRoutine);
            _bakeCooldownRoutine = null;
        }

        /// <summary>
        ///     callback for when plane detection threshold is met
        /// </summary>
        /// <param name="plane">the largest detected plane</param>
        private void OnReferencePlaneReady(ARPlane plane)
        {
            RequestNavMeshBake();
        }

        /// <summary>
        ///     callback for when planes are updated (for continuous baking)
        /// </summary>
        private void OnReferencePlanesUpdated()
        {
            if (!referencePlaneDetection.isReady) return;
            RequestNavMeshBake();
        }

        /// <summary>
        ///     function to spawn a prefab at the closest tracked plane to the given target position
        /// </summary>
        /// <param name="targetPos">world position to spawn near</param>
        /// <param name="prefab">prefab to instantiate</param>
        /// <param name="lookAtPlayer">whether to orient the spawned object toward the camera</param>
        /// <returns>the spawned GameObject or null if no suitable plane found</returns>
        public GameObject SpawnClosest(Vector3 targetPos, GameObject prefab, bool lookAtPlayer)
        {
            if (!prefab)
            {
                Debug.LogError("ReferencePlaneHandler: cannot spawn, prefab is null");
                return null;
            }

            if (!_planeManager)
            {
                Debug.LogError("ReferencePlaneHandler: cannot spawn, ARPlaneManager is not available");
                return null;
            }

            var closestPlane = FindClosestPlane(targetPos);
            if (!closestPlane)
            {
                Debug.LogWarning($"ReferencePlaneHandler: no tracked plane found near target position {targetPos}");
                return null;
            }

            var spawnPosition = SnapToPlane(targetPos, closestPlane);
            var spawnRotation = Quaternion.identity;

            if (lookAtPlayer && Camera.main)
            {
                var directionToCamera = Camera.main.transform.position - spawnPosition;
                directionToCamera.y = 0f;
                if (directionToCamera.sqrMagnitude > 0.001f)
                    spawnRotation = Quaternion.LookRotation(directionToCamera);
            }

            var instance = Instantiate(prefab, spawnPosition, spawnRotation);
            Debug.Log(
                $"ReferencePlaneHandler: spawned '{prefab.name}' at {spawnPosition} on plane {closestPlane.trackableId}");
            return instance;
        }

        /// <summary>
        ///     function to find the tracked plane closest to the given world position
        /// </summary>
        /// <param name="position">target world position</param>
        /// <returns>closest tracked plane or null if none available</returns>
        private ARPlane FindClosestPlane(Vector3 position)
        {
            ARPlane closestPlane = null;
            var closestDistance = float.MaxValue;

            foreach (var plane in _planeManager.trackables)
            {
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;

                var planeGeometry = new Plane(plane.transform.up, plane.transform.position);
                var distance = Mathf.Abs(planeGeometry.GetDistanceToPoint(position));

                if (distance >= closestDistance) continue;

                closestDistance = distance;
                closestPlane = plane;
            }

            return closestPlane;
        }

        /// <summary>
        ///     function to project a position onto the given ar plane surface
        /// </summary>
        /// <param name="position">position to snap</param>
        /// <param name="arPlane">target ar plane</param>
        /// <returns>position snapped to the plane's y height</returns>
        private static Vector3 SnapToPlane(Vector3 position, ARPlane arPlane)
        {
            var planeGeometry = new Plane(arPlane.transform.up, arPlane.transform.position);
            return planeGeometry.ClosestPointOnPlane(position);
        }

        /// <summary>
        ///     function to request a navmesh rebake if cooldown has passed
        /// </summary>
        public void RequestNavMeshBake()
        {
            if (!navMeshSurface)
            {
                Debug.LogWarning("ReferencePlaneHandler: NavMeshSurface not assigned, skipping bake");
                return;
            }

            if (!_canBake) return;

            _canBake = false;
            BakeNavMesh();
            _bakeCooldownRoutine = StartCoroutine(BakeCooldownRoutine());
        }

        /// <summary>
        ///     function to perform the navmesh bake operation
        /// </summary>
        private void BakeNavMesh()
        {
            navMeshSurface.BuildNavMesh();
            navMeshReady = true;
            Debug.Log("ReferencePlaneHandler: navmesh baked successfully");
        }

        /// <summary>
        ///     cooldown coroutine preventing excessive rebakes
        /// </summary>
        private IEnumerator BakeCooldownRoutine()
        {
            yield return new WaitForSeconds(navMeshBakeCooldownSeconds);
            _canBake = true;
            _bakeCooldownRoutine = null;
        }
    }
}