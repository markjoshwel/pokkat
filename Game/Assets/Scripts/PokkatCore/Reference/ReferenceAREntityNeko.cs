/*
 * author: mark joshwel
 * date: 11/12/2024
 * description: neko character with state machine, NavMesh navigation, and procedural animations
 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace PokkatCore.Reference
{
    /// <summary>
    ///     possible behavioural states for the neko character
    /// </summary>
    public enum NekoState
    {
        Idle,
        Jumping,
        SeekingBowl,
        Socializing
    }

    /// <summary>
    ///     manages the neko character behaviour including movement, animations, and interactions
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ReferenceAREntityNeko : MonoBehaviour
    {
        [Header("State Machine")] [Tooltip("Initial state when the neko spawns.")] [SerializeField]
        private NekoState initialState = NekoState.Idle;

        [Tooltip("Time between automatic state changes in seconds.")] [SerializeField]
        private float stateChangeIntervalSeconds = 5f;

        [Header("Movement")] [Tooltip("Radius for random wandering.")] [SerializeField]
        private float wanderRadius = 2f;

        [Tooltip("Distance at which the neko considers the bowl reached.")] [SerializeField]
        private float bowlReachDistance = 0.3f;

        [Header("Jump Animation")] [Tooltip("Height of the procedural jump animation.")] [SerializeField]
        private float jumpHeight = 0.15f;

        [Tooltip("Duration of the jump animation in seconds.")] [SerializeField]
        private float jumpDurationSeconds = 0.5f;

        [Header("Pivot Animation")]
        [Tooltip("Rotation speed for pivot animation in degrees per second.")]
        [SerializeField]
        private float pivotSpeedDegreesPerSecond = 90f;

        [Tooltip("Duration of the pivot animation in seconds.")] [SerializeField]
        private float pivotDurationSeconds = 1f;

        [Header("Socializing")] [Tooltip("Range to detect other nekos for socializing.")] [SerializeField]
        private float socialRange = 3f;

        [Tooltip("Layer mask to detect other nekos.")] [SerializeField]
        private LayerMask nekoLayerMask;

        private readonly Collider[] _socialColliderCache = new Collider[16];

        private NavMeshAgent _agent;
        private Coroutine _animationRoutine;
        private Vector3 _baseLocalPosition;
        private float _nextStateChangeTime;
        private ReferenceAREntityBowl _targetBowl;

        /// <summary>
        ///     current behavioural state of the neko
        /// </summary>
        public NekoState currentState { get; private set; }

        /// <summary>
        ///     jump rate used for syncing with other nekos during socializing
        /// </summary>
        public float jumpRate => 1f / jumpDurationSeconds;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _baseLocalPosition = transform.localPosition;
        }

        private void Start()
        {
            SetState(initialState);
            _nextStateChangeTime = Time.time + stateChangeIntervalSeconds;
        }

        private void Update()
        {
            if (Time.time >= _nextStateChangeTime && currentState != NekoState.SeekingBowl)
            {
                PickRandomState();
                _nextStateChangeTime = Time.time + stateChangeIntervalSeconds;
            }

            switch (currentState)
            {
                case NekoState.Idle:
                    HandleIdleState();
                    break;
                case NekoState.Jumping:
                    break;
                case NekoState.SeekingBowl:
                    HandleSeekingBowlState();
                    break;
                case NekoState.Socializing:
                    HandleSocializingState();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnDisable()
        {
            StopAllAnimations();
        }

        /// <summary>
        ///     function to handle trigger collisions for bowl interaction
        /// </summary>
        /// <param name="other">collider that triggered the event</param>
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Bowl")) return;

            var bowl = other.GetComponent<ReferenceAREntityBowl>();
            if (!bowl) return;
            if (!bowl.Consume()) return;

            Debug.Log("ReferenceAREntityNeko: ate from bowl");
            SetState(NekoState.Idle);
        }

        private void OnValidate()
        {
            stateChangeIntervalSeconds = Mathf.Max(0.1f, stateChangeIntervalSeconds);
            jumpDurationSeconds = Mathf.Max(0.1f, jumpDurationSeconds);
            pivotDurationSeconds = Mathf.Max(0.1f, pivotDurationSeconds);
        }

        /// <summary>
        ///     function to set the target bowl for seeking behaviour
        /// </summary>
        /// <param name="bowl">bowl to seek</param>
        public void SetTargetBowl(ReferenceAREntityBowl bowl)
        {
            _targetBowl = bowl;
            if (_targetBowl)
                Debug.Log($"ReferenceAREntityNeko: target bowl set to {bowl.name}");
        }

        /// <summary>
        ///     function to request the neko to seek the bowl
        /// </summary>
        public void SeekBowl()
        {
            if (!_targetBowl)
            {
                Debug.LogWarning("ReferenceAREntityNeko: cannot seek bowl, no target bowl assigned");
                return;
            }

            SetState(NekoState.SeekingBowl);
        }

        /// <summary>
        ///     function to change the current state and start associated behaviours
        /// </summary>
        /// <param name="newState">state to transition to</param>
        private void SetState(NekoState newState)
        {
            if (currentState == newState) return;

            StopAllAnimations();
            currentState = newState;
            Debug.Log($"ReferenceAREntityNeko: state changed to {newState}");

            switch (newState)
            {
                case NekoState.Idle:
                    _agent.ResetPath();
                    break;
                case NekoState.Jumping:
                    _agent.ResetPath();
                    _animationRoutine = StartCoroutine(JumpRoutine());
                    break;
                case NekoState.SeekingBowl:
                    break;
                case NekoState.Socializing:
                    _agent.ResetPath();
                    break;
            }
        }

        /// <summary>
        ///     function to pick a random state excluding SeekingBowl
        /// </summary>
        private void PickRandomState()
        {
            var roll = Random.Range(0, 3);
            var newState = roll switch
            {
                0 => NekoState.Idle,
                1 => NekoState.Jumping,
                2 => NekoState.Socializing,
                _ => NekoState.Idle
            };

            SetState(newState);
        }

        /// <summary>
        ///     function to handle idle state behaviour (occasional pivot)
        /// </summary>
        private void HandleIdleState()
        {
            if (_animationRoutine != null) return;
            if (Random.value < 0.01f)
                _animationRoutine = StartCoroutine(PivotRoutine());
        }

        /// <summary>
        ///     function to handle seeking bowl state using navmesh
        /// </summary>
        private void HandleSeekingBowlState()
        {
            if (!_targetBowl)
            {
                SetState(NekoState.Idle);
                return;
            }

            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning("ReferenceAREntityNeko: neko not on navmesh, cannot seek bowl");
                return;
            }

            var distanceToBowl = Vector3.Distance(transform.position, _targetBowl.transform.position);
            if (distanceToBowl <= bowlReachDistance)
            {
                _agent.ResetPath();
                OnReachedBowl();
                return;
            }

            if (!_agent.hasPath || _agent.remainingDistance < 0.1f)
                _agent.SetDestination(_targetBowl.transform.position);
        }

        /// <summary>
        ///     function to handle socializing state (look at and sync with other nekos)
        /// </summary>
        private void HandleSocializingState()
        {
            var hitCount =
                Physics.OverlapSphereNonAlloc(transform.position, socialRange, _socialColliderCache, nekoLayerMask);

            ReferenceAREntityNeko nearestOther = null;
            var nearestDistance = float.MaxValue;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _socialColliderCache[i];
                var otherNeko = hit.GetComponentInParent<ReferenceAREntityNeko>();
                if (!otherNeko || otherNeko == this) continue;

                var distance = Vector3.Distance(transform.position, otherNeko.transform.position);
                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                nearestOther = otherNeko;
            }

            if (!nearestOther) return;

            var lookDirection = nearestOther.transform.position - transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDirection),
                    Time.deltaTime * 5f);

            if (_animationRoutine != null) return;
            var syncOffset = 1f / nearestOther.jumpRate * 0.1f;
            _animationRoutine = StartCoroutine(SyncedJumpRoutine(syncOffset));
        }

        /// <summary>
        ///     callback for when the neko reaches the bowl
        /// </summary>
        private void OnReachedBowl()
        {
            Debug.Log("ReferenceAREntityNeko: reached bowl");
            SetState(NekoState.Idle);
        }

        /// <summary>
        ///     function to stop all running animation coroutines
        /// </summary>
        private void StopAllAnimations()
        {
            if (_animationRoutine != null)
            {
                StopCoroutine(_animationRoutine);
                _animationRoutine = null;
            }

            ResetLocalPosition();
        }

        /// <summary>
        ///     function to reset the local position to baseline (after jump animations)
        /// </summary>
        private void ResetLocalPosition()
        {
            var pos = transform.localPosition;
            pos.y = _baseLocalPosition.y;
            transform.localPosition = pos;
        }

        /// <summary>
        ///     procedural jump animation using sin curve
        /// </summary>
        private IEnumerator JumpRoutine()
        {
            var elapsed = 0f;

            while (elapsed < jumpDurationSeconds)
            {
                elapsed += Time.deltaTime;
                var normalised = elapsed / jumpDurationSeconds;
                var heightOffset = Mathf.Sin(normalised * Mathf.PI) * jumpHeight;

                var pos = transform.localPosition;
                pos.y = _baseLocalPosition.y + heightOffset;
                transform.localPosition = pos;

                yield return null;
            }

            ResetLocalPosition();
            _animationRoutine = null;
            SetState(NekoState.Idle);
        }

        /// <summary>
        ///     synced jump with offset for socializing
        /// </summary>
        /// <param name="delaySeconds">delay before starting jump</param>
        private IEnumerator SyncedJumpRoutine(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            var elapsed = 0f;
            while (elapsed < jumpDurationSeconds)
            {
                elapsed += Time.deltaTime;
                var normalised = elapsed / jumpDurationSeconds;
                var heightOffset = Mathf.Sin(normalised * Mathf.PI) * jumpHeight;

                var pos = transform.localPosition;
                pos.y = _baseLocalPosition.y + heightOffset;
                transform.localPosition = pos;

                yield return null;
            }

            ResetLocalPosition();
            _animationRoutine = null;
        }

        /// <summary>
        ///     procedural pivot/rotation animation
        /// </summary>
        private IEnumerator PivotRoutine()
        {
            var elapsed = 0f;
            var direction = Random.value > 0.5f ? 1f : -1f;

            while (elapsed < pivotDurationSeconds)
            {
                elapsed += Time.deltaTime;
                transform.Rotate(Vector3.up, pivotSpeedDegreesPerSecond * direction * Time.deltaTime);
                yield return null;
            }

            _animationRoutine = null;
        }
    }
}