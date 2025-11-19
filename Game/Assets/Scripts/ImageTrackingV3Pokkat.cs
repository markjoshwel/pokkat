/*
 * author: mark joshwel
 * date: 19/11/2025
 * description: maps reference images to prefabs and manages their lifecycle per trackable
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
///     Spawns a prefab per trackable ID and keeps it aligned with the tracked image.
/// </summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackingV3Pokkat : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so tracked image events can be filtered easily.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) ImageTracking:";

    /// <summary>
    ///     Enables verbose logging for debugging lifecycle events.
    /// </summary>
    [SerializeField] private bool loggingEnabled;

    /// <summary>
    ///     Mapping between reference image names and prefabs to instantiate.
    /// </summary>
    [SerializeField] private List<TrackedPrefab> trackedPrefabs = new();

    /// <summary>
    ///     Lookup table that resolves reference image names (and variants) to prefabs.
    /// </summary>
    private readonly Dictionary<string, GameObject> _prefabLookup = new(StringComparer.Ordinal);

    /// <summary>
    ///     Runtime map between active trackable IDs and their instantiated prefab instances.
    /// </summary>
    private readonly Dictionary<TrackableId, GameObject> _spawnedPrefabs = new();

    /// <summary>
    ///     Image tracking manager providing AR tracked-image events for this behaviour.
    /// </summary>
    private ARTrackedImageManager _trackedImageManager;

    private void Awake()
    {
        _trackedImageManager = GetComponent<ARTrackedImageManager>();
        if (!_trackedImageManager)
            throw new MissingComponentException(
                $"{LoggingPrefix} an ARTrackedImageManager is required in the same GameObject");

        BuildPrefabLookup();
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
    ///     Builds a dictionary for resolving prefabs by reference image name.
    /// </summary>
    private void BuildPrefabLookup()
    {
        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Building prefab lookup");

        _prefabLookup.Clear();

        foreach (var entry in trackedPrefabs)
        {
            if (string.IsNullOrWhiteSpace(entry.referenceImageName))
            {
                Debug.LogWarning($"{LoggingPrefix} Skipping prefab with empty reference image name");
                continue;
            }

            if (!entry.prefab)
            {
                Debug.LogWarning(
                    $"{LoggingPrefix} Prefab for reference image '{entry.referenceImageName}' is not assigned");
                continue;
            }

            if (_prefabLookup.ContainsKey(entry.referenceImageName))
            {
                Debug.LogWarning($"{LoggingPrefix} Duplicate entry for reference image '{entry.referenceImageName}'");
                continue;
            }

            _prefabLookup.Add(entry.referenceImageName, entry.prefab);
            if (loggingEnabled) Debug.Log($"{LoggingPrefix} Registered prefab '{entry.referenceImageName}'");
        }

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Prefab lookup size: {_prefabLookup.Count}");
    }

    /// <summary>
    ///     Handles AR tracked-image lifecycle notifications from the manager.
    /// </summary>
    /// <param name="args">Event payload describing added, updated, and removed tracked images.</param>
    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        if (loggingEnabled)
            Debug.Log(
                $"{LoggingPrefix} Trackables changed -> added:{args.added.Count} updated:{args.updated.Count} removed:{args.removed.Count}");

        foreach (var trackedImage in args.added) UpdateOrCreateInstance(trackedImage);

        foreach (var trackedImage in args.updated) UpdateOrCreateInstance(trackedImage);

        foreach (var trackedImagePair in args.removed) RemoveInstance(trackedImagePair.Key);
    }

    /// <summary>
    ///     Creates or updates the prefab instance mapped to a tracked image.
    /// </summary>
    /// <param name="trackedImage">Tracked image whose pose and tracking state determine prefab behaviour.</param>
    private void UpdateOrCreateInstance(ARTrackedImage trackedImage)
    {
        var referenceName = trackedImage.referenceImage.name;
        var state = trackedImage.trackingState;

        if (!_prefabLookup.TryGetValue(referenceName, out var prefab))
        {
            Debug.LogWarning($"{LoggingPrefix} No prefab mapped for reference image '{referenceName}'");
            return;
        }

        if (!_spawnedPrefabs.TryGetValue(trackedImage.trackableId, out var instance) || !instance)
        {
            if (loggingEnabled)
                Debug.Log($"{LoggingPrefix} Creating instance for '{referenceName}' ({trackedImage.trackableId})");

            instance = Instantiate(prefab, trackedImage.transform.position, trackedImage.transform.rotation);
            instance.transform.SetParent(trackedImage.transform, false);
            AlignWithTrackedImage(instance.transform, trackedImage.transform);
            _spawnedPrefabs[trackedImage.trackableId] = instance;
        }
        else if (loggingEnabled)
        {
            Debug.Log(
                $"{LoggingPrefix} Updating instance for '{referenceName}' ({trackedImage.trackableId}) state:{state}");
        }

        if (state == TrackingState.Tracking) AlignWithTrackedImage(instance.transform, trackedImage.transform);

        var shouldDisplay = ShouldDisplay(state);
        instance.SetActive(shouldDisplay);
        if (loggingEnabled)
            Debug.Log($"{LoggingPrefix} Instance '{referenceName}' active:{shouldDisplay} trackingState:{state}");
    }

    /// <summary>
    ///     Removes the prefab associated with the supplied trackable ID.
    /// </summary>
    /// <param name="trackableId">Identifier of the tracked image whose prefab should be destroyed.</param>
    private void RemoveInstance(TrackableId trackableId)
    {
        if (!_spawnedPrefabs.TryGetValue(trackableId, out var instance))
        {
            if (loggingEnabled) Debug.Log($"{LoggingPrefix} Remove requested for unknown trackable {trackableId}");

            return;
        }

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Removing instance {trackableId}");

        if (instance) Destroy(instance);

        _spawnedPrefabs.Remove(trackableId);
    }

    private static bool ShouldDisplay(TrackingState state)
    {
        return state != TrackingState.None;
    }

    private static void AlignWithTrackedImage(Transform target, Transform source)
    {
        target.SetPositionAndRotation(source.position, source.rotation);
        target.localScale = Vector3.one;
    }

    [Serializable]
    private struct TrackedPrefab
    {
        [Tooltip("Reference image name as defined in the XR Reference Image Library")]
        public string referenceImageName;

        public GameObject prefab;
    }
}