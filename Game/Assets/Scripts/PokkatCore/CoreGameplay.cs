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
    public enum CoreGameplayState
    {
        WaitingForAnything,
        HasPlaneWaitingForTracker,
        HasTrackerWaitingForPlane,
        Ok,
    }
    
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
        [Header("Spawn Settings")]
        [Tooltip("Maximum number of nekos that can be active at once.")]
        [SerializeField]
        private int maxActiveNekos = 5;
        private int _currentNekoCount;
        private AREntityBowl _currentlyRegisteredBowl;
        
        /// <summary>
        ///     whether the game is ready for gameplay (sufficient plane area detected, required images tracked, etc.)
        /// </summary>
        public CoreGameplayState gameState { get; private set; }

        private void Awake()
        {
            Setup_Dependencies();
            Logkat.Out("CoreGameplay: Awake/Setup OK");
        }
        
        private void Start()
        {
            Configure_SubscribeToEvents();
            Logkat.Out("CoreGameplay: Start/Configure OK");
            gameState = CoreGameplayState.WaitingForAnything;
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
            Logkat.Out("CoreGameplay: received plane is ready");

            switch (gameState)
            {
                case CoreGameplayState.WaitingForAnything:
                    gameState = CoreGameplayState.HasPlaneWaitingForTracker;
                    break;
                case CoreGameplayState.HasTrackerWaitingForPlane:
                    gameState = CoreGameplayState.Ok;
                    break;
                case CoreGameplayState.HasPlaneWaitingForTracker:
                case CoreGameplayState.Ok:
                    // no state change
                    break;
                default:
                    Logkat.Panic("unreachable");
                    break;
            }
            
            Logkat.Warn("CoreGameplay.OnPlaneReady: not implemented beyond state change");
        }

        private void OnImageDetected(HandledTrackedImage obj)
        {
            switch (gameState)
            {
                case CoreGameplayState.WaitingForAnything:
                    gameState = CoreGameplayState.HasTrackerWaitingForPlane;
                    break;
                case CoreGameplayState.HasPlaneWaitingForTracker:
                    gameState = CoreGameplayState.Ok;
                    break;
                case CoreGameplayState.HasTrackerWaitingForPlane:
                case CoreGameplayState.Ok:
                    // no state change
                    break;
                default:
                    Logkat.Panic("unreachable");
                    break;
            }
            
            Logkat.Warn("CoreGameplay.OnImageDetected: not implemented beyond state change");
        }
    }
}