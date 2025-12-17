/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: central logic coordinator managing neko spawning and game state
 */

using System.Collections.Generic;
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
        Ok
    }

    public class CoreGameplay : MonoBehaviour
    {
        [Header("Dependencies")]
        [HelpBox("Assign all required AR detection and handling components here.", HelpBoxMessageType.Info)]
        [Tooltip("Handles image tracking events from ARTrackedImageManager.")]
        [SerializeField]
        private ImageHandling imageHandling;

        [Tooltip(
            "Handles plane detection events from ARPlaneManager, spawning objects onto detected planes, and runtime NavMesh baking.")]
        [SerializeField]
        private PlaneHandling planeHandling;

        [Tooltip("Manages persistent game statistics.")] [SerializeField]
        private Statskeeper statskeeper;

        [Tooltip("AR camera used for SpawnClosest orientation calculations.")] [SerializeField]
        private Camera arCamera;

        [Header("Temporary Testing - Remove After Validation")]
        [HelpBox("These fields are for testing plane interaction and image tracking spawning. Remove after validation.",
            HelpBoxMessageType.Warning)]
        [Tooltip("TEMP: prefab to spawn when testing plane touch or image tracking.")]
        [SerializeField]
        private GameObject tempTestPrefab;

        [Tooltip("TEMP: whether to destroy spawned objects when their tracked image is lost.")] [SerializeField]
        private bool tempRemoveOnUntrack = true;

        [Header("Neko Configuration")]
        [Header("Spawn Settings")]
        [Tooltip("Maximum number of nekos that can be active at once.")]
        [SerializeField]
        private int maxActiveNekos = 5;

        /// <summary>
        ///     TEMP: tracks spawned objects by their source trackable id for cleanup on untrack
        /// </summary>
        private readonly Dictionary<TrackableId, GameObject> _tempSpawnedByTrackable = new();

        private AREntityBowl _currentlyRegisteredBowl;
        private int _currentNekoCount;

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
            if (!arCamera)
                Logkat.Panic("CoreGameplay requires an AR Camera reference.");
        }

        private void Configure_SubscribeToEvents()
        {
            imageHandling.OnImageDetected += OnImageDetected;
            imageHandling.OnImageLost += OnImageLost;
            planeHandling.OnPlaneReady += OnPlaneReady;
            planeHandling.OnPlaneInteraction += OnPlaneInteraction;
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

        /// <summary>
        ///     TEMP: callback for plane touch interactions - spawns test prefab at touch location
        /// </summary>
        private void OnPlaneInteraction(HandledPlaneInteraction interaction)
        {
            Logkat.Out($"CoreGameplay: touch detected at {interaction.Position}");

            // TEMP: skip if no test prefab assigned
            if (!tempTestPrefab)
            {
                Logkat.Warn("CoreGameplay.OnPlaneInteraction: no tempTestPrefab assigned, skipping spawn");
                return;
            }

            // TEMP: spawn the test prefab directly at the touch pose (already on plane)
            Instantiate(tempTestPrefab, interaction.Pose.position, interaction.Pose.rotation);
            Logkat.Out($"CoreGameplay: TEMP spawned {tempTestPrefab.name} at touch location");
        }

        /// <summary>
        ///     callback for tracked image detection - updates state and spawns test prefab
        /// </summary>
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

            // Logkat.Out($"CoreGameplay: image detected '{obj.Image.referenceImage.name}' at {obj.Image.transform.position}");

            // TEMP: skip if no test prefab assigned
            if (!tempTestPrefab)
            {
                Logkat.Warn("CoreGameplay.OnImageDetected: no tempTestPrefab assigned, skipping spawn");
                return;
            }

            // TEMP: skip if we already spawned for this trackable
            if (_tempSpawnedByTrackable.ContainsKey(obj.Id))
            {
                // update the existing object's transform to follow the tracked image
                var existing = _tempSpawnedByTrackable[obj.Id];
                if (existing)
                    existing.transform.SetPositionAndRotation(obj.Image.transform.position,
                        obj.Image.transform.rotation);
                return;
            }

            // TEMP: spawn test prefab at the tracked image position using SpawnClosest
            // (projects the in-air image position down onto the closest detected plane)
            var spawned = planeHandling.SpawnClosest(tempTestPrefab, obj.Image.transform.position, arCamera);

            if (spawned)
            {
                _tempSpawnedByTrackable[obj.Id] = spawned;
                Logkat.Out(
                    $"CoreGameplay: TEMP spawned {tempTestPrefab.name} via SpawnClosest for image '{obj.Image.referenceImage.name}'");
            }
            else
            {
                Logkat.Warn("CoreGameplay.OnImageDetected: SpawnClosest failed (no plane available?)");
            }
        }

        /// <summary>
        ///     callback for tracked image loss - optionally destroys spawned object based on toggle
        /// </summary>
        private void OnImageLost(HandledTrackedImage obj)
        {
            Logkat.Out($"CoreGameplay: image lost '{obj.Image.referenceImage.name}'");

            // TEMP: skip removal if toggle is disabled
            if (!tempRemoveOnUntrack)
            {
                Logkat.Out("CoreGameplay: tempRemoveOnUntrack is false, keeping spawned object");
                return;
            }

            // TEMP: remove the spawned object if it exists
            if (!_tempSpawnedByTrackable.TryGetValue(obj.Id, out var spawned)) return;

            if (spawned)
            {
                Destroy(spawned);
                Logkat.Out(
                    $"CoreGameplay: TEMP destroyed spawned object for lost image '{obj.Image.referenceImage.name}'");
            }

            _tempSpawnedByTrackable.Remove(obj.Id);
        }
    }
}