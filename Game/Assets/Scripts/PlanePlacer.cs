/*
 * author: mark joshwel
 * date: 17/11/2025
 * description: places the configured prefab on tapped planes after raycasting hits
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
///     Places the ARRaycastManager prefab on touch/click when a tracked plane is hit.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class PlanePlacer : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so placement activity can be filtered in the console.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) PlanePlacer:";

    /// <summary>
    ///     AR raycast manager used to perform plane hits.
    /// </summary>
    [SerializeField] private ARRaycastManager raycastManager;

    /// <summary>
    ///     Enables verbose logging of placement attempts.
    /// </summary>
    [SerializeField] private bool loggingEnabled = true;

    /// <summary>
    ///     Cooldown duration between successful placements.
    /// </summary>
    [Min(0f)] [SerializeField] private float placementCooldownSeconds = 0.25f;

    /// <summary>
    ///     Reusable collection for AR raycast hit results.
    /// </summary>
    private readonly List<ARRaycastHit> _raycastHits = new();

    /// <summary>
    ///     Coroutine handle used to enforce the placement cooldown window.
    /// </summary>
    private Coroutine _placementCooldownRoutine;

    private void Awake()
    {
        raycastManager ??= GetComponent<ARRaycastManager>();
        if (!raycastManager)
            throw new MissingComponentException(
                $"{LoggingPrefix} an ARRaycastManager component is required in the same GameObject");
    }

    private void Update()
    {
        if (_placementCooldownRoutine != null || !TryGetPlacementPosition(out var inputPosition)) return;

        if (!TryPlaceObjectAtTouch(inputPosition)) return;

        if (placementCooldownSeconds <= 0f) return;

        _placementCooldownRoutine = StartCoroutine(PlacementCooldownRoutine());
    }

    private void OnDisable()
    {
        if (_placementCooldownRoutine == null) return;

        StopCoroutine(_placementCooldownRoutine);
        _placementCooldownRoutine = null;
    }

    /// <summary>
    ///     Prevents negative cooldown assignments in the inspector.
    /// </summary>
    private void OnValidate()
    {
        placementCooldownSeconds = Mathf.Max(0f, placementCooldownSeconds);
    }

    /// <summary>
    ///     Attempts to read the current pointer/touch position.
    /// </summary>
    /// <param name="position">Screen position of the initiating touch or mouse click.</param>
    /// <returns><c>true</c> when a valid pointer position was retrieved.</returns>
    private static bool TryGetPlacementPosition(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current is { } touchscreen)
        {
            var touch = touchscreen.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                position = touch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current is { } mouse && mouse.leftButton.wasPressedThisFrame)
        {
            position = mouse.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        var touchExists = Input.touchCount > 0;
        if (touchExists && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        position = default;
        return false;
    }

    /// <summary>
    ///     Raycasts against planes and instantiates the configured prefab when possible.
    /// </summary>
    /// <param name="touchPosition">Screen position used to perform the AR raycast.</param>
    /// <returns><c>true</c> when a prefab was placed successfully.</returns>
    private bool TryPlaceObjectAtTouch(Vector2 touchPosition)
    {
        _raycastHits.Clear();
        raycastManager.Raycast(touchPosition, _raycastHits, TrackableType.Planes);

        if (_raycastHits.Count == 0) return false;

        var hitPose = _raycastHits[0].pose;

        if (!raycastManager.raycastPrefab)
        {
            if (loggingEnabled) Debug.LogError($"{LoggingPrefix} Cannot place; raycastPrefab is not assigned.");

            return false;
        }

        Instantiate(raycastManager.raycastPrefab, hitPose.position, hitPose.rotation);
        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Placed prefab at {hitPose.position}.");

        return true;
    }

    /// <summary>
    ///     Small cooldown so multiple placements do not happen in a single frame.
    /// </summary>
    private IEnumerator PlacementCooldownRoutine()
    {
        yield return new WaitForSeconds(placementCooldownSeconds);
        _placementCooldownRoutine = null;
    }
}