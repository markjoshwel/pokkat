/*
 * author: mark joshwel
 * date: 18/12/2025
 * description: bowl entity with food stages, consumption logic, and unified grounding
 */

using System;
using UnityEngine;

namespace PokkatCore
{
    /// <summary>
    ///     bowl entity that can be placed on AR planes and consumed by nekos.
    ///     uses GroundingBehaviour for unified AR plane stabilisation
    /// </summary>
    [RequireComponent(typeof(GroundingBehaviour))]
    public sealed class AREntityBowl : MonoBehaviour
    {
        #region Private Fields

        /// <summary>
        ///     grounding component for unified AR plane stabilisation
        /// </summary>
        private GroundingBehaviour _grounding;

        #endregion

        #region Public Properties

        /// <summary>
        ///     whether the bowl has food in it
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public bool isFull { get; private set; } = true;

        #endregion

        #region Stat Hooks

        /// <summary>
        ///     skeleton hook for stats integration when neko consumes from bowl
        /// </summary>
        /// <param name="consumer">the neko that consumed from this bowl</param>
        private void OnNekoConsumed(AREntityNeko consumer)
        {
            Logkat.Out($"AREntityBowl: OnNekoConsumed called for {consumer.name}");
        }

        #endregion

        #region Static Events

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

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _grounding = GetComponent<GroundingBehaviour>();
            Logkat.Out("AREntityBowl: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("AREntityBowl: Start/Configure OK");

            // unparent from plane so grounding can lock position
            // parenting causes bowl to drift with AR plane adjustments
            if (transform.parent != null)
            {
                Logkat.Dev($"AREntityBowl: unparenting from {transform.parent.name}");
                transform.SetParent(null, true);
            }

            // store spawn position and get plane height at this XZ
            // we only adjust Y to plane height, keeping XZ exactly where the bowl was spawned
            var spawnPos = transform.position;
            var planes = CoreGameplay.instance?.planes;
            if (planes != null && planes.TryGetPlaneHeightAt(spawnPos, out var planeHeight))
            {
                var groundedPos = new Vector3(spawnPos.x, planeHeight, spawnPos.z);
                Logkat.Dev($"AREntityBowl: grounding from {spawnPos} to {groundedPos}");
                _grounding.Ground(groundedPos);
            }
            else
            {
                // fallback: ground at current position
                Logkat.Dev($"AREntityBowl: no plane, grounding at spawn position {spawnPos}");
                _grounding.Ground(spawnPos);
            }

            // broadcast spawn event for direct AR object awareness
            OnBowlSpawned?.Invoke(this);
            Logkat.Out("AREntityBowl: broadcasted spawn event");

            // play bowl placement sound
            CoreGameplay.instance?.PlayBowlPlaceSound();
        }

        private void Update()
        {
            _grounding.Stabilise();
        }

        private void OnDestroy()
        {
            OnBowlDestroyed?.Invoke(this);
        }

        #endregion

        #region Bowl Consumption

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

            // play bowl consume sound
            CoreGameplay.instance?.PlayBowlConsumeSound();

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

        #endregion
    }
}