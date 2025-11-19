/*
 * author: itd week 1 slides, mark joshwel
 * date: 16/11/2025
 * description: instantiates prefabs for tracked images and toggles visibility with tracking state
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
///     Instantiates one prefab per reference image and toggles its visibility with the tracking state.
/// </summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackingV1Elyas : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so tracked image events can be filtered in the console.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) ImageTracking:";

    /// <summary>
    ///     Enables verbose logging for tracked image events.
    /// </summary>
    [SerializeField] private bool loggingEnabled = true;

    /// <summary>
    ///     Manager responsible for tracking reference images.
    /// </summary>
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    /// <summary>
    ///     Prefabs that will be instantiated once and reused per reference image.
    /// </summary>
    [SerializeField] private GameObject[] placeablePrefabs = Array.Empty<GameObject>();

    /// <summary>
    ///     Cached prefab instances keyed by their reference image names.
    /// </summary>
    private readonly Dictionary<string, GameObject> _spawnedPrefabs = new(StringComparer.Ordinal);

    /// <summary>
    ///     Tracks whether the prefab cache has already been populated.
    /// </summary>
    private bool _prefabsInitialised;

    private void Awake()
    {
        trackedImageManager ??= GetComponent<ARTrackedImageManager>();
    }

    private void OnEnable()
    {
        if (!trackedImageManager)
        {
            Debug.LogError($"{LoggingPrefix} ARTrackedImageManager component not found; disabling behaviour.");
            enabled = false;
            return;
        }

        EnsurePrefabsInitialised();
        trackedImageManager.trackablesChanged.AddListener(OnImagesChanged);
    }

    private void OnDisable()
    {
        if (trackedImageManager) trackedImageManager.trackablesChanged.RemoveListener(OnImagesChanged);
    }

    /// <summary>
    ///     Instantiates inactive copies of every configured prefab for later toggling.
    /// </summary>
    private void EnsurePrefabsInitialised()
    {
        if (_prefabsInitialised) return;

        _spawnedPrefabs.Clear();

        foreach (var prefab in placeablePrefabs)
        {
            if (!prefab) continue;

            if (_spawnedPrefabs.ContainsKey(prefab.name))
            {
                Debug.LogWarning($"{LoggingPrefix} Duplicate prefab entry '{prefab.name}' skipped.");
                continue;
            }

            var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            instance.name = prefab.name;
            instance.SetActive(false);
            _spawnedPrefabs.Add(prefab.name, instance);

            if (loggingEnabled) Debug.Log($"{LoggingPrefix} Prepared prefab '{prefab.name}'.");
        }

        _prefabsInitialised = true;
    }

    /// <summary>
    ///     Handles AR tracked-image lifecycle events from the image manager.
    /// </summary>
    /// <param name="eventArgs">Added, updated, and removed tracked-image collections.</param>
    private void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added) HandleTrackedImage(trackedImage);

        foreach (var trackedImage in eventArgs.updated) HandleTrackedImage(trackedImage);

        foreach (var removedPair in eventArgs.removed)
        {
            var removedImage = removedPair.Value;
            if (!removedImage) continue;

            HidePrefabByReferenceName(removedImage.referenceImage.name);
        }
    }

    /// <summary>
    ///     Aligns the prefab to the tracked pose or hides it when tracking quality drops.
    /// </summary>
    /// <param name="trackedImage">Tracked image providing pose and tracking state.</param>
    private void HandleTrackedImage(ARTrackedImage trackedImage)
    {
        if (!trackedImage) return;

        var referenceName = trackedImage.referenceImage.name;
        if (!_spawnedPrefabs.TryGetValue(referenceName, out var prefabInstance))
        {
            if (loggingEnabled) Debug.LogWarning($"{LoggingPrefix} No prefab prepared for '{referenceName}'.");

            return;
        }

        switch (trackedImage.trackingState)
        {
            case TrackingState.None:
            case TrackingState.Limited:
                prefabInstance.SetActive(false);
                if (loggingEnabled)
                    Debug.Log($"{LoggingPrefix} Hiding '{referenceName}' due to state {trackedImage.trackingState}.");

                break;

            case TrackingState.Tracking:
                prefabInstance.transform.SetPositionAndRotation(
                    trackedImage.transform.position,
                    trackedImage.transform.rotation);
                prefabInstance.SetActive(true);

                if (loggingEnabled) Debug.Log($"{LoggingPrefix} Showing '{referenceName}' at tracked pose.");

                break;
        }
    }

    /// <summary>
    ///     Hides a prefab instance by its reference image name without touching destroyed trackables.
    /// </summary>
    /// <param name="referenceName">Name defined in the XR Reference Image Library.</param>
    private void HidePrefabByReferenceName(string referenceName)
    {
        if (string.IsNullOrWhiteSpace(referenceName)) return;

        if (!_spawnedPrefabs.TryGetValue(referenceName, out var prefabInstance) || !prefabInstance)
        {
            if (loggingEnabled)
                Debug.LogWarning($"{LoggingPrefix} Removal event for '{referenceName}' but prefab is missing.");

            return;
        }

        prefabInstance.SetActive(false);
        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Removal event hid prefab '{referenceName}'.");
    }
}