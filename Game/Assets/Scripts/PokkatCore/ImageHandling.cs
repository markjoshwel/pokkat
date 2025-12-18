/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: wraps ARTrackedImageManager events into game-specific signals
 */

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore
{
    /// <summary>
    ///     wrapper struct for tracked image data emitted by image handling events
    /// </summary>
    public struct HandledTrackedImage
    {
        /// <summary>
        ///     current tracking state of the image
        /// </summary>
        public TrackingState State;

        /// <summary>
        ///     the tracked image reference from ar foundation
        /// </summary>
        public ARTrackedImage Image;

        /// <summary>
        ///     unique identifier for this tracked image
        /// </summary>
        public TrackableId Id;
    }

    /// <summary>
    ///     wraps ar foundation image tracking events into game-specific signals
    /// </summary>
    public class ImageHandling : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Dependencies")]
        [HelpBox("Assign the ARTrackedImageManager component here.", HelpBoxMessageType.Info)]
        [Tooltip("ar tracked image events")]
        [SerializeField]
        private ARTrackedImageManager trackedImageManager;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        ///     variable initialisation function
        /// </summary>
        private void Awake()
        {
            Setup_Dependencies();
            Logkat.Out("ImageHandling: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("ImageHandling: Start/Configure OK");
        }

        /// <summary>
        ///     register ar foundation trackables changed event listener
        /// </summary>
        private void OnEnable()
        {
            // subscribe to ar foundation's trackables changed event
            // (this fires whenever images are added, updated, or removed from tracking)
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        /// <summary>
        ///     unregister ar foundation trackables changed event listener
        /// </summary>
        private void OnDisable()
        {
            // unsubscribe from the event to prevent memory leaks and null reference errors
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        #endregion

        #region Events

        /// <summary>
        ///     fired when a tracked image enters or remains in tracking state
        /// </summary>
        public event Action<HandledTrackedImage> OnImageDetected;

        /// <summary>
        ///     fired when a tracked image is lost or removed
        /// </summary>
        public event Action<HandledTrackedImage> OnImageLost;

        #endregion

        #region Setup

        /// <summary>
        ///     function to validate required component references
        /// </summary>
        private void Setup_Dependencies()
        {
            // panic if the ar tracked image manager reference is not assigned in the inspector
            // (this is a dependency injection pattern, not GetComponent)
            if (!trackedImageManager)
                Logkat.Panic("ImageHandling requires an ARTrackedImageManager reference.");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        ///     function to process ar foundation tracked image change events and fire appropriate game events
        /// </summary>
        /// <param name="args">event payload from the ar tracked image manager</param>
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            // log the counts of added, updated, and removed trackables for debugging
            Logkat.Out(
                $"ImageHandling: Trackables changed -> added:{args.added.Count} updated:{args.updated.Count} removed:{args.removed.Count}");

            // process newly detected images (first time camera sees the image)
            // args.added is a List<ARTrackedImage>
            foreach (var trackedImage in args.added) ProcessTrackedImage(trackedImage);

            // process images with updated tracking information (pose, state changes)
            // args.updated is a List<ARTrackedImage>
            foreach (var trackedImage in args.updated) ProcessTrackedImage(trackedImage);

            // process images that have been removed or lost tracking
            // args.removed is a List<KeyValuePair<TrackableId, ARTrackedImage>>
            // (note: different type than added/updated)
            foreach (var trackedImagePair in args.removed)
            {
                // log which image was lost by name and id
                Logkat.Out(
                    $"ImageHandling: Image lost '{trackedImagePair.Value.referenceImage.name}' ({trackedImagePair.Key})");

                // fire the OnImageLost event with wrapped data
                // (wrap in HandledTrackedImage struct for consistent event signature)
                OnImageLost?.Invoke(new HandledTrackedImage
                {
                    State = trackedImagePair.Value.trackingState,
                    Image = trackedImagePair.Value,
                    Id = trackedImagePair.Key
                });
            }
        }

        /// <summary>
        ///     function to filter tracked images and fire detection event for actively tracked images
        /// </summary>
        /// <param name="trackedImage">tracked image to process</param>
        private void ProcessTrackedImage(ARTrackedImage trackedImage)
        {
            // extract reference name from the image library entry
            var referenceName = trackedImage.referenceImage.name;

            // get current tracking state (Tracking, Limited, or None)
            var state = trackedImage.trackingState;

            // only fire detection event when image is actively being tracked
            // (TrackingState.Tracking means ar foundation has a good pose estimate)
            // (TrackingState.Limited or None are ignored - not reliable enough)
            if (state != TrackingState.Tracking) return;

            // log which image was detected with its id and state
            Logkat.Out(
                $"ImageHandling: Image detected '{referenceName}' ({trackedImage.trackableId}) state:{state}");

            // fire the OnImageDetected event with wrapped data
            // (wrap in HandledTrackedImage struct for consistent event signature)
            OnImageDetected?.Invoke(new HandledTrackedImage
            {
                State = state,
                Image = trackedImage,
                Id = trackedImage.trackableId
            });
        }

        #endregion
    }
}