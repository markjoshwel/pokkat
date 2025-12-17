/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: central logic coordinator managing neko spawning and game state
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace PokkatCore
{
    public class CoreGameplay : MonoBehaviour
    {
        [Header("Dependencies")]
        [HelpBox("Assign all required AR detection and handling components here.", HelpBoxMessageType.Info)]
        [Tooltip("Handles image tracking events from ARTrackedImageManager.")]
        
        [SerializeField]
        private ImageHandling imageHandling;

        [Tooltip("Handles plane detection events from ARPlaneManager, spawning objects onto detected planes, and runtime NavMesh baking.")] [SerializeField]
        private PlaneHandling planeHandling;

        [Tooltip("Manages persistent game statistics.")] [SerializeField]
        private Statskeeper statskeeper;

        [Header("Neko Configuration")]

        [Header("Spawn Settings")] [Tooltip("Maximum number of nekos that can be active at once.")] [SerializeField]
        private int maxActiveNekos = 5;
        
        private int _currentNekoCount;
        private AREntityBowl _currentlyRegisteredBowl;
        
        public bool gameReady { get; private set; }

        private void Awake()
        {
            Setup_Dependencies();
            Logkat.Out("CoreGameplay: Awake/Setup OK");
        }
        
        private void Start()
        {
            Configure_SubscribeToEvents();
            Logkat.Out("CoreGameplay: Start/Configure OK");
        }

        private void Setup_Dependencies()
        {
            if (!imageHandling)
                Logkat.Panic("CoreGameplay requires an ImageHandling reference.");
            if (!planeHandling)
                Logkat.Panic("CoreGameplay requires a PlaneHandling reference.");
            if (!statskeeper)
                Logkat.Panic("CoreGameplay requires a Statskeeper reference.");
        }

        private void Configure_SubscribeToEvents()
        {
            imageHandling.OnImageDetected += OnImageDetected;
            planeHandling.OnPlaneReady += OnPlaneReady;
            Logkat.Out("CoreGameplay: Event Subscription OK");
        }

        private void OnPlaneReady(ARPlane obj)
        {
            Logkat.Out("CoreGameplay: OnPlaneReady");
            // throw new NotImplementedException();
        }

        private void OnImageDetected(HandledTrackedImage obj)
        {
            Logkat.Out("CoreGameplay: OnImageDetected");
            // throw new NotImplementedException();
        }
    }
}