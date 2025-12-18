/*
 * author: mark joshwel
 * date: 19/12/2025
 * description: debug visualisation for runtime-baked NavMesh surfaces.
 *              integrates with PlaneHandling to refresh on navmesh bake
 */

using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace PokkatCore
{
    /// <summary>
    ///     renders the current NavMesh triangulation for debug visualisation.
    ///     automatically refreshes when PlaneHandling bakes the navmesh
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(NavMeshModifier))]
    public sealed class NavMeshDebugRenderer : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Rendering")]
        [Tooltip("material for navmesh visualisation (use transparent/wireframe for best results)")]
        [SerializeField]
        private Material debugMaterial;

        [Tooltip("vertical offset to prevent z-fighting with ground plane")]
        [SerializeField]
        private float yOffset = 0.01f;

        [Tooltip("auto-refresh when PlaneHandling bakes navmesh")]
        [SerializeField]
        private bool autoRefreshOnBake = true;

        [Header("Debug")]
        [Tooltip("enable/disable rendering at runtime")]
        [SerializeField]
        private bool enableRendering = true;

        #endregion

        #region Private Fields

        private Mesh _mesh;
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private NavMeshModifier _navMeshModifier;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _navMeshModifier = GetComponent<NavMeshModifier>();

            _mesh = new Mesh { name = "NavMeshDebugMesh" };
            _filter.sharedMesh = _mesh;

            if (debugMaterial)
                _renderer.sharedMaterial = debugMaterial;

            _renderer.enabled = enableRendering;

            // configure NavMeshModifier to exclude this mesh from navmesh baking
            _navMeshModifier.ignoreFromBuild = true;

            // prevent this mesh from being included in navmesh baking:
            // 1. ensure gameObject is not static (Navigation Static would include it)
            gameObject.isStatic = false;

            // 2. set to IgnoreRaycast layer to avoid physics interactions
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            // 3. disable shadows and other rendering features for performance
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            Logkat.Out("NavMeshDebugRenderer: Awake/Setup OK");
        }

        private void Start()
        {
            // subscribe to PlaneHandling navmesh events if auto-refresh enabled
            if (autoRefreshOnBake)
            {
                var planes = CoreGameplay.instance?.planes;
                if (planes != null)
                {
                    planes.OnNavMeshReady += OnNavMeshReady;
                    planes.OnPlanesUpdated += OnPlanesUpdated;
                    Logkat.Out("NavMeshDebugRenderer: subscribed to PlaneHandling events");
                }
                else
                {
                    Logkat.Warn("NavMeshDebugRenderer: CoreGameplay.instance.planes not available");
                }
            }

            // initial refresh if navmesh already exists
            Refresh();
        }

        private void OnDestroy()
        {
            // unsubscribe from events
            var planes = CoreGameplay.instance?.planes;
            if (planes != null)
            {
                planes.OnNavMeshReady -= OnNavMeshReady;
                planes.OnPlanesUpdated -= OnPlanesUpdated;
            }

            // clean up mesh
            if (_mesh != null)
                Destroy(_mesh);
        }

        private void OnValidate()
        {
            // update renderer enabled state in editor
            if (_renderer != null)
                _renderer.enabled = enableRendering;
        }

        #endregion

        #region Event Handlers

        private void OnNavMeshReady()
        {
            Logkat.Dev("NavMeshDebugRenderer: OnNavMeshReady, scheduling refresh");
            StartCoroutine(DelayedRefresh());
        }

        private void OnPlanesUpdated()
        {
            // only refresh if navmesh is ready (planes update before first bake)
            var planes = CoreGameplay.instance?.planes;
            if (planes != null && planes.navMeshReady)
            {
                Logkat.Dev("NavMeshDebugRenderer: OnPlanesUpdated, scheduling refresh");
                StartCoroutine(DelayedRefresh());
            }
        }

        /// <summary>
        ///     waits until end of frame to refresh, ensuring navmesh bake is complete
        /// </summary>
        private IEnumerator DelayedRefresh()
        {
            // wait for end of frame to ensure navmesh bake is complete
            yield return new WaitForEndOfFrame();
            Refresh();
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     refreshes the debug mesh from current NavMesh triangulation.
        ///     call after NavMesh.BuildNavMesh() or when navmesh changes
        /// </summary>
        public void Refresh()
        {
            if (_mesh == null) return;

            var triangulation = NavMesh.CalculateTriangulation();

            _mesh.Clear();

            if (triangulation.vertices.Length == 0)
            {
                Logkat.Dev("NavMeshDebugRenderer: no navmesh triangulation available");
                return;
            }

            // apply y offset to prevent z-fighting
            var vertices = triangulation.vertices;
            if (Mathf.Abs(yOffset) > 0.0001f)
            {
                for (var i = 0; i < vertices.Length; i++)
                    vertices[i].y += yOffset;
            }

            _mesh.vertices = vertices;
            _mesh.triangles = triangulation.indices;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            Logkat.Dev($"NavMeshDebugRenderer: refreshed with {vertices.Length} vertices, {triangulation.indices.Length / 3} triangles");
        }

        /// <summary>
        ///     toggles debug rendering on/off
        /// </summary>
        public void SetEnabled(bool isEnabled)
        {
            enableRendering = isEnabled;
            if (_renderer != null)
                _renderer.enabled = isEnabled;
        }

        #endregion
    }
}

