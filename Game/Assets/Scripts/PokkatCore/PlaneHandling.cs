/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: wraps ARPlaneManager events and fires when sufficient plane
 *              area is detected, and handles spawning on AR planes
 *              and runtime NavMesh baking
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore
{
    /// <summary>
    ///     wrapper struct for plane interaction data emitted by OnPlaneInteraction
    /// </summary>
    public struct HandledPlaneInteraction
    {
        /// <summary>
        ///     world position where the touch ray hit the plane
        /// </summary>
        public Vector3 Position;

        /// <summary>
        ///     full pose (position + rotation) of the hit point on the plane
        /// </summary>
        public Pose Pose;

        /// <summary>
        ///     the ar plane that was touched
        /// </summary>
        public ARPlane Plane;
    }

    /// <summary>
    ///     wraps ar foundation plane detection events into game-specific signals
    /// </summary>
    public class PlaneHandling : MonoBehaviour
    {
        [Header("Dependencies")]
        [HelpBox("Assign the ARPlaneManager and ARRaycastManager components here.", HelpBoxMessageType.Info)]
        [Tooltip("Plane manager providing AR plane detection events for this behaviour.")]
        [SerializeField]
        private ARPlaneManager planeManager;

        [Tooltip("Raycast manager used to detect touch interactions on planes.")] [SerializeField]
        private ARRaycastManager raycastManager;

        [Header("Detection Threshold")]
        [Tooltip("Minimum total plane area (m²) before firing OnPlaneReady.")]
        [SerializeField]
        private float minimumAreaSquareMeters = 1.0f;

        [Tooltip("Fires OnPlaneReady only once when threshold is met.")] [SerializeField]
        private bool fireOnceOnly = true;

        /// <summary>
        ///     reusable list for ar raycast hit results (avoids gc alloc per frame)
        /// </summary>
        private readonly List<ARRaycastHit> _raycastHits = new();

        /// <summary>
        ///     whether the plane detection threshold has been met
        /// </summary>
        public bool isReady { get; private set; }

        /// <summary>
        ///     variable initialisation function
        /// </summary>
        private void Awake()
        {
            Setup_Dependencies();
            Logkat.Out("PlaneHandling: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("PlaneHandling: Start/Configure OK");
        }

        /// <summary>
        ///     per-frame touch detection and plane interaction raycasting
        /// </summary>
        private void Update()
        {
            // skip if no subscribers are listening for plane interactions
            if (OnPlaneInteraction == null) return;

            // try to get a touch position from this frame
            if (!TryGetTouchPosition(out var touchPosition)) return;

            // raycast from touch position to find plane hits
            if (!TryRaycastToPlane(touchPosition, out var interaction)) return;

            // fire the interaction event for subscribers (e.g., CoreGameplay)
            OnPlaneInteraction.Invoke(interaction);
        }

        /// <summary>
        ///     register ar foundation trackables changed event listener
        /// </summary>
        private void OnEnable()
        {
            // subscribe to ar foundation's trackables changed event
            // (this fires whenever planes are added, updated, or removed from tracking)
            planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        /// <summary>
        ///     unregister ar foundation trackables changed event listener
        /// </summary>
        private void OnDisable()
        {
            // unsubscribe from the event to prevent memory leaks and null reference errors
            planeManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        /// <summary>
        ///     fired when sufficient plane area has been detected for gameplay
        /// </summary>
        public event Action<ARPlane> OnPlaneReady;

        /// <summary>
        ///     fired whenever planes are updated (for runtime navmesh baking)
        /// </summary>
        public event Action OnPlanesUpdated;

        /// <summary>
        ///     fired when a touch/tap hits a tracked plane
        /// </summary>
        public event Action<HandledPlaneInteraction> OnPlaneInteraction;

        /// <summary>
        ///     attempts to read touch/mouse input using the new input system
        /// </summary>
        /// <param name="position">screen position of the touch or click</param>
        /// <returns>true if a valid touch/click was detected this frame</returns>
        private static bool TryGetTouchPosition(out Vector2 position)
        {
            // check for touchscreen input first (mobile AR)
            if (Touchscreen.current is { } touchscreen)
            {
                var touch = touchscreen.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    position = touch.position.ReadValue();
                    return true;
                }
            }

            // fallback to mouse input (editor testing)
            if (Mouse.current is { } mouse && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        ///     raycasts from screen position to find plane hit and builds interaction data
        /// </summary>
        /// <param name="screenPosition">touch/click position in screen coordinates</param>
        /// <param name="interaction">resulting interaction data if raycast hit a plane</param>
        /// <returns>true if a plane was hit</returns>
        private bool TryRaycastToPlane(Vector2 screenPosition, out HandledPlaneInteraction interaction)
        {
            _raycastHits.Clear();
            raycastManager.Raycast(screenPosition, _raycastHits, TrackableType.PlaneWithinPolygon);

            // no plane hit
            if (_raycastHits.Count == 0)
            {
                interaction = default;
                return false;
            }

            // use the first (closest) hit
            var hit = _raycastHits[0];
            var hitPlane = planeManager.GetPlane(hit.trackableId);

            interaction = new HandledPlaneInteraction
            {
                Position = hit.pose.position,
                Pose = hit.pose,
                Plane = hitPlane
            };

            Logkat.Out($"PlaneHandling: plane interaction at {hit.pose.position}");
            return true;
        }

        /// <summary>
        ///     function to validate required component references
        /// </summary>
        private void Setup_Dependencies()
        {
            // panic if the ar plane manager reference is not assigned in the inspector
            // (this is a dependency injection pattern, not GetComponent)
            if (!planeManager)
                Logkat.Panic("PlaneHandling requires an ARPlaneManager reference.");
            if (!raycastManager)
                Logkat.Panic("PlaneHandling requires an ARRaycastManager reference.");
        }

        /// <summary>
        ///     function to process ar foundation plane change events and fire appropriate game events
        /// </summary>
        /// <param name="args">event payload from the ar plane manager</param>
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            // Logkat.Out(
            //     $"PlaneHandling: Trackables changed -> added:{args.added.Count} updated:{args.updated.Count} removed:{args.removed.Count}");

            // fire the planes updated event for any subscribers needing continuous updates
            // (e.g., runtime navmesh baking)
            OnPlanesUpdated?.Invoke();

            // skip threshold check if we've already fired and fireOnceOnly is enabled
            if (fireOnceOnly && isReady) return;

            // calculate total area of all tracked planes
            var totalArea = CalculateTotalPlaneArea();

            // bail out if we haven't reached the minimum area threshold
            if (totalArea < minimumAreaSquareMeters) return;

            // find the largest plane to pass to subscribers
            var largestPlane = FindLargestTrackingPlane();

            // bail out if no valid plane is found (shouldn't happen if totalArea > 0)
            if (!largestPlane) return;

            // mark as ready and fire the event
            isReady = true;
            Logkat.Out(
                $"PlaneHandling: Plane ready, total area {totalArea:F2}m², largest plane {largestPlane.trackableId}");
            OnPlaneReady?.Invoke(largestPlane);
        }

        /// <summary>
        ///     function to calculate the total area of all currently tracked planes
        /// </summary>
        /// <returns>total plane area in square meters</returns>
        private float CalculateTotalPlaneArea()
        {
            var totalArea = 0f;

            // iterate through all tracked planes and sum their areas
            // (only count planes that are actively tracking)
            foreach (var plane in planeManager.trackables)
                if (plane && plane.trackingState == TrackingState.Tracking)
                    totalArea += plane.size.x * plane.size.y;

            return totalArea;
        }

        /// <summary>
        ///     function to find the largest plane currently in tracking state
        /// </summary>
        /// <returns>largest tracked plane or null if none available</returns>
        private ARPlane FindLargestTrackingPlane()
        {
            ARPlane largestPlane = null;
            var largestArea = 0f;

            // iterate through all tracked planes to find the largest one
            foreach (var plane in planeManager.trackables)
            {
                // skip invalid or non-tracking planes
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;

                var area = plane.size.x * plane.size.y;

                // skip if this plane is smaller than the current largest
                if (area <= largestArea) continue;

                largestArea = area;
                largestPlane = plane;
            }

            return largestPlane;
        }

        /// <summary>
        ///     function to find the plane whose surface is closest to a world position
        /// </summary>
        /// <param name="worldPosition">the position to measure distance from</param>
        /// <returns>closest tracked plane or null if none available</returns>
        private ARPlane FindClosestPlane(Vector3 worldPosition)
        {
            ARPlane closestPlane = null;
            var closestDistance = float.MaxValue;

            foreach (var plane in planeManager.trackables)
            {
                // skip invalid or non-tracking planes
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;

                // project point onto this plane's infinite surface and measure distance
                var projectedPoint = plane.infinitePlane.ClosestPointOnPlane(worldPosition);
                var distance = Vector3.Distance(worldPosition, projectedPoint);

                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                closestPlane = plane;
            }

            return closestPlane;
        }

        /// <summary>
        ///     function to reset the fired flag to allow OnPlaneReady to fire again
        /// </summary>
        public void ResetDetection()
        {
            isReady = false;
            Logkat.Out("PlaneHandling.DetectionReset: called");
        }

        /// <summary>
        ///     spawns an object on the closest point of a detected plane to an in-air position,
        ///     oriented so the object's +Z axis faces the given camera
        /// </summary>
        /// <param name="objectToSpawn">the object to spawn</param>
        /// <param name="inAirPosition">in-air position to project down onto the plane</param>
        /// <param name="playerCamera">the ar camera (player viewpoint) for orientation</param>
        /// <param name="targetPlane">optional specific plane to use; if null, finds closest plane</param>
        /// <returns>the spawned game object, or null if spawning failed</returns>
        public GameObject SpawnClosest(
            GameObject objectToSpawn,
            Vector3 inAirPosition,
            Camera playerCamera,
            ARPlane targetPlane = null)
        {
            // bail early if object or camera are missing
            if (!objectToSpawn || !playerCamera)
            {
                Logkat.Warn("PlaneHandling.SpawnClosest: check on objectToSpawn or playerCamera failed");
                return null;
            }

            // find the plane closest to the in-air position
            // (handles fragmented AR tracking where multiple plane splotches exist)
            targetPlane = targetPlane ? targetPlane : FindClosestPlane(inAirPosition);
            if (!targetPlane)
            {
                Logkat.Warn("PlaneHandling.SpawnClosest: no tracked plane available");
                return null;
            }

            // project the in-air position down onto the plane
            var spawnPosition = targetPlane.infinitePlane.ClosestPointOnPlane(inAirPosition);

            // calculate rotation so the spawned object faces the camera
            var toCamera = playerCamera.transform.position - spawnPosition;
            var projectedForward = Vector3.ProjectOnPlane(toCamera, targetPlane.normal).normalized;

            // fallback if camera is directly above (projected forward is zero)
            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = Vector3.ProjectOnPlane(Vector3.forward, targetPlane.normal).normalized;

            var spawnRotation = Quaternion.LookRotation(projectedForward, targetPlane.normal);

            // instantiate and return
            var spawned = Instantiate(objectToSpawn, spawnPosition, spawnRotation);
            Logkat.Out($"PlaneHandling.SpawnClosest: spawned {objectToSpawn.name} at {spawnPosition}");
            return spawned;
        }
    }
}