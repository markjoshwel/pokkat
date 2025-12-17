/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: wraps ARPlaneManager events and fires when sufficient plane
 *              area is detected, and handles spawning on AR planes
 *              and runtime NavMesh baking
 */

using System;
using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore
{
    public class PlaneHandling : MonoBehaviour
    {
        /// <summary>
        ///     fired when sufficient plane area has been detected for gameplay
        /// </summary>
        public event Action<ARPlane> OnPlaneReady;

        /// <summary>
        ///     fired whenever planes are updated (for runtime navmesh baking)
        /// </summary>
        public event Action OnPlanesUpdated;

        private void Awake()
        {
            Logkat.Out("PlaneHandling: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("PlaneHandling: Start/Configure OK");
        }
    }
}