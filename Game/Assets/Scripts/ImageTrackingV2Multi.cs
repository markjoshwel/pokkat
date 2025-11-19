/*
 * author: mark joshwel
 * date: 18/11/2025
 * description: spawns prefabs for detected images using name variants to resolve the mapping
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
///     Spawns prefabs for every tracked image while honouring multiple name variants.
/// </summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackingV2Multi : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so tracked image events can be filtered easily.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) ImageTracking:";

    /// <summary>
    ///     Enables verbose logging for debugging tracked image activity.
    /// </summary>
    [SerializeField] private bool loggingEnabled = true;

    /// <summary>
    ///     Prefabs that can be matched by exact name, name + "Prefab", or name + "Object".
    /// </summary>
    [SerializeField] private List<GameObject> prefabsToSpawn = new();

    /// <summary>
    ///     Lookup table that resolves reference image names (and variants) to prefabs.
    /// </summary>
    private readonly Dictionary<string, GameObject> _prefabLookup = new(StringComparer.Ordinal);

    /// <summary>
    ///     Runtime map between active trackable IDs and their instantiated prefab instances.
    /// </summary>
    private readonly Dictionary<TrackableId, GameObject> _spawnedObjects = new();

    /// <summary>
    ///     Image tracking manager providing AR tracked-image events for this behaviour.
    /// </summary>
    private ARTrackedImageManager _trackedImageManager;

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        if (!_trackedImageManager)
            throw new MissingComponentException($"{LoggingPrefix} ARTrackedImageManager component not found");

        BuildPrefabLookup();
    }

    private void OnEnable()
    {
        _trackedImageManager.trackablesChanged.AddListener(OnImagesTrackedChanged);
    }

    private void OnDisable()
    {
        _trackedImageManager.trackablesChanged.RemoveListener(OnImagesTrackedChanged);
    }

    /// <summary>
    ///     Builds a dictionary for quick prefab lookups.
    /// </summary>
    private void BuildPrefabLookup()
    {
        _prefabLookup.Clear();

        foreach (var prefab in prefabsToSpawn)
        {
            if (!prefab) continue;

            if (_prefabLookup.ContainsKey(prefab.name))
            {
                Debug.LogWarning($"{LoggingPrefix} Duplicate prefab entry '{prefab.name}' skipped.");
                continue;
            }

            _prefabLookup.Add(prefab.name, prefab);
        }
    }

    /// <summary>
    ///     Responds to tracked-image lifecycle notifications by instantiating, updating, or removing prefabs.
    /// </summary>
    /// <param name="eventArgs">Event payload containing added, updated, and removed tracked images.</param>
    private void OnImagesTrackedChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added) UpdateTrackedImage(trackedImage);
        foreach (var trackedImage in eventArgs.updated) UpdateTrackedImage(trackedImage);
        foreach (var removedPair in eventArgs.removed) HandleRemoval(removedPair.Key);
    }

    /// <summary>
    ///     Instantiates or updates the prefab associated with the supplied tracked image.
    /// </summary>
    /// <param name="trackedImage">Tracked image whose pose and tracking state govern prefab behaviour.</param>
    private void UpdateTrackedImage(ARTrackedImage trackedImage)

    {
        if (!trackedImage) return;

        if (!TryResolvePrefabName(trackedImage.referenceImage.name, out var resolvedName))
        {
            if (loggingEnabled)
                Debug.LogWarning($"{LoggingPrefix} No matching prefab found for '{trackedImage.referenceImage.name}'.");

            return;
        }

        if (!_spawnedObjects.TryGetValue(trackedImage.trackableId, out var instance) || !instance)
        {
            var prefab = _prefabLookup[resolvedName];
            instance = Instantiate(prefab, trackedImage.transform.position, trackedImage.transform.rotation);
            _spawnedObjects[trackedImage.trackableId] = instance;

            if (loggingEnabled) Debug.Log($"{LoggingPrefix} Instantiated AR object for '{resolvedName}'.");
        }

        if (trackedImage.trackingState is TrackingState.None or TrackingState.Limited)
        {
            instance.SetActive(false);
            return;
        }

        instance.SetActive(true);
        instance.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
    }


    /// <summary>
    ///     Handles clean-up when a tracked image leaves the session.
    /// </summary>
    /// <param name="trackableId">Identifier of the tracked image being removed.</param>
    private void HandleRemoval(TrackableId trackableId)

    {
        if (!_spawnedObjects.TryGetValue(trackableId, out var instance)) return;

        if (instance) Destroy(instance);

        _spawnedObjects.Remove(trackableId);

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Removed AR object for trackable {trackableId}.");
    }


    /// <summary>
    ///     Resolves possible prefab name variants for a given reference image name.
    /// </summary>
    /// <param name="referenceName">Base name of the reference image from the XR library.</param>
    /// <param name="resolvedName">Resolved prefab key that exists in the lookup dictionary.</param>
    /// <returns><c>true</c> when a matching prefab name was found.</returns>
    private bool TryResolvePrefabName(string referenceName, out string resolvedName)

    {
        var variants = new[]
        {
            referenceName,
            $"{referenceName}Prefab",
            $"{referenceName}Object"
        };

        foreach (var variant in variants)
            if (_prefabLookup.ContainsKey(variant))
            {
                resolvedName = variant;
                return true;
            }

        resolvedName = string.Empty;
        return false;
    }
}