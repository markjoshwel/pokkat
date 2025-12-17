/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: manages the bowl entity with food stages and interaction
 */

using UnityEngine;

namespace PokkatCore.Reference
{
    /// <summary>
    ///     represents the bowl entity that nekos interact with for feeding
    /// </summary>
    public class ReferenceAREntityBowl : MonoBehaviour
    {
        [Header("Food Stages")]
        [Tooltip("Visual representations of bowl fullness (index 0 = full, last = empty).")]
        [SerializeField]
        private GameObject[] foodStages;

        [Tooltip("Maximum number of consumptions before empty.")] [SerializeField]
        private int maxConsumptions = 3;

        /// <summary>
        ///     the hunger value provided per consumption
        /// </summary>
        [Header("Feeding")] [Tooltip("How much hunger each consumption provides.")]
        public readonly float HungerPerConsumption = 10f;

        private int _consumptionCount;

        /// <summary>
        ///     whether the bowl has food remaining
        /// </summary>
        public bool hasFood => _consumptionCount < maxConsumptions;

        /// <summary>
        ///     current food level as a normalised value (1 = full, 0 = empty)
        /// </summary>
        public float foodLevel => 1f - (float)_consumptionCount / maxConsumptions;

        private void Start()
        {
            UpdateFoodVisuals();
        }

        /// <summary>
        ///     function to handle player interaction (hand tracking or raycast) to refill the bowl
        /// </summary>
        /// <param name="other">collider that triggered the interaction</param>
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            Refill();
        }

        /// <summary>
        ///     function called when a neko eats from the bowl
        /// </summary>
        /// <returns>true if food was consumed, false if bowl is empty</returns>
        public bool Consume()
        {
            if (!hasFood)
            {
                Debug.Log("ReferenceAREntityBowl: bowl is empty, cannot consume");
                return false;
            }

            _consumptionCount++;
            UpdateFoodVisuals();
            Debug.Log($"ReferenceAREntityBowl: food consumed ({_consumptionCount}/{maxConsumptions})");
            return true;
        }

        /// <summary>
        ///     function to refill the bowl to full capacity
        /// </summary>
        public void Refill()
        {
            _consumptionCount = 0;
            UpdateFoodVisuals();
            Debug.Log("ReferenceAREntityBowl: bowl refilled");
        }

        /// <summary>
        ///     function to update the visual representation based on current consumption count
        /// </summary>
        private void UpdateFoodVisuals()
        {
            if (foodStages == null || foodStages.Length == 0) return;

            var stageIndex = Mathf.Clamp(
                _consumptionCount * foodStages.Length / (maxConsumptions + 1),
                0,
                foodStages.Length - 1);

            for (var i = 0; i < foodStages.Length; i++)
                if (foodStages[i])
                    foodStages[i].SetActive(i == stageIndex);

            Debug.Log($"ReferenceAREntityBowl: showing food stage {stageIndex}");
        }
    }
}