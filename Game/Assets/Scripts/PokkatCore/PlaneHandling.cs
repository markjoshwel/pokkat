/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: wraps ARPlaneManager events and fires when sufficient plane
 *              area is detected, and handles spawning on AR planes
 *              and runtime NavMesh baking
 */

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore
{
    /// <summary>
    ///     wraps ar foundation plane detection events into game-specific signals
    /// </summary>
    public class PlaneHandling : MonoBehaviour
    {
        [Header("Dependencies")]
        [HelpBox("Assign the ARPlaneManager component here.", HelpBoxMessageType.Info)]
        [Tooltip("Plane manager providing AR plane detection events for this behaviour.")]
        [SerializeField]
        private ARPlaneManager planeManager;

        [Header("Detection Threshold")]
        [Tooltip("Minimum total plane area (m²) before firing OnPlaneReady.")]
        [SerializeField]
        private float minimumAreaSquareMeters = 1.0f;

        [Tooltip("Fires OnPlaneReady only once when threshold is met.")]
        [SerializeField]
        private bool fireOnceOnly = true;

        /// <summary>
        ///     whether the plane detection threshold has been met
        /// </summary>
        public bool isReady { get; private set; }

        /// <summary>
        ///     fired when sufficient plane area has been detected for gameplay
        /// </summary>
        public event Action<ARPlane> OnPlaneReady;

        /// <summary>
        ///     fired whenever planes are updated (for runtime navmesh baking)
        /// </summary>
        public event Action OnPlanesUpdated;

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
        ///     function to validate required component references
        /// </summary>
        private void Setup_Dependencies()
        {
            // panic if the ar plane manager reference is not assigned in the inspector
            // (this is a dependency injection pattern, not GetComponent)
            if (!planeManager)
                Logkat.Panic("PlaneHandling requires an ARPlaneManager reference.");
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
        ///     function to reset the fired flag to allow OnPlaneReady to fire again
        /// </summary>
        public void ResetDetection()
        {
            isReady = false;
            Logkat.Out("PlaneHandling: Detection reset");
        }
    }
}