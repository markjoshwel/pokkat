/*
 * author: mark joshwel
 * date: 11/12/2024
 * description: wraps ARTrackedImageManager events into game-specific signals
 */

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore.Reference
{
    /// <summary>
    ///     abstracts ar foundation image tracking events into game-specific signals
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class ReferenceImageDetection : MonoBehaviour
    {
        private ARTrackedImageManager _trackedImageManager;

        private void Awake()
        {
            _trackedImageManager = GetComponent<ARTrackedImageManager>();
            if (!_trackedImageManager)
                throw new MissingComponentException(
                    "ReferenceImageDetection: an ARTrackedImageManager is required in the same GameObject");
        }

        private void OnEnable()
        {
            _trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void OnDisable()
        {
            _trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        /// <summary>
        ///     fired when a tracked image enters or remains in tracking state
        /// </summary>
        public event Action<ARTrackedImage> OnImageDetected;

        /// <summary>
        ///     fired when a tracked image is lost or removed
        /// </summary>
        public event Action<TrackableId> OnImageLost;

        /// <summary>
        ///     function to process ar image tracking events and fire appropriate game signals
        /// </summary>
        /// <param name="args">event payload from the ARTrackedImageManager</param>
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (var image in args.added) ProcessTrackedImage(image);
            foreach (var image in args.updated) ProcessTrackedImage(image);

            foreach (var imagePair in args.removed)
            {
                Debug.Log($"ReferenceImageDetection: image lost ({imagePair.Key})");
                OnImageLost?.Invoke(imagePair.Key);
            }
        }

        /// <summary>
        ///     function to fire OnImageDetected only when the image is actively tracking
        /// </summary>
        /// <param name="image">the tracked image to evaluate</param>
        private void ProcessTrackedImage(ARTrackedImage image)
        {
            if (image.trackingState != TrackingState.Tracking) return;

            Debug.Log($"ReferenceImageDetection: image detected '{image.referenceImage.name}' at {image.transform.position}");
            OnImageDetected?.Invoke(image);
        }
    }
}