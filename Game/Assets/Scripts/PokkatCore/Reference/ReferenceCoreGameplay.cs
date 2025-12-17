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

namespace PokkatCore.Reference
{
    /// <summary>
    ///     mapping between reference image names and prefabs for neko spawning
    /// </summary>
    [Serializable]
    public struct ReferenceNekoPrefabMapping
    {
        [Tooltip("Reference image name as defined in the XR Reference Image Library.")]
        public string referenceImageName;

        [Tooltip("Prefab to spawn when this image is detected.")]
        public GameObject prefab;

        [Tooltip("Whether this is the main neko (affects spawn priority).")]
        public bool isMainNeko;
    }

    /// <summary>
    ///     central logic coordinator managing neko spawning, bowl registration, and game state
    /// </summary>
    public class ReferenceCoreGameplay : MonoBehaviour
    {
        [Header("Dependencies")]
        [HelpBox("Assign all required AR detection and handling components here.", HelpBoxMessageType.Info)]
        [Tooltip("Handles image tracking events from ARTrackedImageManager.")]
        [SerializeField]
        private ReferenceImageDetection referenceImageDetection;

        [Tooltip("Handles plane detection events from ARPlaneManager.")] [SerializeField]
        private ReferencePlaneDetection referencePlaneDetection;

        [Tooltip("Handles spawning objects onto detected planes and NavMesh baking.")] [SerializeField]
        private ReferencePlaneHandler referencePlaneHandler;

        [Tooltip("Manages persistent game statistics.")] [SerializeField]
        private ReferenceStatskeeper referenceStatskeeper;

        [Header("Neko Configuration")]
        [Tooltip("Mappings from reference image names to neko prefabs.")]
        [SerializeField]
        private List<ReferenceNekoPrefabMapping> nekoPrefabMappings = new();

        [Tooltip("Default prefab for the main neko character.")] [SerializeField]
        private GameObject defaultNekoPrefab;

        [Tooltip("Prefab for friend/secondary neko characters.")] [SerializeField]
        private GameObject friendNekoPrefab;

        [Header("Spawn Settings")] [Tooltip("Maximum number of nekos that can be active at once.")] [SerializeField]
        private int maxActiveNekos = 5;

        [Tooltip("Spawn nekos facing the camera.")] [SerializeField]
        private bool spawnLookingAtPlayer = true;

        private readonly Dictionary<TrackableId, GameObject> _activeNekos = new();
        private readonly Dictionary<string, GameObject> _prefabLookup = new();
        private int _mainNekoCount;
        private ReferenceAREntityBowl _registeredBowl;

        /// <summary>
        ///     whether the game is ready for spawning (planes detected)
        /// </summary>
        public bool gameReady { get; private set; }

        /// <summary>
        ///     number of currently active neko instances
        /// </summary>
        public int activeNekoCount => _activeNekos.Count;

        private void Awake()
        {
            ValidateDependencies();
            BuildPrefabLookup();
        }

        private void Start()
        {
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        ///     function to validate that all required dependencies are assigned
        /// </summary>
        private void ValidateDependencies()
        {
            if (!referenceImageDetection)
                Debug.LogWarning("ReferenceCoreGameplay: ReferenceImageDetection reference is missing");

            if (!referencePlaneDetection)
                Debug.LogWarning("ReferenceCoreGameplay: ReferencePlaneDetection reference is missing");

            if (!referencePlaneHandler)
                Debug.LogWarning("ReferenceCoreGameplay: ReferencePlaneHandler reference is missing");

            if (!referenceStatskeeper)
                Debug.LogWarning("ReferenceCoreGameplay: ReferenceStatskeeper reference is missing");
        }

        /// <summary>
        ///     function to build the prefab lookup from configured mappings
        /// </summary>
        private void BuildPrefabLookup()
        {
            _prefabLookup.Clear();

            foreach (var mapping in nekoPrefabMappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.referenceImageName) || !mapping.prefab)
                {
                    Debug.LogWarning("ReferenceCoreGameplay: skipping invalid prefab mapping");
                    continue;
                }

                if (_prefabLookup.ContainsKey(mapping.referenceImageName))
                {
                    Debug.LogWarning($"ReferenceCoreGameplay: duplicate mapping for '{mapping.referenceImageName}'");
                    continue;
                }

                _prefabLookup[mapping.referenceImageName] = mapping.prefab;
                Debug.Log($"ReferenceCoreGameplay: registered prefab for '{mapping.referenceImageName}'");
            }
        }

        /// <summary>
        ///     function to subscribe to detection events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (referenceImageDetection)
            {
                referenceImageDetection.OnImageDetected += OnReferenceImageDetected;
                referenceImageDetection.OnImageLost += OnReferenceImageLost;
            }

