/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: wraps ARPlaneManager events and fires when sufficient plane
 *              area is detected, and handles spawning on AR planes
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        #region Setup

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

        #endregion

        #region Spawning

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

            // instantiate and parent to the plane so it moves with AR tracking adjustments
            var spawned = Instantiate(objectToSpawn, spawnPosition, spawnRotation);
            spawned.transform.SetParent(targetPlane.transform, true);

            Logkat.Out($"PlaneHandling.SpawnClosest: spawned {objectToSpawn.name} at {spawnPosition}");
            return spawned;
        }

        #endregion

        #region Inspector Fields

        [Header("Dependencies")]
        [HelpBox("Assign the ARPlaneManager and ARRaycastManager components here.", HelpBoxMessageType.Info)]
        [Tooltip("ar plane detection events")]
        [SerializeField]
        private ARPlaneManager planeManager;

        [Tooltip("touch interactions on planes")] [SerializeField]
        private ARRaycastManager raycastManager;

        [Header("Detection Threshold")] [Tooltip("minimum m² before firing ready event")] [SerializeField]
        private float minimumAreaSquareMeters = 1.0f;

        [Tooltip("fire ready event only once")] [SerializeField]
        private bool fireOnceOnly = true;

        #endregion

        #region Private Fields

        /// <summary>
        ///     reusable list for ar raycast hit results (avoids gc alloc per frame)
        /// </summary>
        private readonly List<ARRaycastHit> _raycastHits = new();

        /// <summary>
        ///     coroutine handle for bake cooldown
        /// </summary>
        private Coroutine _bakeCooldownRoutine;

        #endregion

        #region Public Properties

        /// <summary>
        ///     whether the plane detection threshold has been met
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public bool isReady { get; private set; }

        /// <summary>
        ///     public accessor for the ar plane manager (for plane queries)
        /// </summary>
        public ARPlaneManager arPlaneManager => planeManager;

        #endregion

        #region Unity Lifecycle

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
        ///     per-frame touch detection and plane interaction raycasting.
        ///     petting nekos has priority over plane interactions (bowl spawning)
        /// </summary>
        private void Update()
        {
            // try to get a touch position from this frame
            if (!TryGetTouchPosition(out var touchPosition)) return;

            // UI has highest priority - skip if touch is over a UI element (button, etc.)
            if (IsTouchOverUI())
            {
                Logkat.Dev("PlaneHandling: touch is over UI, skipping plane/neko interaction");
                return;
            }

            // petting nekos has priority - check first before plane interactions
            if (TouchHitsNeko(touchPosition))
            {
                Logkat.Dev("PlaneHandling: touch hit neko, skipping plane interaction");
                return;
            }

            // skip plane interaction if no subscribers are listening
            if (OnPlaneInteraction == null) return;

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

        #endregion

        #region Events

        /// <summary>
        ///     fired when sufficient plane area has been detected for gameplay
        /// </summary>
        public event Action<ARPlane> OnPlaneReady;

        /// <summary>
        ///     fired when a touch/tap hits a tracked plane
        /// </summary>
        public event Action<HandledPlaneInteraction> OnPlaneInteraction;

        #endregion

        #region Touch Input

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
        ///     checks if the current touch/click is over a UI element (button, panel, etc.)
        /// </summary>
        /// <returns>true if touch is over UI and should be blocked from world interactions</returns>
        private static bool IsTouchOverUI()
        {
            // EventSystem handles both touch and mouse input detection over UI
            if (EventSystem.current == null) return false;

            // for touch input, check the specific touch finger id
            if (Touchscreen.current is { } touchscreen)
            {
                var touch = touchscreen.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    var fingerId = touch.touchId.ReadValue();
                    return EventSystem.current.IsPointerOverGameObject(fingerId);
                }
            }

            // for mouse input (editor), use default pointer id (-1)
            return EventSystem.current.IsPointerOverGameObject();
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
        ///     checks if a screen position hits any neko; if so, triggers petting interaction.
        ///     NOTE: requires neko prefab to have a Collider component for Physics.Raycast to detect it
        /// </summary>
        /// <param name="screenPosition">touch position in screen coordinates</param>
        /// <returns>true if a neko was hit (and petted)</returns>
        private static bool TouchHitsNeko(Vector2 screenPosition)
        {
            var mainCamera = Camera.main;
            if (!mainCamera) return false;

            var ray = mainCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 100f))
            {
                Logkat.Dev("PlaneHandling: TouchHitsNeko raycast hit nothing");
                return false;
            }

            Logkat.Dev($"PlaneHandling: TouchHitsNeko raycast hit {hit.transform.name} (tag={hit.transform.tag})");

            // try to find neko component via tag or parent hierarchy
            var neko = hit.transform.CompareTag("NekoMain") || hit.transform.CompareTag("NekoFriend")
                ? hit.transform.GetComponent<AREntityNeko>() ?? hit.transform.GetComponentInParent<AREntityNeko>()
                : hit.transform.GetComponentInParent<AREntityNeko>();

            if (!neko)
            {
                Logkat.Dev("PlaneHandling: TouchHitsNeko hit object has no AREntityNeko component");
                return false;
            }

            // trigger petting interaction on the neko
            Logkat.Out($"PlaneHandling: petting neko {neko.name}");
            neko.Pet();
            return true;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        ///     function to process ar foundation plane change events and fire appropriate game events
        /// </summary>
        /// <param name="args">event payload from the ar plane manager</param>
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            Logkat.Dev(
                $"PlaneHandling: Trackables changed -> added:{args.added.Count} updated:{args.updated.Count} removed:{args.removed.Count}");

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

        #endregion

        #region Plane Queries

        /// <summary>
        ///     function to find the plane whose surface is closest to a world position
        /// </summary>
        /// <param name="worldPosition">the position to measure distance from</param>
        /// <returns>closest tracked plane or null if none available</returns>
        // ReSharper disable once MemberCanBePrivate.Global
        public ARPlane FindClosestPlane(Vector3 worldPosition)
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
        ///     minimum plane normal Y component to be considered horizontal (floor-like).
        ///     0.9 ≈ ~25° from horizontal, filters out walls and steep slopes
        /// </summary>
        private const float HorizontalPlaneThreshold = 0.9f;

        /// <summary>
        ///     finds the closest plane that is at or below the given position (for grounding).
        ///     falls back to FindClosestPlane if no plane is below
        /// </summary>
        /// <param name="worldPosition">the position to find a plane below</param>
        /// <returns>closest plane below position, or closest plane if none below, or null</returns>
        // ReSharper disable once MemberCanBePrivate.Global
        public ARPlane FindClosestPlaneBelow(Vector3 worldPosition)
        {
            ARPlane bestPlaneBelow = null;
            var smallestDropDistance = float.MaxValue;

            foreach (var plane in planeManager.trackables)
            {
                // skip invalid or non-tracking planes
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;

                // project point onto this plane's infinite surface
                var projectedPoint = plane.infinitePlane.ClosestPointOnPlane(worldPosition);

                // only consider planes at or below the position (projectedPoint.y <= worldPosition.y)
                var dropDistance = worldPosition.y - projectedPoint.y;
                if (dropDistance < 0) continue; // plane is above us, skip

                // prefer the plane with the smallest drop distance (closest below)
                if (dropDistance < smallestDropDistance)
                {
                    smallestDropDistance = dropDistance;
                    bestPlaneBelow = plane;
                }
            }

            // if no plane below, fall back to the closest plane
            return bestPlaneBelow ? bestPlaneBelow : FindClosestPlane(worldPosition);
        }

        /// <summary>
        ///     finds the closest horizontal plane (floor-like) that is at or below the given position.
        ///     filters out walls and steep slopes by checking plane normal.
        ///     falls back to FindClosestPlaneBelow if no horizontal plane found
        /// </summary>
        /// <param name="worldPosition">the position to find a horizontal plane below</param>
        /// <returns>closest horizontal plane below, or any plane below, or null</returns>
        // ReSharper disable once MemberCanBePrivate.Global
        public ARPlane FindClosestHorizontalPlaneBelow(Vector3 worldPosition)
        {
            ARPlane bestPlaneBelow = null;
            var smallestDropDistance = float.MaxValue;

            foreach (var plane in planeManager.trackables)
            {
                // skip invalid or non-tracking planes
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;

                // skip non-horizontal planes (walls, steep slopes)
                // normal.y close to 1.0 means floor-like, close to 0.0 means wall-like
                if (plane.normal.y < HorizontalPlaneThreshold) continue;

                // project point onto this plane's infinite surface
                var projectedPoint = plane.infinitePlane.ClosestPointOnPlane(worldPosition);

                // only consider planes at or below the position
                var dropDistance = worldPosition.y - projectedPoint.y;
                if (dropDistance < 0) continue; // plane is above us, skip

                // prefer the plane with the smallest drop distance (closest below)
                if (dropDistance < smallestDropDistance)
                {
                    smallestDropDistance = dropDistance;
                    bestPlaneBelow = plane;
                }
            }

            // if no horizontal plane below, fall back to any plane below
            return bestPlaneBelow ? bestPlaneBelow : FindClosestPlaneBelow(worldPosition);
        }

        /// <summary>
        ///     projects a world position onto the nearest tracked plane surface.
        ///     prefers planes below the position for proper grounding behaviour
        /// </summary>
        /// <param name="worldPosition">the position to project onto a plane</param>
        /// <param name="projectedPosition">the resulting position on the plane surface</param>
        /// <returns>true if projection succeeded, false if no plane available</returns>
        public bool TryProjectToPlane(Vector3 worldPosition, out Vector3 projectedPosition)
        {
            var closestPlane = FindClosestPlaneBelow(worldPosition);
            if (!closestPlane)
            {
                projectedPosition = worldPosition;
                return false;
            }

            projectedPosition = closestPlane.infinitePlane.ClosestPointOnPlane(worldPosition);
            Logkat.Dev(
                $"PlaneHandling: projected {worldPosition} to {projectedPosition} on plane {closestPlane.trackableId}");
            return true;
        }

        /// <summary>
        ///     projects a world position onto the nearest horizontal (floor-like) plane.
        ///     filters out walls and steep slopes for reliable grounding.
        ///     used by GroundingBehaviour for stabilisation
        /// </summary>
        /// <param name="worldPosition">the position to project onto a horizontal plane</param>
        /// <param name="projectedPosition">the resulting position on the plane surface</param>
        /// <returns>true if projection succeeded, false if no horizontal plane available</returns>
        public bool TryProjectToHorizontalPlane(Vector3 worldPosition, out Vector3 projectedPosition)
        {
            var closestPlane = FindClosestHorizontalPlaneBelow(worldPosition);
            if (!closestPlane)
            {
                projectedPosition = worldPosition;
                return false;
            }

            projectedPosition = closestPlane.infinitePlane.ClosestPointOnPlane(worldPosition);
            Logkat.Dev(
                $"PlaneHandling: projected {worldPosition} to {projectedPosition} on horizontal plane {closestPlane.trackableId}");
            return true;
        }

        /// <summary>
        ///     gets the Y height of the nearest horizontal plane at a given XZ position.
        ///     unlike TryProjectToHorizontalPlane, this returns only Y and keeps XZ unchanged.
        ///     prefers planes whose boundary contains the XZ position for more stable grounding.
        ///     used by GroundingBehaviour for stabilisation
        /// </summary>
        /// <param name="worldPosition">the position to query (uses X and Z for plane selection)</param>
        /// <param name="planeHeight">the Y height of the plane at this XZ position</param>
        /// <returns>true if a plane was found, false otherwise</returns>
        public bool TryGetPlaneHeightAt(Vector3 worldPosition, out float planeHeight)
        {
            // first pass: try to find a horizontal plane whose boundary actually contains this XZ
            ARPlane containingPlane = null;
            var smallestContainingDrop = float.MaxValue;

            foreach (var plane in planeManager.trackables)
            {
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;
                if (plane.normal.y < HorizontalPlaneThreshold) continue;

                // check if this XZ is within the plane's boundary polygon
                var planeLocalPos = plane.transform.InverseTransformPoint(worldPosition);
                var xzInPlane = new Vector2(planeLocalPos.x, planeLocalPos.z);

                // check if point is roughly within the plane's size (simplified bounding box check)
                var halfSize = plane.size / 2f;
                var isWithinBounds = Mathf.Abs(xzInPlane.x) <= halfSize.x + 0.3f &&
                                     Mathf.Abs(xzInPlane.y) <= halfSize.y + 0.3f;

                if (!isWithinBounds) continue;

                // project to get Y height
                var projected = plane.infinitePlane.ClosestPointOnPlane(worldPosition);
                var dropDistance = worldPosition.y - projected.y;

                // prefer planes at or below the position
                if (dropDistance < -0.1f) continue; // plane is significantly above, skip

                if (dropDistance < smallestContainingDrop)
                {
                    smallestContainingDrop = dropDistance;
                    containingPlane = plane;
                }
            }

            // if we found a containing plane, use it
            if (containingPlane)
            {
                var projected = containingPlane.infinitePlane.ClosestPointOnPlane(worldPosition);
                planeHeight = projected.y;
                return true;
            }

            // fallback: use any closest horizontal plane below (original behaviour)
            var closestPlane = FindClosestHorizontalPlaneBelow(worldPosition);
            if (!closestPlane)
            {
                planeHeight = worldPosition.y;
                return false;
            }

            var fallbackProjected = closestPlane.infinitePlane.ClosestPointOnPlane(worldPosition);
            planeHeight = fallbackProjected.y;
            return true;
        }

        /// <summary>
        ///     projects a world position onto the nearest tracked plane surface,
        ///     returns original position if no plane available
        /// </summary>
        /// <param name="worldPosition">the position to project onto a plane</param>
        /// <returns>projected position on plane, or original position if no plane</returns>
        public Vector3 ProjectToPlane(Vector3 worldPosition)
        {
            return TryProjectToPlane(worldPosition, out var projected) ? projected : worldPosition;
        }

        /// <summary>
        ///     function to reset the fired flag to allow OnPlaneReady to fire again
        /// </summary>
        public void ResetDetection()
        {
            isReady = false;
            Logkat.Out("PlaneHandling.DetectionReset: called");
        }

        #endregion
    }
}