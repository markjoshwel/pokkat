/*
 * author: mark joshwel
 * date: 18/12/2025
 * description: central logic coordinator managing neko spawning, multi-image tracking, and ground stabilisation
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
        NekoWaitingForNavMesh,
        Ok
    }

    /// <summary>
    ///     tracks a spawned neko's state independent of unity's trackable id system
    /// </summary>
    public class TrackedNekoInstance
    {
        /// <summary>
        ///     last known stable position (spawn point or landing point)
        /// </summary>
        public Vector3 AnchorPosition;

        /// <summary>
        ///     the neko entity component
        /// </summary>
        public AREntityNeko Entity;

        /// <summary>
        ///     the gameobject (for clean-up)
        /// </summary>
        public GameObject GameObject;

        /// <summary>
        ///     true if currently following the tracked image
        /// </summary>
        public bool IsFollowing;

        /// <summary>
        ///     true if landed on a plane
        /// </summary>
        public bool IsGrounded;
    }

    /// <summary>
    ///     central coordinator managing neko spawning via image tracking and plane interactions
    ///     scene singleton (does not persist across scenes) for easy prefab access without
    ///     dependency injection/inspector assignment
    /// </summary>
    public class CoreGameplay : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Dependencies")]
        [HelpBox("Assign all required AR detection and handling components here.", HelpBoxMessageType.Info)]
        [Tooltip("ar image tracking events")]
        [SerializeField]
        private ImageHandling imageHandling;

        [Tooltip("ar plane detection and spawning")] [SerializeField]
        private PlaneHandling planeHandling;

        [Tooltip("persistent game statistics")] [SerializeField]
        private Statskeeper statskeeper;

        [Tooltip("player/AR camera for spawn orientation")] [SerializeField]
        private Camera playerCamera;

        [Header("Prefabs")] [Tooltip("main neko on first image detection")] [SerializeField]
        private GameObject mainNekoPrefab;

        [Tooltip("friend neko on subsequent detections")] [SerializeField]
        private GameObject friendNekoPrefab;

        [Tooltip("bowl when tapping on plane")] [SerializeField]
        private GameObject bowlPrefab;

        [Header("Spawn Settings")] [Tooltip("maximum concurrent nekos")] [SerializeField]
        private int maxActiveNekos = 3;

        [Tooltip("minimum distance in metres for new spawn")] [SerializeField]
        private float multiImageDistanceThreshold = 0.25f;

        [Tooltip("seconds between neko spawns")] [SerializeField]
        private float spawnCooldown = 1.0f;

        #endregion

        #region Private Fields

        /// <summary>
        ///     all tracked neko instances (max 3)
        /// </summary>
        private readonly List<TrackedNekoInstance> _trackedNekos = new();

        /// <summary>
        ///     the currently active bowl instance (singleton - only one allowed)
        /// </summary>
        private AREntityBowl _activeBowl;

        /// <summary>
        ///     last spawn time for cooldown check
        /// </summary>
        private float _lastSpawnTime = -999f;

        /// <summary>
        ///     the currently active tracked image reference
        /// </summary>
        private ARTrackedImage _activeImage;

        /// <summary>
        ///     whether the main neko has been spawned
        /// </summary>
        private bool _mainNekoSpawned;

        /// <summary>
        ///     whether the main neko is waiting for navmesh
        /// </summary>
        private bool _mainNekoWaitingForNavMesh;

        #endregion

        #region Public Properties

        /// <summary>
        ///     scene singleton instance for prefab access (does not persist across scenes)
        /// </summary>
        public static CoreGameplay instance { get; private set; }

        /// <summary>
        ///     current game state (waiting for plane/tracker, or ready)
        /// </summary>
        public CoreGameplayState gameState { get; private set; }

        /// <summary>
        ///     public accessor for PlaneHandling (for AREntityNeko.Fall)
        /// </summary>
        public PlaneHandling planes => planeHandling;

        /// <summary>
        ///     public accessor for the active bowl (for neko behaviour)
        /// </summary>
        public AREntityBowl activeBowl => _activeBowl;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // singleton setup
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            Setup_Dependencies();
            Logkat.Out("CoreGameplay: Awake/Setup OK");
        }

        private void Start()
        {
            Configure_SubscribeToEvents();
            Logkat.Out("CoreGameplay: Start/Configure OK");
            gameState = CoreGameplayState.WaitingForAnything;
        }

        private void Update()
        {
            UpdateFollowingNeko();
            TryTriggerEarlyFall();
        }

        #endregion

        #region Setup

        private void Setup_Dependencies()
        {
            if (!imageHandling) Logkat.Panic("CoreGameplay requires an ImageHandling reference.");
            if (!planeHandling) Logkat.Panic("CoreGameplay requires a PlaneHandling reference.");
            if (!statskeeper) Logkat.Panic("CoreGameplay requires a Statskeeper reference.");
            if (!playerCamera) Logkat.Panic("CoreGameplay requires an AR Camera reference.");
        }

        private void Configure_SubscribeToEvents()
        {
            imageHandling.OnImageDetected += OnImageDetected;
            imageHandling.OnImageLost += OnImageLost;
            planeHandling.OnPlaneReady += OnPlaneReady;
            planeHandling.OnPlaneInteraction += OnPlaneInteraction;
            Logkat.Out("CoreGameplay: Event Subscription OK");
        }

        #endregion

        #region Following Neko Update

        /// <summary>
        ///     update the neko that is following the tracked image
        /// </summary>
        private void UpdateFollowingNeko()
        {
            // find the following neko
            TrackedNekoInstance followingNeko = null;
            foreach (var neko in _trackedNekos)
                if (neko.IsFollowing)
                {
                    followingNeko = neko;
                    break;
                }

            // no neko is following - nothing to do
            if (followingNeko == null) return;

            // no active image reference
            if (_activeImage == null)
            {
                Logkat.Dev("CoreGameplay: followingNeko exists but _activeImage is null, triggering fall");
                TriggerNekoFall(followingNeko);
                return;
            }

            // check tracking state
            var trackingState = _activeImage.trackingState;
            Logkat.Dev(
                $"CoreGameplay: UpdateFollowingNeko - state={trackingState}, imagePos={_activeImage.transform.position}, nekoPos={followingNeko.Entity.transform.position}");

            if (trackingState == TrackingState.Tracking)
            {
                // sync neko position to image
                followingNeko.Entity.transform.position = _activeImage.transform.position;
                followingNeko.AnchorPosition = _activeImage.transform.position;
            }
            else
            {
                // tracking lost or limited - trigger fall
                Logkat.Dev($"CoreGameplay: tracking state is {trackingState}, triggering fall");
                TriggerNekoFall(followingNeko);
            }
        }

        /// <summary>
        ///     triggers fall for a following neko and marks it as grounded
        /// </summary>
        private void TriggerNekoFall(TrackedNekoInstance neko)
        {
            Logkat.Dev($"CoreGameplay: TriggerNekoFall called, pos={neko.Entity.transform.position}");
            neko.IsFollowing = false;
            neko.Entity.StopFollowing();
            neko.Entity.Fall(() =>
            {
                // callback when fall completes
                neko.IsGrounded = true;
                neko.AnchorPosition = neko.Entity.transform.position;
                Logkat.Dev($"CoreGameplay: neko landed, anchorPosition={neko.AnchorPosition}");
            });
        }

        #endregion

        #region Event Handlers

        /// <summary>
        ///     callback for when sufficient plane area has been detected
        /// </summary>
        private void OnPlaneReady(ARPlane plane)
        {
            Logkat.Out("CoreGameplay: plane ready received");

            switch (gameState)
            {
                case CoreGameplayState.WaitingForAnything:
                    gameState = CoreGameplayState.HasPlaneWaitingForTracker;
                    break;
                case CoreGameplayState.HasTrackerWaitingForPlane:
                    gameState = CoreGameplayState.Ok;
                    break;
                case CoreGameplayState.HasPlaneWaitingForTracker:
                case CoreGameplayState.NekoWaitingForNavMesh:
                case CoreGameplayState.Ok:
                    break;
                default:
                    Logkat.Panic("unreachable game state");
                    break;
            }
            
            // trigger early fall for any following neko now that plane is available
            // (handles case where tracker was scanned at angle, spawning neko mid-air)
            TryTriggerEarlyFall();
        }
        
        /// <summary>
        ///     triggers fall for following neko if plane is available and neko is above plane.
        ///     called when plane becomes ready or periodically from Update
        /// </summary>
        private void TryTriggerEarlyFall()
        {
            if (!planeHandling.isReady) return;
            
            // find following neko
            TrackedNekoInstance followingNeko = null;
            foreach (var neko in _trackedNekos)
                if (neko.IsFollowing)
                {
                    followingNeko = neko;
                    break;
                }
            
            if (followingNeko == null) return;
            
            // check if neko is significantly above the nearest plane (spawned mid-air)
            if (!planeHandling.TryProjectToPlane(followingNeko.Entity.transform.position, out var projectedPos))
                return;
            
            var heightAbovePlane = followingNeko.Entity.transform.position.y - projectedPos.y;
            
            // if neko is more than 5cm above the plane, trigger early fall
            // (small threshold to avoid triggering for minor tracking jitter)
            if (heightAbovePlane > 0.05f)
            {
                Logkat.Out($"CoreGameplay: early fall triggered, height above plane = {heightAbovePlane:F3}m");
                TriggerNekoFall(followingNeko);
            }
        }

        /// <summary>
        ///     callback for plane touch interactions - spawns bowl at touch location
        /// </summary>
        private void OnPlaneInteraction(HandledPlaneInteraction interaction)
        {
            if (!bowlPrefab)
            {
                Logkat.Warn("CoreGameplay: no bowlPrefab assigned, skipping spawn");
                return;
            }

            if (_activeBowl)
            {
                Logkat.Out("CoreGameplay: destroying existing bowl for replacement");
                Destroy(_activeBowl.gameObject);
                _activeBowl = null;
            }

            var spawned = planeHandling.SpawnClosest(bowlPrefab, interaction.Position, playerCamera);
            if (!spawned) return;

            _activeBowl = spawned.GetComponent<AREntityBowl>();
            if (!_activeBowl)
            {
                Logkat.Warn("CoreGameplay: spawned bowl has no AREntityBowl component");
                Destroy(spawned);
                return;
            }

            Logkat.Out($"CoreGameplay: spawned bowl at {interaction.Position}");
            // note: AREntityNeko listens for OnBowlSpawned directly
        }

        /// <summary>
        ///     callback for tracked image detection - handles multispawn logic
        /// </summary>
        private void OnImageDetected(HandledTrackedImage tracked)
        {
            Logkat.Dev(
                $"CoreGameplay: OnImageDetected - pos={tracked.Image.transform.position}, id={tracked.Id}");

            // update game state
            switch (gameState)
            {
                case CoreGameplayState.WaitingForAnything:
                    gameState = CoreGameplayState.HasTrackerWaitingForPlane;
                    break;
                case CoreGameplayState.HasPlaneWaitingForTracker:
                    gameState = CoreGameplayState.Ok;
                    break;
                case CoreGameplayState.HasTrackerWaitingForPlane:
                case CoreGameplayState.NekoWaitingForNavMesh:
                case CoreGameplayState.Ok:
                    break;
                default:
                    Logkat.Panic("unreachable game state");
                    break;
            }

            // store active image reference
            _activeImage = tracked.Image;
            var currentPos = tracked.Image.transform.position;

            // find currently following neko (if any)
            TrackedNekoInstance followingNeko = null;
            foreach (var neko in _trackedNekos)
                if (neko.IsFollowing)
                {
                    followingNeko = neko;
                    break;
                }

            Logkat.Dev(
                $"CoreGameplay: followingNeko={followingNeko != null}, totalNekos={_trackedNekos.Count}");

            // case 1: a neko is currently following
            if (followingNeko != null)
            {
                // check if image position jumped (different physical card)
                var distanceFromFollowing = Vector3.Distance(currentPos, followingNeko.AnchorPosition);
                Logkat.Dev(
                    $"CoreGameplay: distanceFromFollowing={distanceFromFollowing:F3}, threshold={multiImageDistanceThreshold}");

                if (distanceFromFollowing > multiImageDistanceThreshold)
                {
                    // image jumped! ground the current neko and spawn new one
                    Logkat.Dev("CoreGameplay: position jump detected, grounding current neko and spawning new");
                    TriggerNekoFall(followingNeko);
                    TrySpawnNewNeko(currentPos);
                }

                // else: same position, neko continues following (handled in Update)
                return;
            }

            // case 2: no neko is following; check if we should spawn
            // check distance to all grounded nekos using their CURRENT position (not anchor)
            // because nekos drift with the plane, we need to compare against where they actually are
            var isFarFromAll = true;
            foreach (var neko in _trackedNekos)
            {
                if (!neko.IsGrounded) continue;
                if (neko.Entity == null) continue;

                // use current transform position, not anchor, because neko drifts with plane
                var nekoWorldPos = neko.Entity.transform.position;
                var distance = Vector3.Distance(currentPos, nekoWorldPos);

                Logkat.Dev($"CoreGameplay: distance to grounded neko={distance:F3}");
                if (distance < multiImageDistanceThreshold)
                {
                    isFarFromAll = false;
                    break;
                }
            }

            if (isFarFromAll)
            {
                Logkat.Dev("CoreGameplay: far from all grounded nekos, spawning new");
                TrySpawnNewNeko(currentPos);
            }

            Logkat.Dev("CoreGameplay: near existing grounded neko, not spawning");
        }

        #endregion

        #region Neko Spawning

        /// <summary>
        ///     attempts to spawn a new neko at the given position
        /// </summary>
        private void TrySpawnNewNeko(Vector3 position)
        {
            // clean up any null entries (destroyed nekos)
            _trackedNekos.RemoveAll(n => n.Entity == null || n.GameObject == null);

            // enforce spawn cooldown
            if (Time.time - _lastSpawnTime < spawnCooldown)
            {
                Logkat.Dev($"CoreGameplay: spawn cooldown active ({Time.time - _lastSpawnTime:F2}s < {spawnCooldown}s)");
                return;
            }

            // enforce max neko limit
            if (_trackedNekos.Count >= maxActiveNekos)
            {
                Logkat.Dev(
                    $"CoreGameplay: max nekos ({maxActiveNekos}) reached, count={_trackedNekos.Count}, skipping spawn");
                return;
            }

            // determine prefab
            var isMainNeko = !_mainNekoSpawned;
            var prefab = isMainNeko ? mainNekoPrefab : friendNekoPrefab;

            if (!prefab)
            {
                Logkat.Warn("CoreGameplay: required prefab not assigned");
                return;
            }

            Logkat.Dev(
                $"CoreGameplay: spawning neko at {position}, isMain={isMainNeko}, currentCount={_trackedNekos.Count}");

            // calculate rotation to face the camera
            var toCamera = playerCamera.transform.position - position;
            toCamera.y = 0; // keep upright, only rotate on Y axis
            var rotation = toCamera.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toCamera)
                : Quaternion.identity;

            // spawn at image position facing camera (will follow immediately)
            var spawned = Instantiate(prefab, position, rotation);
            var nekoEntity = spawned.GetComponentInChildren<AREntityNeko>();

            if (!nekoEntity)
            {
                Logkat.Warn("CoreGameplay: spawned prefab has no AREntityNeko component");
                Destroy(spawned);
                return;
            }

            // update spawn time
            _lastSpawnTime = Time.time;

            // create tracked instance
            var nekoInstance = new TrackedNekoInstance
            {
                Entity = nekoEntity,
                GameObject = spawned,
                AnchorPosition = position,
                IsFollowing = true,
                IsGrounded = false
            };

            _trackedNekos.Add(nekoInstance);
            nekoEntity.StartFollowing();

            if (isMainNeko)
            {
                _mainNekoSpawned = true;
                Logkat.Out("CoreGameplay: spawned MAIN neko");
            }
            else
            {
                Logkat.Out("CoreGameplay: spawned FRIEND neko");
            }
        }

        #endregion

        #region NavMesh State

        /// <summary>
        ///     called by main neko when it is waiting for navmesh
        /// </summary>
        public void NotifyMainNekoWaitingForNavMesh()
        {
            _mainNekoWaitingForNavMesh = true;
            UpdateNavMeshGameState();
        }

        /// <summary>
        ///     called by main neko when it has navmesh ready
        /// </summary>
        public void NotifyMainNekoHasNavMesh()
        {
            _mainNekoWaitingForNavMesh = false;
            UpdateNavMeshGameState();
        }

        /// <summary>
        ///     updates game state based on navmesh waiting status.
        ///     only transitions to NekoWaitingForNavMesh/Ok after we have both plane and tracker
        /// </summary>
        private void UpdateNavMeshGameState()
        {
            // only handle navmesh states once we're past the initial waiting states
            // (i.e., we have both plane and tracker)
            switch (gameState)
            {
                case CoreGameplayState.WaitingForAnything:
                case CoreGameplayState.HasPlaneWaitingForTracker:
                case CoreGameplayState.HasTrackerWaitingForPlane:
                    // not ready yet - navmesh state doesn't apply
                    return;
                case CoreGameplayState.NekoWaitingForNavMesh:
                case CoreGameplayState.Ok:
                    // transition based on navmesh status
                    gameState = _mainNekoWaitingForNavMesh
                        ? CoreGameplayState.NekoWaitingForNavMesh
                        : CoreGameplayState.Ok;
                    break;
                default:
                    Logkat.Panic("unreachable game state");
                    break;
            }
        }

        /// <summary>
        ///     callback for tracked image loss
        /// </summary>
        private void OnImageLost(HandledTrackedImage tracked)
        {
            Logkat.Dev($"CoreGameplay: OnImageLost - id={tracked.Id}");

            // clear active image if it matches
            if (_activeImage == tracked.Image) _activeImage = null;

            // find and ground any following neko
            foreach (var neko in _trackedNekos.Where(neko => neko.IsFollowing))
            {
                Logkat.Dev("CoreGameplay: grounding following neko due to image loss");
                TriggerNekoFall(neko);
                break;
            }
        }

        #endregion

        #region Audio Stubs

        /// <summary>
        ///     plays bowl placement sound effect
        /// </summary>
        public void PlayBowlPlaceSound()
        {
            Logkat.Warn("CoreGameplay: PlayBowlPlaceSound not implemented yet");
        }

        /// <summary>
        ///     plays bowl consumption sound effect
        /// </summary>
        public void PlayBowlConsumeSound()
        {
            Logkat.Warn("CoreGameplay: PlayBowlConsumeSound not implemented yet");
        }

        /// <summary>
        ///     plays neko footstep sound effect (called on each walk step)
        /// </summary>
        public void PlayStepSound()
        {
            Logkat.Warn("CoreGameplay: PlayStepSound not implemented yet");
        }

        /// <summary>
        ///     plays neko meow sound effect (called on petting, playing with friend)
        /// </summary>
        public void PlayMeowSound()
        {
            Logkat.Warn("CoreGameplay: PlayMeowSound not implemented yet");
        }

        /// <summary>
        ///     plays neko eating sound effect (called during eating animation)
        /// </summary>
        public void PlayEatingSound()
        {
            Logkat.Warn("CoreGameplay: PlayEatingSound not implemented yet");
        }

        /// <summary>
        ///     plays neko jump sound effect (called on jump)
        /// </summary>
        public void PlayJumpSound()
        {
            Logkat.Warn("CoreGameplay: PlayJumpSound not implemented yet");
        }

        #endregion
    }
}
