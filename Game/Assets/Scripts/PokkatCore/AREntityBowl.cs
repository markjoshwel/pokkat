/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: manages the bowl entity with food stages and interaction
 */

using System;
using UnityEngine;

namespace PokkatCore
{
    public class AREntityBowl : MonoBehaviour
    {
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

            // broadcast spawn event for direct AR object awareness
            OnBowlSpawned?.Invoke(this);
            Logkat.Out("AREntityBowl: broadcasted spawn event");
        }

        private void OnDestroy()
        {
            // broadcast destroy event
            OnBowlDestroyed?.Invoke(this);
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
        protected virtual void OnNekoConsumed(AREntityNeko consumer)
        {
            Logkat.Out($"AREntityBowl: OnNekoConsumed called for {consumer.name}");
        }
    }
}