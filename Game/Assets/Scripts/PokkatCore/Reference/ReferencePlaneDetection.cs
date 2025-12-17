/*
 * author: mark joshwel
 * date: 11/12/2024
 * description: wraps ARPlaneManager events and fires when sufficient plane area is detected
 */

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore.Reference
{
    /// <summary>
    ///     abstracts ar foundation plane detection events into game-specific signals
    /// </summary>
    [RequireComponent(typeof(ARPlaneManager))]
    public class ReferencePlaneDetection : MonoBehaviour
    {
        [Header("Detection Threshold")]
        [Tooltip("Minimum total plane area (m²) before firing OnPlaneReady.")]
        [SerializeField]
        private float minimumAreaSquareMeters = 1.0f;

        [Tooltip("Fires OnPlaneReady only once when threshold is met.")] [SerializeField]
        private bool fireOnceOnly = true;

        /// <summary>
        ///     whether the plane detection threshold has been met
        /// </summary>
        public bool isReady { get; private set; }

        /// <summary>
        ///     the ARPlaneManager used by this detection wrapper
        /// </summary>
        public ARPlaneManager planeManager { get; private set; }

        private void Awake()
        {
            planeManager = GetComponent<ARPlaneManager>();
            if (!planeManager)
                throw new MissingComponentException(
                    "ReferencePlaneDetection: an ARPlaneManager is required in the same GameObject");
        }

        private void OnEnable()
        {
            planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void OnDisable()
        {
            planeManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        /// <summary>
        ///     fired when sufficient plane area has been detected for gameplay
        /// </summary>
        public event Action<ARPlane> OnPlaneReady;

        /// <summary>
        ///     fired whenever planes are updated (for continuous monitoring if needed)
        /// </summary>
        public event Action OnPlanesUpdated;

        /// <summary>
        ///     function to process plane tracking events and check area threshold
        /// </summary>
        /// <param name="args">event payload from the ARPlaneManager</param>
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            OnPlanesUpdated?.Invoke();

            if (fireOnceOnly && isReady) return;

            var totalArea = CalculateTotalPlaneArea();
            if (totalArea < minimumAreaSquareMeters) return;

            var largestPlane = FindLargestTrackingPlane();
            if (!largestPlane) return;

            isReady = true;
            Debug.Log(
                $"ReferencePlaneDetection: plane ready, total area {totalArea:F2}m², largest plane {largestPlane.trackableId}");
            OnPlaneReady?.Invoke(largestPlane);
        }

        /// <summary>
        ///     function to calculate the total area of all currently tracked planes
        /// </summary>
        /// <returns>total plane area in square meters</returns>
        private float CalculateTotalPlaneArea()
        {
            var totalArea = 0f;

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

            foreach (var plane in planeManager.trackables)
            {
                if (!plane || plane.trackingState != TrackingState.Tracking) continue;

                var area = plane.size.x * plane.size.y;
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
            Debug.Log("ReferencePlaneDetection: detection reset");
        }
    }
}