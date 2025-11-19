/*
 * author: mark joshwel
 * date: 19/11/2025
 * description: constrains the neko character to tracked planes and guides it toward the bowl target
 */

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
///     Locks a neko character to the closest tracked plane and moves it toward the bowl target.
/// </summary>
public class CoreDemo : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so they can be filtered easily in the console.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) CoreDemo:";

    /// <summary>
    ///     Plane manager responsible for notifying this behaviour about detected surfaces.
    /// </summary>
    [SerializeField] private ARPlaneManager planeManager;

    /// <summary>
    ///     Direct movement speed (in metres per second) when NavMesh navigation is unavailable.
    /// </summary>
    [SerializeField] private float moveSpeedMetersPerSecond = 0.4f;

    /// <summary>
    ///     Distance threshold (metres) before the neko is considered close enough to the bowl.
    /// </summary>
    [SerializeField] private float stopDistanceMeters = 0.05f;

    /// <summary>
    ///     Chooses whether to prefer NavMesh-driven movement when an agent is present.
    /// </summary>
    [SerializeField] private bool preferNavMeshIfAvailable = true;

    /// <summary>
    ///     Enables verbose logging for debugging plane locking or movement.
    /// </summary>
    [SerializeField] private bool loggingEnabled = true;

    /// <summary>
    ///     Plane currently used to constrain the movement logic.
    /// </summary>
    private ARPlane _activePlane;

    /// <summary>
    ///     Reference to the bowl target.
    /// </summary>
    private GameObject _bowl;

    /// <summary>
    ///     Reference to the neko character instance.
    /// </summary>
    private GameObject _neko;

    /// <summary>
    ///     Optional NavMeshAgent extracted from the neko.
    /// </summary>
    private NavMeshAgent _nekoAgent;

    /// <summary>
    ///     Whether the behaviour currently tracks a valid plane.
    /// </summary>
    private bool _planeLocked;

    /// <summary>
    ///     Ensures a plane manager dependency exists even if it lives deeper in the rig.
    /// </summary>
    private void Awake()
    {
        if (planeManager) return;

        planeManager = GetComponentInChildren<ARPlaneManager>(true);
        if (!planeManager)
            Debug.LogWarning($"{LoggingPrefix} could not locate an ARPlaneManager in the rig hierarchy.");
    }

    /// <summary>
    ///     Looks up the neko/bowl references and moves the neko when a plane is locked.
    /// </summary>
    private void Update()
    {
        if (!_neko)
        {
            var nekoCandidate = GameObject.FindGameObjectWithTag("Neko");
            if (nekoCandidate)
            {
                CacheNeko(nekoCandidate);
                if (loggingEnabled) Debug.Log($"{LoggingPrefix} Cached neko '{_neko.name}'.");
            }
        }

        if (!_bowl)
        {
            _bowl = GameObject.FindGameObjectWithTag("Bowl");
            if (_bowl && loggingEnabled) Debug.Log($"{LoggingPrefix} Cached bowl '{_bowl.name}'.");
        }

        if (!planeManager || !_neko || !_bowl) return;

        if (!_planeLocked) TryLockPlane();

        if (!_planeLocked) return;

        MoveNekoTowardBowl();
    }

    /// <summary>
    ///     Registers to plane change notifications.
    /// </summary>
    private void OnEnable()
    {
        if (!planeManager)
        {
            Debug.LogError($"{LoggingPrefix} ARPlaneManager reference missing; disabling behaviour.");
            enabled = false;
            return;
        }

        planeManager.trackablesChanged.AddListener(OnPlanesChanged);
    }

    /// <summary>
    ///     Deregisters plane change notifications when disabled.
    /// </summary>
    private void OnDisable()
    {
        if (!planeManager) return;


        planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
    }

    /// <summary>
    ///     Stores the neko reference and its optional NavMeshAgent component.
    /// </summary>
    /// <param name="nekoObj">Scene object tagged as the neko.</param>
    private void CacheNeko(GameObject nekoObj)
    {
        _neko = nekoObj;
        _nekoAgent = _neko.GetComponent<NavMeshAgent>();
        if (_nekoAgent && loggingEnabled) Debug.Log($"{LoggingPrefix} Found NavMeshAgent on neko.");
    }

    /// <summary>
    ///     Attempts to relock onto a plane whenever tracking data changes.
    /// </summary>
    /// <param name="_">Unused event payload.</param>
    private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> _)
    {
        if (_planeLocked) return;

        TryLockPlane();
    }

    /// <summary>
    ///     Picks the closest plane to the neko and stores it for movement logic.
    /// </summary>
    private void TryLockPlane()
    {
        _activePlane = SelectPlaneNearNeko();
        _planeLocked = _activePlane != null;

        if (!_planeLocked)
        {
            if (loggingEnabled) Debug.Log($"{LoggingPrefix} No tracked plane near the neko yet.");

            return;
        }

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Locked to plane '{_activePlane.trackableId}'.");
    }

    /// <summary>
    ///     Retrieves the currently tracked plane whose surface is closest to the neko position.
    /// </summary>
    /// <returns>Plane instance or null if nothing suitable exists.</returns>
    private ARPlane SelectPlaneNearNeko()
    {
        if (!_neko || !planeManager) return null;

        ARPlane closestPlane = null;
        var closestDistance = float.MaxValue;

        foreach (var plane in planeManager.trackables)
        {
            if (!plane || plane.trackingState != TrackingState.Tracking) continue;

            var distance = Mathf.Abs(Vector3.Dot(
                plane.transform.up,
                _neko.transform.position - plane.transform.position));

            if (distance >= closestDistance) continue;

            closestDistance = distance;
            closestPlane = plane;
        }

        return closestPlane;
    }

    /// <summary>
    ///     Projects the neko and bowl onto the locked plane and advances toward the bowl.
    /// </summary>
    private void MoveNekoTowardBowl()
    {
        if (!_activePlane || _activePlane.trackingState != TrackingState.Tracking)
        {
            _planeLocked = false;
            _activePlane = null;
            if (loggingEnabled) Debug.LogWarning($"{LoggingPrefix} Lost plane tracking; relocking next frame.");

            return;
        }

        var plane = new Plane(_activePlane.transform.up, _activePlane.transform.position);
        var nekoProjected = ProjectOntoPlane(_neko.transform.position, plane);
        var bowlProjected = ProjectOntoPlane(_bowl.transform.position, plane);

        if ((nekoProjected - bowlProjected).sqrMagnitude <= stopDistanceMeters * stopDistanceMeters)
        {
            if (_nekoAgent && _nekoAgent.isActiveAndEnabled && _nekoAgent.isOnNavMesh)
                _nekoAgent.ResetPath();
            else if (loggingEnabled) Debug.Log($"{LoggingPrefix} Neko reached bowl (direct movement).");

            return;
        }

        if (preferNavMeshIfAvailable && _nekoAgent && _nekoAgent.isActiveAndEnabled && _nekoAgent.isOnNavMesh)
        {
            if (!_nekoAgent.hasPath || _nekoAgent.destination != bowlProjected)
            {
                _nekoAgent.SetDestination(bowlProjected);
                if (loggingEnabled) Debug.Log($"{LoggingPrefix} NavMesh destination set to bowl projection.");
            }

            return;
        }

        _neko.transform.position = Vector3.MoveTowards(
            nekoProjected,
            bowlProjected,
            moveSpeedMetersPerSecond * Time.deltaTime);

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Moving neko directly toward bowl.");
    }

    /// <summary>
    ///     Returns the projection of a world point onto a supplied plane.
    /// </summary>
    /// <param name="point">World position.</param>
    /// <param name="plane">Plane definition.</param>
    /// <returns>Projected world position.</returns>
    private static Vector3 ProjectOntoPlane(Vector3 point, Plane plane)
    {
        var distance = plane.GetDistanceToPoint(point);
        return point - plane.normal * distance;
    }
}