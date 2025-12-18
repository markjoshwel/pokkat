/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: manages the bowl entity with food stages and interaction
 */

using System;
using UnityEngine;

namespace PokkatCore
{
    public sealed class AREntityBowl : MonoBehaviour
    {
        [Header("Ground Stabilisation")] [Tooltip("project to nearest plane when spawned")] [SerializeField]
        private bool enableGroundStabilisation = true;

        [Tooltip("seconds between checks")] [SerializeField]
        private float stabilisationInterval = 0.1f;

        [Tooltip("minimum drift to stabilise")] [SerializeField]
        private float stabilisationThreshold = 0.02f;

        /// <summary>
        ///     timer for ground stabilisation interval
        /// </summary>
        private float _stabilisationTimer;

        /// <summary>
        ///     spawn position for XZ locking (like grounded neko)
        /// </summary>
        private Vector3 _spawnPosition;

        /// <summary>
        ///     whether the bowl has been grounded to a plane
        /// </summary>
        private bool _isGrounded;

        /// <summary>
        ///     whether the bowl has food in it
        /// </summary>
        public bool isFull { get; private set; } = true;

        private void Awake()
        {
            Logkat.Out("AREntityBowl: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("AREntityBowl: Start/Configure OK");

            // unparent from plane so we can lock position (like grounded neko)
            // parenting causes bowl to drift with AR plane adjustments
            if (transform.parent != null)
            {
                Logkat.Dev($"AREntityBowl: unparenting from {transform.parent.name}");
                transform.SetParent(null, true);
            }

            // store spawn position for XZ locking
            _spawnPosition = transform.position;
            Logkat.Dev($"AREntityBowl: spawn position = {_spawnPosition}");

            // immediately project to ground (like neko fall)
            ProjectToGround();

            // broadcast spawn event for direct AR object awareness
            OnBowlSpawned?.Invoke(this);
            Logkat.Out("AREntityBowl: broadcasted spawn event");
        }

        private void Update()
        {
            UpdateGroundStabilisation();
        }

        private void OnDestroy()
        {
            // broadcast destroy event
            OnBowlDestroyed?.Invoke(this);
        }

        /// <summary>
        ///     projects the bowl to the nearest plane surface (like neko fall)
        /// </summary>
        private void ProjectToGround()
        {
            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes)
            {
                Logkat.Dev("AREntityBowl: no CoreGameplay/planes, cannot project");
                return;
            }

            if (gameplay.planes.TryProjectToPlane(transform.position, out var projected))
            {
                Logkat.Dev($"AREntityBowl: projected from {transform.position} to {projected}");
                transform.position = projected;
                _spawnPosition = new Vector3(_spawnPosition.x, projected.y, _spawnPosition.z);
                _isGrounded = true;
            }
            else
            {
                Logkat.Dev($"AREntityBowl: failed to project {transform.position} to plane");
            }
        }

        /// <summary>
        ///     handles ground stabilisation (timer-based plane projection with XZ lock)
        /// </summary>
        private void UpdateGroundStabilisation()
        {
            if (!enableGroundStabilisation) return;
            if (!_isGrounded) return;

            _stabilisationTimer -= Time.deltaTime;
            if (_stabilisationTimer > 0f) return;
            _stabilisationTimer = stabilisationInterval;

            var gameplay = CoreGameplay.instance;
            if (!gameplay || !gameplay.planes) return;

            if (!gameplay.planes.TryProjectToPlane(transform.position, out var projectedPos))
            {
                Logkat.Dev($"AREntityBowl: stabilisation failed, no plane at {transform.position}");
                return;
            }

            var yDrift = Mathf.Abs(transform.position.y - projectedPos.y);
            var xzDrift = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(_spawnPosition.x, _spawnPosition.z)
            );

            // lock XZ to spawn position, only allow Y stabilisation
            if (yDrift >= stabilisationThreshold || xzDrift >= stabilisationThreshold)
            {
                var stablePos = new Vector3(_spawnPosition.x, projectedPos.y, _spawnPosition.z);
                Logkat.Dev($"AREntityBowl: stabilising from {transform.position} to {stablePos} (yDrift={yDrift:F3}, xzDrift={xzDrift:F3})");
                transform.position = stablePos;
            }
        }

        /// <summary>
        ///     static event fired when any bowl is spawned (for direct AR object awareness)
        /// </summary>
        public static event Action<AREntityBowl> OnBowlSpawned;

        /// <summary>
        ///     static event fired when any bowl is destroyed
        /// </summary>
        public static event Action<AREntityBowl> OnBowlDestroyed;

        /// <summary>
        ///     fired when a neko consumes from this bowl
        /// </summary>
        public event Action<AREntityNeko> OnConsumed;

        /// <summary>
        ///     consumes food from the bowl (called by neko when eating)
        /// </summary>
        /// <param name="consumer">the neko consuming from this bowl</param>
        public void Consume(AREntityNeko consumer)
        {
            if (!isFull)
            {
                Logkat.Warn("AREntityBowl: bowl is already empty");
                return;
            }

            isFull = false;
            Logkat.Out("AREntityBowl: consumed by neko");

            UpdateBowlVisual();
            OnNekoConsumed(consumer);
            OnConsumed?.Invoke(consumer);
        }

        /// <summary>
        ///     refills the bowl (for future use)
        /// </summary>
        public void Refill()
        {
            isFull = true;
            Logkat.Out("AREntityBowl: refilled");
            UpdateBowlVisual();
        }

        /// <summary>
        ///     updates the bowl's visual appearance based on isFull state
        /// </summary>
        private void UpdateBowlVisual()
        {
            Logkat.Warn("AREntityBowl: mesh swap not implemented yet");
        }

        /// <summary>
        ///     skeleton hook for stats integration when neko consumes from bowl
        /// </summary>
        /// <param name="consumer">the neko that consumed from this bowl</param>
        private void OnNekoConsumed(AREntityNeko consumer)
        {
            Logkat.Out($"AREntityBowl: OnNekoConsumed called for {consumer.name}");
        }
    }
}