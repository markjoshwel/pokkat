/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: neko character with texture management, blinking, and procedural animations
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace PokkatCore
{
    /// <summary>
    ///     finite state machine states for neko behaviour
    /// </summary>
    public enum NekoState
    {
        Idle,
        Roaming,
        MovingToBowl,
        Eating,
        PlayingWithFriend,
        BeingPetted
    }

    /// <summary>
    ///     wrapper struct for neko interaction data emitted by OnNekoInteraction
    /// </summary>
    public struct HandledNekoInteraction
    {
        /// <summary>
        ///     the neko that was interacted with
        /// </summary>
        public AREntityNeko Neko;

        /// <summary>
        ///     world position where the touch ray hit the neko
        /// </summary>
        public Vector3 Position;
    }

    /// <summary>
    ///     neko entity with texture loading, periodic blinking, and procedural movement
    /// </summary>
    public sealed class AREntityNeko : MonoBehaviour
    {
        [Header("Texture Settings")] [Tooltip("texture id to load (0-44)")] [SerializeField]
        private int textureId;

        [Tooltip("periodic blinking")] [SerializeField]
        private bool enableBlinking = true;

        [Tooltip("seconds between blinks")] [SerializeField]
        private float blinkInterval = 3f;

        [Tooltip("blink duration in seconds")] [SerializeField]
        private float blinkDuration = 0.15f;

        [Header("Movement Settings")] [Tooltip("metres per second")] [SerializeField]
        private float walkSpeed = 0.5f;

        [Tooltip("seconds per step")] [SerializeField]
        private float walkStepDuration = 0.08f;

        [Tooltip("step distance multiplier")] [SerializeField]
        private float walkStepDistanceMultiplier = 0.5f;

        [Tooltip("tilt angle in degrees")] [SerializeField]
        private float walkTiltAngle = 15f;

        [Tooltip("jump height in metres")] [SerializeField]
        private float jumpHeight = 0.15f;

        [Tooltip("jump duration in seconds")] [SerializeField]
        private float jumpDuration = 0.3f;

        [Tooltip("bounce factor for jump easing (0=sine, 1=full bounce)")] [SerializeField]
        private float jumpBounceFactor = 0.3f;

        [Tooltip("fall speed in metres per second")] [SerializeField]
        private float fallSpeed = 2f;

        [Header("Ground Stabilisation")] [Tooltip("project to nearest plane when grounded")] [SerializeField]
        private bool enableGroundStabilisation = true;

        [Tooltip("seconds between checks")] [SerializeField]
        private float stabilisationInterval = 0.5f;

        [Tooltip("minimum drift to stabilise")] [SerializeField]
        private float stabilisationThreshold = 0.02f;

        [Header("Behaviour Settings")] [Tooltip("roaming radius in metres")] [SerializeField]
        private float roamRadius = 0.5f;

        [Tooltip("minimum idle wait in seconds")] [SerializeField]
        private float idleWaitMin = 2f;

        [Tooltip("maximum idle wait in seconds")] [SerializeField]
        private float idleWaitMax = 5f;

        [Header("Play Settings")] [Tooltip("delay before friend jumps (seconds)")] [SerializeField]
        private float friendJumpDelay = 0.5f;

        [Tooltip("number of jumps when playing")] [SerializeField]
        private int playJumpCount = 3;

        [Header("Eating Settings")] [Tooltip("eating duration in seconds")] [SerializeField]
        private float eatingDuration = 5f;

        /// <summary>
        ///     cached renderers for texture application
        /// </summary>
        private readonly List<Renderer> _renderers = new();

        // /// <summary>
        // ///     original y rotation to restore after animations
        // /// </summary>
        // private float _baseYRotation;

        /// <summary>
        ///     coroutine handle for blinking
        /// </summary>
        private Coroutine _blinkCoroutine;

        /// <summary>
        ///     current FSM state
        /// </summary>
        private NekoState _currentState = NekoState.Idle;

        /// <summary>
        ///     eyes closed texture for this neko
        /// </summary>
        private Texture _eyesClosedTexture;

        /// <summary>
        ///     eyes open texture for this neko
        /// </summary>
        private Texture _eyesOpenTexture;

        /// <summary>
        ///     whether this neko is currently following a tracked image
        /// </summary>
        private bool _isFollowing;

        /// <summary>
        ///     whether this neko has landed on a plane
        /// </summary>
        private bool _isGrounded;

        /// <summary>
        ///     coroutine handle for current movement animation
        /// </summary>
        private Coroutine _movementCoroutine;

        /// <summary>
        ///     pending bowl notification (queued if in non-interruptible state)
        /// </summary>
        private bool _pendingBowlNotification;

        /// <summary>
        ///     pending friend to play with (queued if in non-interruptible state)
        /// </summary>
        private AREntityNeko _pendingFriend;

        /// <summary>
        ///     timer for ground stabilisation interval
        /// </summary>
        private float _stabilisationTimer;

        /// <summary>
        ///     coroutine handle for current state behaviour
        /// </summary>
        private Coroutine _stateCoroutine;

        /// <summary>
        ///     public accessor for current state
        /// </summary>
        public NekoState currentState => _currentState;

        private void Awake()
        {
            CacheRenderers();

            if (CompareTag("NekoFriend"))
            {
                Logkat.Out("AREntityNeko: i am a friend! (randomising texture)");
                RandomizeTextureId();
            }
            else
            {
                if (textureId is < 0 or > 44)
                {
                    Logkat.Warn("AREntityNeko: textureId out of range (0-44), defaulting to 0");
                    textureId = 0;
                }
            }

            LoadTextures(textureId);
            Logkat.Out("AREntityNeko: Awake/Setup OK");
        }

        private void Start()
        {
            ApplyTexture(_eyesOpenTexture);
            // _baseYRotation = transform.eulerAngles.y;

            if (enableBlinking)
                _blinkCoroutine = StartCoroutine(BlinkRoutine());

            // subscribe to AR object spawn events for direct awareness
            AREntityBowl.OnBowlSpawned += OnBowlSpawnedHandler;
            OnNekoSpawned += OnNekoSpawnedHandler;

            // subscribe to own interaction event for petting
            OnNekoInteraction += OnNekoInteractionPetted;

            Logkat.Out("AREntityNeko: Start/Configure OK");

            // broadcast own spawn event for other AR objects to detect
            OnNekoSpawned?.Invoke(this);
            Logkat.Out("AREntityNeko: broadcasted spawn event");
        }

        private void Update()
        {
            UpdateGroundStabilisation();
            UpdateStateMachine();
            UpdateTouchDetection();
        }

        private void OnDisable()
        {
            if (_blinkCoroutine != null)
            {
                StopCoroutine(_blinkCoroutine);
                _blinkCoroutine = null;
            }

            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
                _movementCoroutine = null;
            }

            if (_stateCoroutine != null)
            {
                StopCoroutine(_stateCoroutine);
                _stateCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            // unsubscribe from events
            AREntityBowl.OnBowlSpawned -= OnBowlSpawnedHandler;
            OnNekoSpawned -= OnNekoSpawnedHandler;
            OnNekoInteraction -= OnNekoInteractionPetted;

            // broadcast destroy event
            OnNekoDestroyed?.Invoke(this);
        }

        /// <summary>
        ///     static event fired when any neko is spawned (for direct AR object awareness)
        /// </summary>
        public static event Action<AREntityNeko> OnNekoSpawned;

        /// <summary>
        ///     static event fired when any neko is destroyed
        /// </summary>
        public static event Action<AREntityNeko> OnNekoDestroyed;

        /// <summary>
        ///     fired when this neko is tapped/clicked (for petting)
        /// </summary>
        public event Action<HandledNekoInteraction> OnNekoInteraction;

        /// <summary>
        ///     handles ground stabilisation (timer-based plane projection)
        /// </summary>
        private void UpdateGroundStabilisation()
        {
            // skip ground stabilisation if disabled, not grounded, or following
            if (!enableGroundStabilisation || !_isGrounded || _isFollowing) return;

            // timer-based stabilisation to avoid running every frame
            _stabilisationTimer -= Time.deltaTime;
            if (_stabilisationTimer > 0f) return;
            _stabilisationTimer = stabilisationInterval;

            // get CoreGameplay and planes reference
            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes) return;

            // project current position onto nearest plane
            if (!gameplay.planes.TryProjectToPlane(transform.position, out var projectedPos)) return;

            // only move if drift exceeds threshold (avoids micro-jitter)
            var drift = Vector3.Distance(transform.position, projectedPos);
            if (drift < stabilisationThreshold) return;

            // snap to projected position
            transform.position = projectedPos;
        }

        /// <summary>
        ///     handles FSM state updates (only when grounded and not following)
        /// </summary>
        private void UpdateStateMachine()
        {
            // skip FSM if not grounded or still following
            if (!_isGrounded || _isFollowing) return;

            // FSM only runs when idle (other states are coroutine-driven)
            if (_currentState != NekoState.Idle) return;

            // check for pending actions from notifications
            if (_pendingFriend)
            {
                var friend = _pendingFriend;
                _pendingFriend = null;
                TransitionToState(NekoState.PlayingWithFriend, friend);
                return;
            }

            if (_pendingBowlNotification)
            {
                _pendingBowlNotification = false;
                var gameplay = CoreGameplay.instance;
                if (gameplay && gameplay.activeBowl && gameplay.activeBowl.isFull && CompareTag("NekoMain"))
                {
                    TransitionToState(NekoState.MovingToBowl);
                    return;
                }
            }

            // start idle behaviour if not already running
            _stateCoroutine ??= StartCoroutine(IdleStateRoutine());
        }

        /// <summary>
        ///     caches all renderer components in children
        /// </summary>
        private void CacheRenderers()
        {
            _renderers.Clear();
            _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>
        ///     loads texture pair from Resources/NekoTextures for the given id
        /// </summary>
        private void LoadTextures(int id)
        {
            var idString = id.ToString("D2");
            var eyesOpenPath = $"NekoTextures/Tex_Neko_Body_{idString}";
            var eyesClosedPath = $"NekoTextures/Tex_Neko_Body_{idString}_eyeclose";

            _eyesOpenTexture = Resources.Load<Texture>(eyesOpenPath);
            _eyesClosedTexture = Resources.Load<Texture>(eyesClosedPath);

            if (!_eyesOpenTexture || !_eyesClosedTexture)
                Logkat.Warn($"AREntityNeko: failed to load textures for id {id}");
        }

        /// <summary>
        ///     applies a texture to all cached renderers
        /// </summary>
        private void ApplyTexture(Texture texture)
        {
            if (!texture) return;
            foreach (var rdr in _renderers)
                rdr.material.mainTexture = texture;
        }

        /// <summary>
        ///     changes the neko's texture to the specified id
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void SetTextureId(int id)
        {
            if (id is < 0 or > 44) Logkat.Panic("AREntityNeko: textureId out of range (0-44)");
            textureId = id;
            LoadTextures(id);
            ApplyTexture(_eyesOpenTexture);
            Logkat.Out($"AREntityNeko: set texture to ID {id}");
        }

        /// <summary>
        ///     changes the neko's texture to a random id (0-44)
        /// </summary>
        private void RandomizeTextureId()
        {
            SetTextureId(Random.Range(0, 45));
        }

        /// <summary>
        ///     performs a single blink (eyes close then open)
        /// </summary>
        private void Blink()
        {
            if (_eyesClosedTexture && _eyesOpenTexture)
                StartCoroutine(BlinkOnce());
        }

        /// <summary>
        ///     coroutine for a single blink
        /// </summary>
        private IEnumerator BlinkOnce()
        {
            ApplyTexture(_eyesClosedTexture);
            yield return new WaitForSeconds(blinkDuration);
            ApplyTexture(_eyesOpenTexture);
        }

        /// <summary>
        ///     coroutine that blinks periodically while enabled
        /// </summary>
        private IEnumerator BlinkRoutine()
        {
            while (enableBlinking)
            {
                yield return new WaitForSeconds(blinkInterval);
                yield return BlinkOnce();
            }
        }

        /// <summary>
        ///     starts following mode (called by CoreGameplay when spawned on tracked image)
        /// </summary>
        public void StartFollowing()
        {
            _isFollowing = true;
            _isGrounded = false;
            // unparent so CoreGameplay can move us freely
            transform.SetParent(null);
            Logkat.Out("AREntityNeko: started following tracked image");
        }

        /// <summary>
        ///     stops following mode and triggers fall to nearest plane
        /// </summary>
        public void StopFollowing()
        {
            _isFollowing = false;
            Logkat.Out("AREntityNeko: stopped following");
        }

        /// <summary>
        ///     falls to the nearest detected plane with completion callback
        /// </summary>
        /// <param name="onComplete">callback invoked when fall completes</param>
        public void Fall(Action onComplete = null)
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(FallRoutine(onComplete));
        }

        /// <summary>
        ///     walks toward a target position with choppy stop-motion style animation
        /// </summary>
        public void WalkTo(Vector3 targetPosition)
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(WalkRoutine(targetPosition));
        }

        /// <summary>
        ///     performs a single jump in place
        /// </summary>
        internal void Jump()
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(JumpRoutine());
        }

        /// <summary>
        ///     coroutine that falls to the nearest plane
        /// </summary>
        /// <param name="onComplete">optional callback invoked when fall completes</param>
        private IEnumerator FallRoutine(Action onComplete)
        {
            // Logkat.Out($"AREntityNeko: [Debug] FallRoutine started, currentPos={transform.position}");

            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes)
            {
                Logkat.Warn("AREntityNeko: no CoreGameplay instance, cannot fall properly");
                onComplete?.Invoke();
                yield break;
            }

            // project current position onto nearest plane surface
            Vector3 targetPosition;
            if (gameplay.planes.TryProjectToPlane(transform.position, out var projectedPos))
            {
                targetPosition = projectedPos;
                // Logkat.Out($"AREntityNeko: [Debug] projected to plane, targetPos={targetPosition}");
            }
            else
            {
                // fallback: no plane available, snap to y=0
                // Logkat.Warn("AREntityNeko: [Debug] no plane found, falling to y=0");
                targetPosition = transform.position;
                targetPosition.y = 0f;
            }

            // fall toward target position
            // Logkat.Out($"AREntityNeko: [Debug] falling from {transform.position} to {targetPosition}");
            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                var fallStep = fallSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallStep);
                yield return null;
            }

            // snap to exact target and mark as grounded
            transform.position = targetPosition;
            _isGrounded = true;

            // try to snap to nearest navmesh point for roaming/movement
            if (NavMesh.SamplePosition(transform.position, out var navHit, 2f, NavMesh.AllAreas))
            {
                transform.position = navHit.position;
                Logkat.Out($"AREntityNeko: snapped to navmesh at {navHit.position}");
                CoreGameplay.instance?.NotifyNekoNavMeshReady();
            }
            else
            {
                Logkat.Warn($"AREntityNeko: no navmesh nearby after landing at {transform.position}");
                CoreGameplay.instance?.NotifyNekoNavMeshFailed();
            }

            _movementCoroutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        ///     coroutine for choppy stop-motion walk animation
        /// </summary>
        private IEnumerator WalkRoutine(Vector3 targetPosition)
        {
            Logkat.Out("AREntityNeko: walking");

            var tiltLeft = true;

            while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
            {
                // face the target
                var direction = (targetPosition - transform.position).normalized;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.001f)
                {
                    var targetRotation = Quaternion.LookRotation(direction);

                    // add choppy tilt like a kid moving a lego figure
                    var tiltAngle = tiltLeft ? walkTiltAngle : -walkTiltAngle;
                    var tiltRotation = Quaternion.Euler(0, 0, tiltAngle);
                    transform.rotation = targetRotation * tiltRotation;
                }

                // move forward in discrete steps (choppy/stop-motion)
                var stepDistance = walkSpeed * walkStepDuration * walkStepDistanceMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, stepDistance);

                tiltLeft = !tiltLeft;
                yield return new WaitForSeconds(walkStepDuration);
            }

            // reset rotation to upright
            var finalRotation = transform.eulerAngles;
            finalRotation.z = 0;
            transform.eulerAngles = finalRotation;

            Logkat.Out("AREntityNeko: finished walking");
            _movementCoroutine = null;
        }

        /// <summary>
        ///     coroutine for a single jump with bounce easing
        /// </summary>
        private IEnumerator JumpRoutine()
        {
            Logkat.Out("AREntityNeko: jumping");

            var startPos = transform.position;
            var elapsed = 0f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / jumpDuration);

                // ease-out-back curve for bouncy feel
                var easedT = EaseOutBack(t, jumpBounceFactor);

                // parabolic arc using eased time (peaks at t=0.5)
                var heightOffset = 4f * easedT * (1f - easedT) * jumpHeight;
                transform.position = startPos + Vector3.up * Mathf.Max(0f, heightOffset);

                yield return null;
            }

            // ensure we land exactly where we started
            transform.position = startPos;

            Logkat.Out("AREntityNeko: finished jumping");
            _movementCoroutine = null;
        }

        /// <summary>
        ///     ease-out-back curve for bouncy animations
        /// </summary>
        /// <param name="t">normalised time (0-1)</param>
        /// <param name="bounceFactor">bounce intensity (0=none, 1=full)</param>
        private static float EaseOutBack(float t, float bounceFactor)
        {
            var c1 = 1.70158f * bounceFactor;
            var c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        #region FSM State Transitions

        /// <summary>
        ///     FSM: transitions to a new state, cancelling current state coroutine
        /// </summary>
        /// <param name="newState">the state to transition to</param>
        /// <param name="context">optional context object (e.g., friend neko for PlayingWithFriend)</param>
        private void TransitionToState(NekoState newState, object context = null)
        {
            // stop current state coroutine
            if (_stateCoroutine != null)
            {
                StopCoroutine(_stateCoroutine);
                _stateCoroutine = null;
            }

            // stop movement coroutine if transitioning out of a walking state
            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
                _movementCoroutine = null;

                // reset rotation to upright
                var rotation = transform.eulerAngles;
                rotation.z = 0;
                transform.eulerAngles = rotation;
            }

            var previousState = _currentState;
            _currentState = newState;
            Logkat.Out($"AREntityNeko: {previousState} -> {newState}");

            // start new state coroutine
            _stateCoroutine = newState switch
            {
                NekoState.Idle => StartCoroutine(IdleStateRoutine()),
                NekoState.Roaming => StartCoroutine(RoamingStateRoutine()),
                NekoState.MovingToBowl => StartCoroutine(MovingToBowlStateRoutine()),
                NekoState.Eating => StartCoroutine(EatingStateRoutine()),
                NekoState.PlayingWithFriend => StartCoroutine(SocialisingStateRoutine(context as AREntityNeko)),
                NekoState.BeingPetted => StartCoroutine(PettedStateRoutine()),
                _ => null
            };
        }

        #endregion

        #region Helper Methods

        /// <summary>
        ///     rotates to face a target position (Y-axis only)
        /// </summary>
        /// <param name="target">world position to look at</param>
        private void LookAt(Vector3 target)
        {
            var direction = target - transform.position;
            direction.y = 0;

            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        ///     per-frame touch detection for neko interaction
        /// </summary>
        private void UpdateTouchDetection()
        {
            // skip if no subscribers or not interactable
            if (OnNekoInteraction == null) return;
            if (!_isGrounded || _isFollowing) return;

            // try to get touch position
            if (!TryGetTouchPosition(out var touchPosition)) return;

            // raycast from camera through touch point
            var mainCamera = Camera.main;
            if (!mainCamera) return;

            var ray = mainCamera.ScreenPointToRay(touchPosition);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;

            // check if this neko was hit
            if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return;

            // fire the interaction event for subscribers
            var interaction = new HandledNekoInteraction
            {
                Neko = this,
                Position = hit.point
            };

            Logkat.Out($"AREntityNeko: interaction at {hit.point}");
            OnNekoInteraction.Invoke(interaction);
        }

        /// <summary>
        ///     attempts to read touch/mouse input using the new input system
        /// </summary>
        /// <param name="position">screen position of the touch or click</param>
        /// <returns>true if a valid touch/click was detected this frame</returns>
        private static bool TryGetTouchPosition(out Vector2 position)
        {
            // check for touchscreen input first (mobile AR)
            if (Touchscreen.current is { } touchscreen)
            {
                var touch = touchscreen.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    position = touch.position.ReadValue();
                    return true;
                }
            }

            // fallback to mouse input (editor testing)
            if (Mouse.current is { } mouse && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        ///     callback for neko interaction - handles petting via FSM
        /// </summary>
        private void OnNekoInteractionPetted(HandledNekoInteraction interaction)
        {
            Logkat.Out("AREntityNeko: i was petted!");

            // only transition to BeingPetted if in an interruptible state
            if (_currentState is NekoState.Idle or NekoState.Roaming)
                TransitionToState(NekoState.BeingPetted);
        }

        #endregion


        #region Direct AR Object Awareness (event handlers)

        /// <summary>
        ///     handler for when any bowl spawns in the scene (direct AR object awareness)
        /// </summary>
        /// <param name="bowl">the spawned bowl</param>
        private void OnBowlSpawnedHandler(AREntityBowl bowl)
        {
            // only main neko reacts to bowl spawns
            if (!CompareTag("NekoMain")) return;
            if (!_isGrounded) return;

            Logkat.Out($"AREntityNeko: detected bowl spawn at {bowl.transform.position}");

            // use existing notification logic
            StateNotifyBowlPlaced();
        }

        /// <summary>
        ///     handler for when any neko spawns in the scene (direct AR object awareness)
        /// </summary>
        /// <param name="otherNeko">the spawned neko</param>
        private void OnNekoSpawnedHandler(AREntityNeko otherNeko)
        {
            // ignore self
            if (otherNeko == this) return;

            // only main neko initiates play with friends
            if (!CompareTag("NekoMain")) return;
            if (!_isGrounded) return;

            // only react to friend nekos
            if (!otherNeko.CompareTag("NekoFriend")) return;

            Logkat.Out($"AREntityNeko: detected friend neko spawn at {otherNeko.transform.position}");

            // use existing notification logic
            StateNotifyFriendSpawned(otherNeko);
        }

        #endregion

        #region FSM Notification Methods (called by CoreGameplay - legacy, now also triggered by direct detection)

        /// <summary>
        ///     FSM: notifies this neko that a bowl was placed (interrupt roaming to eat)
        /// </summary>
        public void StateNotifyBowlPlaced()
        {
            if (!CompareTag("NekoMain")) return;

            // if in a non-interruptible state, queue the notification
            if (_currentState == NekoState.PlayingWithFriend)
            {
                _pendingBowlNotification = true;
                Logkat.Out("AREntityNeko: bowl notification queued (playing with friend)");
                return;
            }

            // interrupt roaming or idle to go eat
            if (_currentState is NekoState.Roaming or NekoState.Idle)
            {
                var gameplay = CoreGameplay.instance;
                if (gameplay && gameplay.activeBowl && gameplay.activeBowl.isFull)
                {
                    Logkat.Out("AREntityNeko: bowl placed, heading to eat");
                    TransitionToState(NekoState.MovingToBowl);
                }
            }
        }

        /// <summary>
        ///     FSM: notifies this neko that a friend was spawned (initiate play)
        /// </summary>
        /// <param name="friend">the newly spawned friend neko</param>
        public void StateNotifyFriendSpawned(AREntityNeko friend)
        {
            if (!CompareTag("NekoMain")) return;

            // if in a non-interruptible state, queue the friend
            if (_currentState == NekoState.PlayingWithFriend || _currentState == NekoState.Eating)
            {
                _pendingFriend = friend;
                Logkat.Out("AREntityNeko: friend notification queued");
                return;
            }

            // interrupt roaming or idle to play
            if (_currentState is NekoState.Roaming or NekoState.Idle)
            {
                Logkat.Out("AREntityNeko: friend spawned, initiating play");
                TransitionToState(NekoState.PlayingWithFriend, friend);
            }
        }

        /// <summary>
        ///     FSM: called by main neko to make this friend neko start playing
        /// </summary>
        private void FsmStartStatePlayingAsFriend(AREntityNeko mainNeko)
        {
            if (!CompareTag("NekoFriend")) return;
            Logkat.Out("AREntityNeko: starting play as friend");
            TransitionToState(NekoState.PlayingWithFriend, mainNeko);
        }

        #endregion

        #region FSM State Coroutines

        /// <summary>
        ///     FSM: idle state - wait for a random interval then transition to roaming
        /// </summary>
        private IEnumerator IdleStateRoutine()
        {
            var waitTime = Random.Range(idleWaitMin, idleWaitMax);
            yield return new WaitForSeconds(waitTime);

            // check if navmesh is ready before roaming
            var gameplay = CoreGameplay.instance;
            if (gameplay && gameplay.planes && gameplay.planes.navMeshReady)
                TransitionToState(NekoState.Roaming);
            else
                // navmesh not ready, restart idle
                _stateCoroutine = null;
        }

        /// <summary>
        ///     FSM: roaming state - pick random navmesh point and walk to it
        /// </summary>
        private IEnumerator RoamingStateRoutine()
        {
            // first verify neko is on or near navmesh
            if (!NavMesh.SamplePosition(transform.position, out _, 1f, NavMesh.AllAreas))
            {
                Logkat.Warn($"AREntityNeko: not on navmesh at {transform.position}, staying idle");
                TransitionToState(NekoState.Idle);
                yield break;
            }

            // sample random point on navmesh within roam radius
            var randomDirection = Random.insideUnitSphere * roamRadius;
            randomDirection.y = 0; // keep on horizontal plane
            randomDirection += transform.position;

            // try with roam radius first, then with larger fallback radius
            var foundTarget = false;
            Vector3 targetPosition = default;

            if (NavMesh.SamplePosition(randomDirection, out var hit, roamRadius * 2f, NavMesh.AllAreas))
            {
                targetPosition = hit.position;
                foundTarget = true;
            }

            if (!foundTarget)
            {
                Logkat.Warn($"AREntityNeko: failed to sample navmesh point near {randomDirection}, staying idle");
                TransitionToState(NekoState.Idle);
                yield break;
            }

            Logkat.Out($"AREntityNeko: roaming to {targetPosition}");

            // walk to target (using existing WalkRoutine logic inline)
            var tiltLeft = true;

            while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
            {
                // check for interrupts (handled by FsmTransitionToState stopping this coroutine)
                var direction = (targetPosition - transform.position).normalized;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.001f)
                {
                    var targetRotation = Quaternion.LookRotation(direction);
                    var tiltAngle = tiltLeft ? walkTiltAngle : -walkTiltAngle;
                    var tiltRotation = Quaternion.Euler(0, 0, tiltAngle);
                    transform.rotation = targetRotation * tiltRotation;
                }

                var stepDistance = walkSpeed * walkStepDuration * walkStepDistanceMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, stepDistance);

                tiltLeft = !tiltLeft;
                yield return new WaitForSeconds(walkStepDuration);
            }

            // reset rotation to upright
            var finalRotation = transform.eulerAngles;
            finalRotation.z = 0;
            transform.eulerAngles = finalRotation;

            TransitionToState(NekoState.Idle);
        }

        /// <summary>
        ///     FSM: moving to bowl state - walk toward the bowl
        /// </summary>
        private IEnumerator MovingToBowlStateRoutine()
        {
            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.activeBowl)
            {
                Logkat.Warn("AREntityNeko: no bowl found, returning to idle");
                TransitionToState(NekoState.Idle);
                yield break;
            }

            var bowlPosition = gameplay.activeBowl.transform.position;
            Logkat.Out($"AREntityNeko: moving to bowl at {bowlPosition}");

            // walk to bowl (similar to roaming)
            var tiltLeft = true;

            while (Vector3.Distance(transform.position, bowlPosition) > 0.15f)
            {
                var direction = (bowlPosition - transform.position).normalized;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.001f)
                {
                    var targetRotation = Quaternion.LookRotation(direction);
                    var tiltAngle = tiltLeft ? walkTiltAngle : -walkTiltAngle;
                    var tiltRotation = Quaternion.Euler(0, 0, tiltAngle);
                    transform.rotation = targetRotation * tiltRotation;
                }

                var stepDistance = walkSpeed * walkStepDuration * walkStepDistanceMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, bowlPosition, stepDistance);

                tiltLeft = !tiltLeft;
                yield return new WaitForSeconds(walkStepDuration);
            }

            // reset rotation and face bowl
            LookAt(bowlPosition);

            TransitionToState(NekoState.Eating);
        }

        /// <summary>
        ///     FSM: eating state - consume from bowl
        /// </summary>
        private IEnumerator EatingStateRoutine()
        {
            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.activeBowl)
            {
                Logkat.Warn("AREntityNeko: bowl disappeared, returning to idle");
                TransitionToState(NekoState.Idle);
                yield break;
            }

            // face the bowl
            LookAt(gameplay.activeBowl.transform.position);

            // eating animation: continuous jumping for eatingDuration seconds
            Logkat.Out("AREntityNeko: eating from bowl");
            var elapsed = 0f;
            while (elapsed < eatingDuration)
            {
                Blink();
                Jump();
                yield return new WaitForSeconds(jumpDuration + 0.1f);
                elapsed += jumpDuration + 0.1f;
            }

            // consume the bowl
            gameplay.activeBowl.Consume(this);
            OnFed();

            TransitionToState(NekoState.Idle);
        }

        /// <summary>
        ///     FSM: playing with friend state - look at each other and jump in staggered sync
        /// </summary>
        private IEnumerator SocialisingStateRoutine(AREntityNeko friend)
        {
            if (!friend)
            {
                Logkat.Warn("AREntityNeko: no friend to play with, returning to idle");
                TransitionToState(NekoState.Idle);
                yield break;
            }

            // wait for friend to be grounded (they might still be falling)
            var waitTime = 0f;
            while (!friend._isGrounded && waitTime < 5f)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }

            // tell friend to start playing too
            if (CompareTag("NekoMain"))
                friend.FsmStartStatePlayingAsFriend(this);

            // face each other
            LookAt(friend.transform.position);
            friend.LookAt(transform.position);

            Logkat.Out("AREntityNeko: playing with friend!");

            // simultaneous jump sequence with offset (main neko only controls the loop)
            if (CompareTag("NekoMain"))
            {
                for (var i = 0; i < playJumpCount; i++)
                {
                    // main neko jumps immediately
                    Jump();
                    // friend jumps after small delay (both are now jumping)
                    StartCoroutine(DelayedFriendJump(friend, friendJumpDelay));

                    // wait for both jumps to complete
                    yield return new WaitForSeconds(jumpDuration + friendJumpDelay + 0.1f);
                }

                OnPlayedWithFriend();
            }
            else
            {
                // friend neko just waits for main to finish controlling the sequence
                yield return new WaitForSeconds((jumpDuration + friendJumpDelay + 0.1f) * playJumpCount);
                OnPlayedWithFriend();
            }

            TransitionToState(NekoState.Idle);
        }

        /// <summary>
        ///     helper coroutine to make friend jump after a delay
        /// </summary>
        private static IEnumerator DelayedFriendJump(AREntityNeko friend, float delay)
        {
            yield return new WaitForSeconds(delay);
            friend.Jump();
        }

        /// <summary>
        ///     FSM: being petted state - play happy reaction then return to idle
        /// </summary>
        private IEnumerator PettedStateRoutine()
        {
            Logkat.Out("AREntityNeko: enjoying pets!");

            // happy reaction: blink and jump
            Blink();
            Jump();

            // wait for jump to complete
            yield return new WaitForSeconds(jumpDuration);

            // call stat hook
            OnPetted();

            TransitionToState(NekoState.Idle);
        }

        #endregion

        #region Statistic Hooks

        /// <summary>
        ///     hook called when neko is fed
        /// </summary>
        private void OnFed()
        {
            Logkat.Out("AREntityNeko: OnFed called");
        }

        /// <summary>
        ///     hook called when neko is petted
        /// </summary>
        private void OnPetted()
        {
            Logkat.Out("AREntityNeko: OnPetted called");
        }

        /// <summary>
        ///     hook called when neko plays with friend
        /// </summary>
        private void OnPlayedWithFriend()
        {
            Logkat.Out("AREntityNeko: OnPlayedWithFriend called");
        }

        #endregion
    }
}

