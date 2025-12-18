/*
 * author: mark joshwel
 * date: 18/12/2025
 * description: neko entity with texture management, procedural animations, and behaviour loop.
 *              supports both main neko and friend nekos with mutual recognition and play interactions.
 *              uses GroundingBehaviour for unified AR plane stabilisation
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PokkatCore
{
    /// <summary>
    ///     neko entity with texture loading, blinking animations, movement routines, and behaviour loop.
    ///     handles both main neko (initiates play) and friend nekos (responds to main) with mutual
    ///     face-each-other recognition when multiple friends are present.
    ///     uses GroundingBehaviour for unified AR plane stabilisation
    /// </summary>
    [RequireComponent(typeof(GroundingBehaviour))]
    public sealed class AREntityNeko : MonoBehaviour
    {
        #region Inspector Fields

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
        private float jumpDuration = 0.5f;

        [Tooltip("bounce factor for jump easing (0=sine, 1=full bounce)")] [SerializeField]
        private float jumpBounceFactor = 0.3f;

        [Tooltip("fall speed in metres per second")] [SerializeField]
        private float fallSpeed = 2f;


        [Header("Behaviour Settings")] [Tooltip("roaming radius in metres")] [SerializeField]
        private float roamRadius = 0.5f;

        [Tooltip("duration for animated turns (seconds)")] [SerializeField]
        private float turnDuration = 0.3f;

        [Header("Idle Settings")] [Tooltip("minimum idle wait time (seconds)")] [SerializeField]
        private float idleDurationMin = 1f;

        [Tooltip("maximum idle wait time (seconds)")] [SerializeField]
        private float idleDurationMax = 3f;

        [Tooltip("chance to roam when roam ready (0-1)")] [SerializeField]
        [Range(0f, 1f)]
        private float idleRoamChance = 0.5f;

        [Tooltip("chance to notice another neko (0-1, remainder is look around)")] [SerializeField]
        [Range(0f, 1f)]
        private float idleNoticeChance = 0.3f;

        [Tooltip("how long to look at another neko when noticing (seconds)")] [SerializeField]
        private float noticeHoldDuration = 0.5f;

        [Tooltip("how long to hold when looking around (seconds)")] [SerializeField]
        private float lookAroundHoldDuration = 0.3f;

        [Header("Play Settings")] [Tooltip("delay before friend jumps (seconds)")] [SerializeField]
        private float friendJumpDelay = 0.5f;

        [Tooltip("number of jumps when playing")] [SerializeField]
        private int playJumpCount = 3;

        [Header("Eating Settings")] [Tooltip("eating duration in seconds")] [SerializeField]
        private float eatingDuration = 3f;

        #endregion

        #region Private Fields

        /// <summary>
        ///     grounding component for unified AR plane stabilisation
        /// </summary>
        private GroundingBehaviour _grounding;

        /// <summary>
        ///     cached renderers for texture application
        /// </summary>
        private readonly List<Renderer> _renderers = new();

        /// <summary>
        ///     queue of friend nekos awaiting play interaction (supports multiple friends).
        ///     main neko plays with each friend sequentially via TryRunPendingAction
        /// </summary>
        private readonly Queue<AREntityNeko> _pendingFriendQueue = new();

        /// <summary>
        ///     coroutine handle for blinking
        /// </summary>
        private Coroutine _blinkCoroutine;

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
        ///     coroutine handle for current movement animation
        /// </summary>
        private Coroutine _movementCoroutine;


        /// <summary>
        ///     whether to run the behaviour loop (set false on destroy)
        /// </summary>
        private bool _runBehaviourLoop = true;

        /// <summary>
        ///     the friend neko currently playing with this neko (active play session).
        ///     null when not playing. pending play requests are queued in _pendingFriendQueue
        /// </summary>
        private AREntityNeko _currentPlayPartner;

        /// <summary>
        ///     whether this neko is currently in a play session with another neko
        /// </summary>
        public bool isPlaying => _currentPlayPartner != null;

        /// <summary>
        ///     pending pet request from player touch (processed by behaviour loop)
        /// </summary>
        private bool _pendingPetRequest;

        /// <summary>
        ///     whether this neko is currently eating from a bowl (blocks idle actions)
        /// </summary>
        private bool _isEating;

        #endregion

        #region Static Events

        /// <summary>
        ///     static event fired when any neko is spawned (for direct AR object awareness)
        /// </summary>
        public static event Action<AREntityNeko> OnNekoSpawned;

        /// <summary>
        ///     static event fired when any neko is destroyed
        /// </summary>
        public static event Action<AREntityNeko> OnNekoDestroyed;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        ///     initialises renderer cache, grounding component, sets texture based on tag, loads textures
        /// </summary>
        private void Awake()
        {
            _grounding = GetComponent<GroundingBehaviour>();
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

        /// <summary>
        ///     applies initial texture, starts blinking, subscribes to events, starts behaviour loop
        /// </summary>
        private void Start()
        {
            ApplyTexture(_eyesOpenTexture);
            if (enableBlinking) StartCoroutine(BlinkRoutine());

            AREntityBowl.OnBowlSpawned += OnBowlSpawnedHandler;
            OnNekoSpawned += OnNekoSpawnedHandler;

            OnNekoSpawned?.Invoke(this);
            StartCoroutine(BehaviourLoop());
        }

        /// <summary>
        ///     handles ground stabilisation per-frame (delegates to GroundingBehaviour)
        /// </summary>
        private void Update()
        {
            // skip stabilisation while following tracked image
            if (_isFollowing) return;
            _grounding.Stabilise();
        }

        /// <summary>
        ///     cleans up event subscriptions and fires destroy event
        /// </summary>
        private void OnDestroy()
        {
            _runBehaviourLoop = false;
            AREntityBowl.OnBowlSpawned -= OnBowlSpawnedHandler;
            OnNekoSpawned -= OnNekoSpawnedHandler;
            OnNekoDestroyed?.Invoke(this);
        }

        #endregion

        #region Texture Management

        /// <summary>
        ///     caches all renderer components in children
        /// </summary>
        private void CacheRenderers()
        {
            _renderers.Clear();
            _renderers.AddRange(GetComponentsInChildren<Renderer>(true));
        }

        /// <summary>
        ///     loads texture pair from Resources/NekoTextures for the given id (eyes open + closed)
        /// </summary>
        /// <param name="id">texture id (0-44)</param>
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

        #endregion

        #region Blinking Animation

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

        #endregion

        #region Following State

        /// <summary>
        ///     starts following mode (called by CoreGameplay when spawned on tracked image)
        /// </summary>
        public void StartFollowing()
        {
            _isFollowing = true;
            _grounding.Reset();
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

        #endregion

        #region Movement Animations

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
        ///     performs a single jump in place
        /// </summary>
        private void Jump()
        {
            if (_movementCoroutine != null) StopCoroutine(_movementCoroutine);
            _movementCoroutine = StartCoroutine(JumpRoutine());
        }

        /// <summary>
        ///     coroutine that falls to the nearest horizontal plane and grounds the neko.
        ///     preserves XZ position from tracked image, only adjusts Y to match plane height
        /// </summary>
        /// <param name="onComplete">optional callback invoked when fall completes</param>
        private IEnumerator FallRoutine(Action onComplete)
        {
            Logkat.Dev($"AREntityNeko: FallRoutine started, currentPos={transform.position}");

            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes)
            {
                Logkat.Warn("AREntityNeko: no CoreGameplay instance, cannot fall properly");
                onComplete?.Invoke();
                yield break;
            }

            // get plane height at current XZ position (preserves XZ from tracked image)
            var currentPos = transform.position;
            Vector3 targetPosition;
            if (gameplay.planes.TryGetPlaneHeightAt(currentPos, out var planeHeight))
            {
                // keep XZ, only adjust Y to plane height
                targetPosition = new Vector3(currentPos.x, planeHeight, currentPos.z);
                Logkat.Dev($"AREntityNeko: falling to plane height, targetPos={targetPosition}");
            }
            else
            {
                // fallback: no plane available, snap to y=0 but keep XZ
                Logkat.Dev("AREntityNeko: no horizontal plane found, falling to y=0");
                targetPosition = new Vector3(currentPos.x, 0f, currentPos.z);
            }

            // fall toward target position (animated Y-only drop)
            Logkat.Dev($"AREntityNeko: falling from {transform.position} to {targetPosition}");
            while (Mathf.Abs(transform.position.y - targetPosition.y) > 0.01f)
            {
                var fallStep = fallSpeed * Time.deltaTime;
                var newY = Mathf.MoveTowards(transform.position.y, targetPosition.y, fallStep);
                transform.position = new Vector3(targetPosition.x, newY, targetPosition.z);
                yield return null;
            }

            // snap to exact target
            transform.position = targetPosition;


            // ground the neko at final position (enables stabilisation with XZ lock)
            _grounding.Ground(transform.position);

            _movementCoroutine = null;
            onComplete?.Invoke();
        }


        /// <summary>
        ///     coroutine for a single jump with bounce easing
        /// </summary>
        private IEnumerator JumpRoutine()
        {
            Logkat.Out("AREntityNeko: jumping");

            // play jump sound at start of jump
            CoreGameplay.instance?.PlayJumpSound();

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
        /// <returns>eased value with overshoot</returns>
        private static float EaseOutBack(float t, float bounceFactor)
        {
            var c1 = 1.70158f * bounceFactor;
            var c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>
        ///     rotates neko to face target position (y-axis only)
        /// </summary>
        /// <summary>
        ///     rotates neko to face target position instantly (y-axis only)
        /// </summary>
        /// <param name="target">world position to look at</param>
        private void LookAt(Vector3 target)
        {
            var dir = target - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>
        ///     gradually rotates neko to face target position over time (y-axis only).
        ///     used for natural-looking social interactions
        /// </summary>
        /// <param name="target">world position to look at</param>
        /// <param name="duration">time in seconds to complete the turn</param>
        private IEnumerator TurnToward(Vector3 target, float duration = 0.3f)
        {
            var dir = target - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) yield break;

            var startRotation = transform.rotation;
            var targetRotation = Quaternion.LookRotation(dir);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // ease-out for natural deceleration
                t = 1f - (1f - t) * (1f - t);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            transform.rotation = targetRotation;
        }

        /// <summary>
        ///     snaps neko to nearest horizontal plane while preserving XZ anchor.
        ///     delegates to GroundingBehaviour for unified grounding logic
        /// </summary>
        private void SnapToGround()
        {
            _grounding.SnapToGround();
        }

        /// <summary>
        ///     shared coroutine for choppy stop-motion walk animation.
        ///     used by roam, move-to-bowl, and move-to-friend behaviours.
        ///     updates grounding anchor after each step to allow intentional movement
        /// </summary>
        /// <param name="targetPosition">destination position</param>
        /// <param name="arrivalDistance">distance threshold to consider arrived</param>
        /// <param name="interruptCheck">optional func that returns true to interrupt walking</param>
        private IEnumerator WalkTowardCoroutine(
            Vector3 targetPosition,
            float arrivalDistance,
            Func<bool> interruptCheck = null)
        {
            var tiltLeft = true;
            while (Vector3.Distance(transform.position, targetPosition) > arrivalDistance)
            {
                // check for interrupt conditions (e.g., friend spawned, bowl placed)
                if (interruptCheck != null && interruptCheck())
                    yield break;

                // calculate direction to target (y=0 to keep upright)
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

                // move forward in discrete steps (choppy/stop-motion style)
                var stepDistance = walkSpeed * walkStepDuration * walkStepDistanceMultiplier;
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, stepDistance);
                tiltLeft = !tiltLeft;

                // update grounding anchor to new position (allows intentional movement)
                _grounding.UpdateAnchor(transform.position);

                // play step sound on each step
                CoreGameplay.instance?.PlayStepSound();

                yield return new WaitForSeconds(walkStepDuration);
            }

            // reset rotation to upright after walking
            ResetTilt();
        }

        /// <summary>
        ///     resets z-axis tilt to upright after walking
        /// </summary>
        private void ResetTilt()
        {
            var finalRotation = transform.eulerAngles;
            finalRotation.z = 0;
            transform.eulerAngles = finalRotation;
        }

        /// <summary>
        ///     petting interaction entry point - sets pending flag for behaviour loop to handle.
        ///     called by PlaneHandling when touch input hits this neko
        /// </summary>
        public void Pet()
        {
            _pendingPetRequest = true;
        }

        /// <summary>
        ///     executes the pet animation - neko faces player camera and bounces once.
        ///     uses animated turn for natural feel
        /// </summary>
        private IEnumerator RunPetAction()
        {
            // face the player camera with animated turn
            var mainCamera = Camera.main;
            if (mainCamera)
                yield return TurnToward(mainCamera.transform.position, turnDuration);

            // blink and bounce as acknowledgement
            Blink();
            Jump();

            // play meow sound
            CoreGameplay.instance?.PlayMeowSound();

            Logkat.Out("AREntityNeko: petted!");

            // only main neko triggers the stat hook
            if (CompareTag("NekoMain"))
                OnPetted();
        }

        #endregion

        #region Action Handling

        /// <summary>
        ///     checks if there's a pending action and returns true if current behaviour should abort.
        ///     does NOT execute the action - just checks if one is pending
        /// </summary>
        private bool HasPendingAction()
        {
            // pet request has the highest priority
            if (_pendingPetRequest) return true;

            // clean up null/destroyed friends from queue
            while (_pendingFriendQueue.Count > 0 && _pendingFriendQueue.Peek() == null)
                _pendingFriendQueue.Dequeue();

            return _pendingFriendQueue.Count > 0;
        }

        /// <summary>
        ///     tries to execute the highest priority pending action.
        ///     returns true if an action was executed, false if none pending
        /// </summary>
        private bool TryRunPendingAction()
        {
            // 1. pet request (highest priority - player interaction)
            if (_pendingPetRequest)
            {
                _pendingPetRequest = false;
                StartCoroutine(RunPetAction());
                return true;
            }

            // 2. friend play request
            // clean up null/destroyed friends from queue
            while (_pendingFriendQueue.Count > 0 && _pendingFriendQueue.Peek() == null)
                _pendingFriendQueue.Dequeue();

            if (_pendingFriendQueue.Count > 0)
            {
                var friend = _pendingFriendQueue.Dequeue();
                if (friend != null)
                {
                    StartCoroutine(PlayWithFriend(friend));
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Awareness Handlers

        /// <summary>
        ///     callback for when any bowl spawns in the scene (direct AR object awareness).
        ///     only main neko responds to bowl placement
        /// </summary>
        /// <param name="bowl">the spawned bowl entity</param>
        private void OnBowlSpawnedHandler(AREntityBowl bowl)
        {
            if (!CompareTag("NekoMain")) return;
            if (!_grounding.isGrounded) return;
            Logkat.Out($"AREntityNeko: detected bowl spawn at {bowl.transform.position}");
            StateNotifyBowlPlaced();
        }

        /// <summary>
        ///     callback for when any neko spawns in the scene (direct AR object awareness).
        ///     main neko queues friend nekos for play interaction; friend nekos ignore this
        /// </summary>
        /// <param name="otherNeko">the spawned neko</param>
        private void OnNekoSpawnedHandler(AREntityNeko otherNeko)
        {
            // ignore self
            if (otherNeko == this) return;

            // only main neko initiates play with friends
            if (!CompareTag("NekoMain")) return;
            if (!_grounding.isGrounded) return;

            // only react to friend nekos
            if (!otherNeko.CompareTag("NekoFriend")) return;

            Logkat.Out($"AREntityNeko: detected friend neko spawn at {otherNeko.transform.position}");

            // queue friend for play (handles multiple friends spawning in quick succession)
            _pendingFriendQueue.Enqueue(otherNeko);
        }

        #endregion

        #region Behaviour Loop

        /// <summary>
        ///     main behaviour loop - runs while neko is alive.
        ///     idle-centric: waits in idle state, performs idle actions, handles priority interrupts
        /// </summary>
        private IEnumerator BehaviourLoop()
        {
            while (_runBehaviourLoop)
            {
                // wait until we've landed on a plane (grounding complete)
                while (!_grounding.isGrounded) yield return null;

                // check for pending actions first (pet, friend play)
                if (TryRunPendingAction())
                {
                    yield return null;
                    continue;
                }

                // check if roaming is possible (plane detection working)
                var canRoam = CanRoam();
                
                // notify game state if main neko waiting for planes
                if (!canRoam && CompareTag("NekoMain"))
                    CoreGameplay.instance?.NotifyMainNekoWaitingForPlane();
                else if (canRoam && CompareTag("NekoMain"))
                    CoreGameplay.instance?.NotifyMainNekoHasPlane();

                // idle (roaming allowed if planes are ready)
                yield return Idle(canRoam);
            }
        }

        /// <summary>
        ///     idle state handler - wait random duration then performs idle action.
        ///     can be interrupted by pending actions at any time.
        ///     skips idle actions if neko is eating
        /// </summary>
        /// <param name="canRoam">if true, roaming is allowed; if false, only notice/look around</param>
        private IEnumerator Idle(bool canRoam)
        {
            // skip idle if eating (neko should focus on bowl)
            if (_isEating) yield break;

            // wait random idle duration, checking for interrupts
            var idleDuration = Random.Range(idleDurationMin, idleDurationMax);
            var elapsed = 0f;
            while (elapsed < idleDuration)
            {
                if (HasPendingAction() || _isEating) yield break;
                yield return null;
                elapsed += Time.deltaTime;
            }

            // check for pending actions or eating before starting idle action
            if (HasPendingAction() || _isEating) yield break;

            // roll for idle action type
            // distribution: roam (if planes ready) -> notice -> look around
            var roll = Random.value;
            Logkat.Dev($"AREntityNeko: Idle roll={roll:F2}, canRoam={canRoam}, roamChance={idleRoamChance}, noticeChance={idleNoticeChance}");

            if (canRoam && roll < idleRoamChance)
            {
                // roam only if plane detection is ready
                yield return IdleRoam();
            }
            else if (roll < idleRoamChance + idleNoticeChance)
            {
                // notice another neko (if any exist)
                yield return IdleNotice();
            }
            else
            {
                // look around to a random direction
                yield return IdleLookAround();
            }
        }

        /// <summary>
        ///     checks if plane-based roaming is possible (plane detection is working)
        /// </summary>
        /// <returns>true if planes are available for roaming</returns>
        private bool CanRoam()
        {
            var planes = CoreGameplay.instance?.planes;
            return planes != null && planes.isReady;
        }

        /// <summary>
        ///     idle action: picks random point on detected AR plane and walks to it.
        ///     uses PlaneHandling for plane projection instead of NavMesh.
        ///     can be interrupted by pending actions (pet, friend play)
        /// </summary>
        private IEnumerator IdleRoam()
        {
            // check for pending actions first
            if (HasPendingAction()) yield break;

            var planes = CoreGameplay.instance?.planes;
            if (planes == null) yield break;

            // pick a random point within roam radius (XZ only)
            var randomOffset = Random.insideUnitSphere * roamRadius;
            randomOffset.y = 0f;
            var targetPosition = transform.position + randomOffset;

            // project the random point onto the nearest AR plane
            if (!planes.TryProjectToPlane(targetPosition, out var projectedPosition))
            {
                // no plane found at target position, skip this roam
                Logkat.Dev($"AREntityNeko: IdleRoam failed to find plane near {targetPosition}");
                yield break;
            }

            // walk to the projected point, checking for interrupts
            Logkat.Dev($"AREntityNeko: IdleRoam walking to {projectedPosition}");
            yield return WalkTowardCoroutine(projectedPosition, 0.1f, HasPendingAction);
        }

        /// <summary>
        ///     idle action: looks at a random other neko in the scene.
        ///     creates natural "noticing" behaviour between nekos.
        ///     skipped if neko is eating
        /// </summary>
        private IEnumerator IdleNotice()
        {
            // skip if eating (neko should focus on bowl)
            if (_isEating) yield break;

            // find all other nekos in the scene (unsorted for performance)
            var allNekos = FindObjectsByType<AREntityNeko>(FindObjectsSortMode.None);
            if (allNekos.Length <= 1) yield break; // no other nekos to notice

            // filter out self and build list of valid targets
            var validTargets = new List<AREntityNeko>();
            foreach (var neko in allNekos)
            {
                if (neko != this && neko != null)
                    validTargets.Add(neko);
            }

            if (validTargets.Count == 0) yield break;

            // pick a random other neko
            var target = validTargets[Random.Range(0, validTargets.Count)];
            if (!target) yield break;

            // check for pending actions before turning
            if (HasPendingAction()) yield break;

            // gradually turn to face the other neko
            yield return TurnToward(target.transform.position, turnDuration);

            // hold the look for a moment (interruptible)
            var holdElapsed = 0f;
            while (holdElapsed < noticeHoldDuration)
            {
                if (HasPendingAction()) yield break;
                yield return null;
                holdElapsed += Time.deltaTime;
            }
        }

        /// <summary>
        ///     idle action: looks in a random direction.
        ///     creates natural "looking around" behaviour
        /// </summary>
        private IEnumerator IdleLookAround()
        {
            // skip if eating (neko should focus on bowl)
            if (_isEating) yield break;

            // check for pending actions before turning
            if (HasPendingAction()) yield break;

            // pick a random direction (y-axis rotation only)
            var randomAngle = Random.Range(0f, 360f);
            var randomDirection = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            var targetPosition = transform.position + randomDirection;

            // gradually turn to face the random direction
            yield return TurnToward(targetPosition, turnDuration);

            // hold the look for a moment (interruptible)
            var holdElapsed = 0f;
            while (holdElapsed < lookAroundHoldDuration)
            {
                if (HasPendingAction()) yield break;
                yield return null;
                holdElapsed += Time.deltaTime;
            }
        }

        /// <summary>
        ///     play interaction between main neko and one friend neko.
        ///     both nekos gradually turn to face each other before jumping together.
        ///     multiple friends are handled sequentially via _pendingFriendQueue.
        ///     does not require friend to be grounded - plays with friend's current position
        /// </summary>
        /// <param name="friend">the friend neko to play with</param>
        private IEnumerator PlayWithFriend(AREntityNeko friend)
        {
            if (!friend) yield break;

            // set up mutual recognition - both nekos know they're playing together
            _currentPlayPartner = friend;
            friend._currentPlayPartner = this;

            // both nekos gradually turn to face each other (natural "noticing" moment)
            // uses current position, works even if friend is still following image
            StartCoroutine(TurnToward(friend.transform.position, turnDuration));
            StartCoroutine(friend.TurnToward(transform.position, turnDuration));
            yield return new WaitForSeconds(turnDuration + 0.1f);

            // play meow sound when playing starts (after turning to face each other)
            CoreGameplay.instance?.PlayMeowSound();

            Logkat.Out("AREntityNeko: playing with friend (facing each other)");

            // synchronised jumping sequence
            for (var i = 0; i < playJumpCount; i++)
            {
                // refresh facing direction each jump (instant snap during play is fine)
                LookAt(friend.transform.position);
                friend.LookAt(transform.position);

                Jump();
                StartCoroutine(DelayedFriendJump(friend, friendJumpDelay));
                yield return new WaitForSeconds(jumpDuration + friendJumpDelay + 0.1f);
            }

            // clear play partner references
            _currentPlayPartner = null;
            if (friend) friend._currentPlayPartner = null;

            OnPlayedWithFriend();
        }

        /// <summary>
        ///     triggers a jump on friend neko after a delay (for offset synchronised jumping)
        /// </summary>
        /// <param name="friend">friend neko to jump</param>
        /// <param name="delay">delay before jump in seconds</param>
        private static IEnumerator DelayedFriendJump(AREntityNeko friend, float delay)
        {
            yield return new WaitForSeconds(delay);
            friend?.Jump();
        }

        /// <summary>
        ///     notifies main neko that a bowl has been placed - triggers move-and-eat behaviour
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void StateNotifyBowlPlaced()
        {
            if (!CompareTag("NekoMain")) return;

            // cancel any existing move-and-eat coroutine (prevents stuck walking animation)
            if (_moveAndEatCoroutine != null)
            {
                StopCoroutine(_moveAndEatCoroutine);
                _moveAndEatCoroutine = null;
                ResetTilt();
            }

            _moveAndEatCoroutine = StartCoroutine(MoveAndEat());
        }

        /// <summary>
        ///     coroutine handle for move-and-eat (for cancellation on new bowl spawn)
        /// </summary>
        private Coroutine _moveAndEatCoroutine;

        /// <summary>
        ///     the bowl we are currently walking toward (for interrupt detection)
        /// </summary>
        private AREntityBowl _targetBowl;

        /// <summary>
        ///     moves toward active bowl and eats it.
        ///     can be interrupted by pending actions (pet, friend play) or bowl replacement
        /// </summary>
        private IEnumerator MoveAndEat()
        {
            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.activeBowl) yield break;

            _targetBowl = gameplay.activeBowl;
            var bowlPos = _targetBowl.transform.position;

            // interrupt check: pending actions OR bowl was replaced/destroyed
            bool ShouldInterrupt()
            {
                return HasPendingAction() || !_targetBowl || gameplay.activeBowl != _targetBowl;
            }

            // walk toward bowl, can be interrupted
            yield return WalkTowardCoroutine(bowlPos, 0.15f, ShouldInterrupt);

            // if interrupted by pending action, exit early and let behaviour loop handle it
            if (HasPendingAction())
            {
                _targetBowl = null;
                _moveAndEatCoroutine = null;
                yield break;
            }

            // if bowl was replaced/destroyed during walk, reset and exit
            if (!_targetBowl || gameplay.activeBowl != _targetBowl)
            {
                ResetTilt();
                _targetBowl = null;
                _moveAndEatCoroutine = null;
                yield break;
            }

            // face the bowl with animated turn
            yield return TurnToward(bowlPos, turnDuration);

            // start eating - blocks idle actions like IdleNotice
            _isEating = true;

            // eating animation: continuous jumping
            var elapsed = 0f;
            while (elapsed < eatingDuration)
            {
                // pending actions or bowl changes can interrupt eating
                if (HasPendingAction() || !_targetBowl || gameplay.activeBowl != _targetBowl)
                {
                    _isEating = false;
                    _targetBowl = null;
                    _moveAndEatCoroutine = null;
                    yield break;
                }

                Blink();
                Jump();

                // play eating sound during eating animation
                gameplay.PlayEatingSound();

                yield return new WaitForSeconds(jumpDuration + 0.1f);
                elapsed += jumpDuration + 0.1f;
            }

            // consume the bowl
            if (_targetBowl && gameplay.activeBowl == _targetBowl)
            {
                _targetBowl.Consume(this);
                OnFed();
            }

            _isEating = false;
            _targetBowl = null;
            _moveAndEatCoroutine = null;
            SnapToGround();
        }

        #endregion

        #region Stat Hooks

        /// <summary>
        ///     called when neko finishes eating from bowl (for stats integration)
        /// </summary>
        private void OnFed()
        {
            Logkat.Out("AREntityNeko: fed");
        }

        /// <summary>
        ///     called when neko finishes playing with friend (for stats integration)
        /// </summary>
        private void OnPlayedWithFriend()
        {
            Logkat.Out("AREntityNeko: played with friend");
        }

        /// <summary>
        ///     called when main neko is petted by player (for stats integration)
        /// </summary>
        private void OnPetted()
        {
            Logkat.Out("AREntityNeko: petted (main neko stat hook)");
        }

        #endregion
    }
}