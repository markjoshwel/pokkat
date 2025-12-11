/*
 * author: mark joshwel
 * date: 11/12/2024
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
    /// <summary>
    ///     mapping between reference image names and prefabs for neko spawning
    /// </summary>
    [Serializable]
    public struct NekoPrefabMapping
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
    public class CoreGameplay : MonoBehaviour
    {
        [Header("Dependencies")]
        [HelpBox("Assign all required AR detection and handling components here.", HelpBoxMessageType.Info)]
        [Tooltip("Handles image tracking events from ARTrackedImageManager.")]
        [SerializeField]
        private ImageDetection imageDetection;

        [Tooltip("Handles plane detection events from ARPlaneManager.")] [SerializeField]
        private PlaneDetection planeDetection;

        [Tooltip("Handles spawning objects onto detected planes and NavMesh baking.")] [SerializeField]
        private PlaneHandler planeHandler;

        [Tooltip("Manages persistent game statistics.")] [SerializeField]
        private Statskeeper statskeeper;

        [Header("Neko Configuration")]
        [Tooltip("Mappings from reference image names to neko prefabs.")]
        [SerializeField]
        private List<NekoPrefabMapping> nekoPrefabMappings = new();

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
        private AREntityBowl _registeredBowl;

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
            if (!imageDetection)
                Debug.LogWarning("CoreGameplay: ImageDetection reference is missing");

            if (!planeDetection)
                Debug.LogWarning("CoreGameplay: PlaneDetection reference is missing");

            if (!planeHandler)
                Debug.LogWarning("CoreGameplay: PlaneHandler reference is missing");

            if (!statskeeper)
                Debug.LogWarning("CoreGameplay: Statskeeper reference is missing");
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
                    Debug.LogWarning("CoreGameplay: skipping invalid prefab mapping");
                    continue;
                }

                if (_prefabLookup.ContainsKey(mapping.referenceImageName))
                {
                    Debug.LogWarning($"CoreGameplay: duplicate mapping for '{mapping.referenceImageName}'");
                    continue;
                }

                _prefabLookup[mapping.referenceImageName] = mapping.prefab;
                Debug.Log($"CoreGameplay: registered prefab for '{mapping.referenceImageName}'");
            }
        }

        /// <summary>
        ///     function to subscribe to detection events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (imageDetection)
            {
                imageDetection.OnImageDetected += OnImageDetected;
                imageDetection.OnImageLost += OnImageLost;
            }

            if (planeDetection)
                planeDetection.OnPlaneReady += OnPlaneReady;
        }

        /// <summary>
        ///     function to unsubscribe from detection events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (imageDetection)
            {
                imageDetection.OnImageDetected -= OnImageDetected;
                imageDetection.OnImageLost -= OnImageLost;
            }

            if (planeDetection)
                planeDetection.OnPlaneReady -= OnPlaneReady;
        }

        /// <summary>
        ///     callback for when plane detection threshold is met
        /// </summary>
        /// <param name="plane">largest detected plane</param>
        private void OnPlaneReady(ARPlane plane)
        {
            gameReady = true;
            Debug.Log($"CoreGameplay: game ready, plane detected ({plane.trackableId})");
        }

        /// <summary>
        ///     callback for when an image is detected and tracking
        /// </summary>
        /// <param name="trackedImage">the detected ar tracked image</param>
        private void OnImageDetected(ARTrackedImage trackedImage)
        {
            if (_activeNekos.ContainsKey(trackedImage.trackableId))
                return;

            if (!gameReady)
            {
                Debug.Log("CoreGameplay: ignoring image detection, game not ready yet");
                return;
            }

            if (_activeNekos.Count >= maxActiveNekos)
            {
                Debug.Log($"CoreGameplay: maximum nekos reached ({maxActiveNekos})");
                return;
            }

            SpawnNekoForImage(trackedImage);
        }

        /// <summary>
        ///     callback for when a tracked image is lost
        /// </summary>
        /// <param name="trackableId">id of the lost trackable</param>
        private void OnImageLost(TrackableId trackableId)
        {
            if (!_activeNekos.ContainsKey(trackableId)) return;
            Debug.Log("CoreGameplay: image lost, neko remains spawned at last position");
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
                Debug.LogWarning($"CoreGameplay: no prefab resolved for image '{referenceName}'");
                return;
            }

            if (!planeHandler)
            {
                Debug.LogError("CoreGameplay: cannot spawn, PlaneHandler is missing");
                return;
            }

            var spawnPosition = trackedImage.transform.position;
            var instance = planeHandler.SpawnClosest(spawnPosition, prefab, spawnLookingAtPlayer);

            if (!instance)
            {
                Debug.LogWarning($"CoreGameplay: failed to spawn neko for '{referenceName}'");
                return;
            }

            _activeNekos[trackedImage.trackableId] = instance;

            var nekoComponent = instance.GetComponent<AREntityNeko>();
            if (nekoComponent && _registeredBowl)
                nekoComponent.SetTargetBowl(_registeredBowl);

            Debug.Log($"CoreGameplay: spawned neko for '{referenceName}' (total: {_activeNekos.Count})");
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
        public void RegisterBowl(AREntityBowl bowl)
        {
            _registeredBowl = bowl;

            foreach (var nekoComponent in from nekoPair in _activeNekos
                     where nekoPair.Value
                     select nekoPair.Value.GetComponent<AREntityNeko>()) nekoComponent?.SetTargetBowl(bowl);

            Debug.Log($"CoreGameplay: bowl registered ({bowl.name})");
        }

        /// <summary>
        ///     function to notify that a neko consumed food from a bowl
        /// </summary>
        /// <param name="hungerAmount">amount of hunger restored</param>
        public void NotifyFoodConsumed(float hungerAmount)
        {
            if (!statskeeper) return;

            statskeeper.IncreaseHunger(hungerAmount);
            statskeeper.ModifyHappiness(5f);
        }

        /// <summary>
        ///     function to trigger all active nekos to seek the registered bowl
        /// </summary>
        public void CommandNekosToSeekBowl()
        {
            if (!_registeredBowl)
            {
                Debug.LogWarning("CoreGameplay: no bowl registered, cannot command nekos to seek");
                return;
            }

            foreach (var nekoComponent in from nekoPair in _activeNekos
                     where nekoPair.Value
                     select nekoPair.Value.GetComponent<AREntityNeko>()) nekoComponent?.SeekBowl();

            Debug.Log($"CoreGameplay: commanded {_activeNekos.Count} nekos to seek bowl");
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
            Debug.Log($"CoreGameplay: removed neko {trackableId} (remaining: {_activeNekos.Count})");
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
            Debug.Log("CoreGameplay: cleared all nekos");
        }
    }
}