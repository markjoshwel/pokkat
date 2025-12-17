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
    public class ImageHandling : MonoBehaviour
    {
        /// <summary>
        ///     fired when a tracked image enters or remains in tracking state
        /// </summary>
        public event Action<ARTrackedImage> OnImageDetected;

        /// <summary>
        ///     fired when a tracked image is lost or removed
        /// </summary>
        public event Action<TrackableId> OnImageLost;
    }
}