            if (referencePlaneDetection)
                referencePlaneDetection.OnPlaneReady += OnReferencePlaneReady;
        }

        /// <summary>
        ///     function to unsubscribe from detection events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (referenceImageDetection)
            {
                referenceImageDetection.OnImageDetected -= OnReferenceImageDetected;
                referenceImageDetection.OnImageLost -= OnReferenceImageLost;
            }

            if (referencePlaneDetection)
                referencePlaneDetection.OnPlaneReady -= OnReferencePlaneReady;
        }

        /// <summary>
        ///     callback for when plane detection threshold is met
        /// </summary>
        /// <param name="plane">largest detected plane</param>
        private void OnReferencePlaneReady(ARPlane plane)
        {
            gameReady = true;
            Debug.Log($"ReferenceCoreGameplay: game ready, plane detected ({plane.trackableId})");
        }

        /// <summary>
        ///     callback for when an image is detected and tracking
        /// </summary>
        /// <param name="trackedImage">the detected ar tracked image</param>
        private void OnReferenceImageDetected(ARTrackedImage trackedImage)
        {
            if (_activeNekos.ContainsKey(trackedImage.trackableId))
                return;

            if (!gameReady)
            {
                Debug.Log("ReferenceCoreGameplay: ignoring image detection, game not ready yet");
                return;
            }

            if (_activeNekos.Count >= maxActiveNekos)
            {
                Debug.Log($"ReferenceCoreGameplay: maximum nekos reached ({maxActiveNekos})");
                return;
            }

            SpawnNekoForImage(trackedImage);
        }

        /// <summary>
        ///     callback for when a tracked image is lost
        /// </summary>
        /// <param name="trackableId">id of the lost trackable</param>
        private void OnReferenceImageLost(TrackableId trackableId)
        {
            if (!_activeNekos.ContainsKey(trackableId)) return;
            Debug.Log("ReferenceCoreGameplay: image lost, neko remains spawned at last position");
        }

        /// <summary>
        ///     function to spawn a neko for the detected image
        /// </summary>
        /// <param name="trackedImage">the tracked image to spawn a neko for</param>
        private void SpawnNekoForImage(ARTrackedImage trackedImage)
        {
            var referenceName = trackedImage.referenceImage.name;
            var prefab = ResolvePrefabForImage(referenceName);

            if (!prefab)
            {
                Debug.LogWarning($"ReferenceCoreGameplay: no prefab resolved for image '{referenceName}'");
                return;
            }

            if (!referencePlaneHandler)
            {
                Debug.LogError("ReferenceCoreGameplay: cannot spawn, ReferencePlaneHandler is missing");
                return;
            }

            var spawnPosition = trackedImage.transform.position;
            var instance = referencePlaneHandler.SpawnClosest(spawnPosition, prefab, spawnLookingAtPlayer);

            if (!instance)
            {
                Debug.LogWarning($"ReferenceCoreGameplay: failed to spawn neko for '{referenceName}'");
                return;
            }

            _activeNekos[trackedImage.trackableId] = instance;

            var nekoComponent = instance.GetComponent<ReferenceAREntityNeko>();
            if (nekoComponent && _registeredBowl)
                nekoComponent.SetTargetBowl(_registeredBowl);

            Debug.Log($"ReferenceCoreGameplay: spawned neko for '{referenceName}' (total: {_activeNekos.Count})");
        }

        /// <summary>
        ///     function to resolve which prefab to use for a given reference image
        /// </summary>
        /// <param name="referenceName">reference image name</param>
        /// <returns>prefab to instantiate</returns>
        private GameObject ResolvePrefabForImage(string referenceName)
        {
            if (_prefabLookup.TryGetValue(referenceName, out var mappedPrefab))
                return mappedPrefab;

            if (_mainNekoCount != 0 || !defaultNekoPrefab)
                return friendNekoPrefab ? friendNekoPrefab : defaultNekoPrefab;

            _mainNekoCount++;
            return defaultNekoPrefab;
        }

        /// <summary>
        ///     function to register a bowl for neko interactions
        /// </summary>
        /// <param name="bowl">bowl to register</param>
        public void RegisterBowl(ReferenceAREntityBowl bowl)
        {
            _registeredBowl = bowl;

            foreach (var nekoComponent in from nekoPair in _activeNekos
                     where nekoPair.Value
                     select nekoPair.Value.GetComponent<ReferenceAREntityNeko>()) nekoComponent?.SetTargetBowl(bowl);

            Debug.Log($"ReferenceCoreGameplay: bowl registered ({bowl.name})");
        }

        /// <summary>
        ///     function to notify that a neko consumed food from a bowl
        /// </summary>
        /// <param name="hungerAmount">amount of hunger restored</param>
        public void NotifyFoodConsumed(float hungerAmount)
        {
            if (!referenceStatskeeper) return;

            referenceStatskeeper.IncreaseHunger(hungerAmount);
            referenceStatskeeper.ModifyHappiness(5f);
        }

        /// <summary>
        ///     function to trigger all active nekos to seek the registered bowl
        /// </summary>
        public void CommandNekosToSeekBowl()
        {
            if (!_registeredBowl)
            {
                Debug.LogWarning("ReferenceCoreGameplay: no bowl registered, cannot command nekos to seek");
                return;
            }

            foreach (var nekoComponent in from nekoPair in _activeNekos
                     where nekoPair.Value
                     select nekoPair.Value.GetComponent<ReferenceAREntityNeko>()) nekoComponent?.SeekBowl();

            Debug.Log($"ReferenceCoreGameplay: commanded {_activeNekos.Count} nekos to seek bowl");
        }

        /// <summary>
        ///     function to remove a specific neko from active tracking
        /// </summary>
        /// <param name="trackableId">trackable id of the neko to remove</param>
        public void RemoveNeko(TrackableId trackableId)
        {
            if (!_activeNekos.TryGetValue(trackableId, out var neko)) return;

            if (neko)
                Destroy(neko);

            _activeNekos.Remove(trackableId);
            Debug.Log($"ReferenceCoreGameplay: removed neko {trackableId} (remaining: {_activeNekos.Count})");
        }

        /// <summary>
        ///     function to remove all active nekos
        /// </summary>
        public void ClearAllNekos()
        {
            foreach (var nekoPair in _activeNekos.Where(nekoPair => nekoPair.Value))
                Destroy(nekoPair.Value);

            _activeNekos.Clear();
            _mainNekoCount = 0;
            Debug.Log("ReferenceCoreGameplay: cleared all nekos");
        }
    }
}