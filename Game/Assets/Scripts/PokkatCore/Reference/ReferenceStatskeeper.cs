/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: manages persistent game statistics with JSON serialization
 */

using System;
using System.IO;
using UnityEngine;


namespace PokkatCore.Reference
{
    /// <summary>
    ///     serializable structure holding game statistics persisted to disk
    /// </summary>
    [Serializable]
    public struct ReferenceStatisticsStruct
    {
        public float hunger;
        public float happiness;
        public string lastOpenedTimestamp;
    }

    /// <summary>
    ///     manages persistent game statistics including hunger decay over time
    /// </summary>
    public class ReferenceStatskeeper : MonoBehaviour
    {
        private const string SaveFileName = "pokkat_stats.json";

        [Header("Hunger Settings")] [Tooltip("Maximum hunger value.")] [SerializeField]
        private float maxHunger = 100f;

        [Tooltip("Hunger decay rate per hour when app is closed.")] [SerializeField]
        private float hungerDecayRatePerHour = 2f;

        [Tooltip("Initial hunger value for new saves.")] [SerializeField]
        private float initialHunger = 80f;

        [Header("Happiness Settings")] [Tooltip("Maximum happiness value.")] [SerializeField]
        private float maxHappiness = 100f;

        [Tooltip("Initial happiness value for new saves.")] [SerializeField]
        private float initialHappiness = 70f;

        private string _savePath;

        private ReferenceStatisticsStruct _referenceStatisticsStruct;

        /// <summary>
        ///     the current hunger value
        /// </summary>
        public float currentHunger => _referenceStatisticsStruct.hunger;

        /// <summary>
        ///     the current happiness value
        /// </summary>
        public float currentHappiness => _referenceStatisticsStruct.happiness;

        /// <summary>
        ///     the maximum hunger value
        /// </summary>
        public float currentMaxHunger => maxHunger;

        /// <summary>
        ///     the maximum happiness value
        /// </summary>
        public float currentMaxHappiness => maxHappiness;

        private void Awake()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            LoadStatistics();
        }

        private void Start()
        {
            ApplyTimePassed();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveStatistics();
        }

        private void OnApplicationQuit()
        {
            SaveStatistics();
        }

        /// <summary>
        ///     function to load statistics from disk or create default values if missing
        /// </summary>
        private void LoadStatistics()
        {
            if (File.Exists(_savePath))
                try
                {
                    var json = File.ReadAllText(_savePath);
                    _referenceStatisticsStruct = JsonUtility.FromJson<ReferenceStatisticsStruct>(json);
                    Debug.Log($"ReferenceStatskeeper: loaded statistics from {_savePath}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"ReferenceStatskeeper: failed to load statistics ({ex.Message})");
                }

            _referenceStatisticsStruct = new ReferenceStatisticsStruct
            {
                hunger = initialHunger,
                happiness = initialHappiness,
                lastOpenedTimestamp = DateTime.UtcNow.ToString("O")
            };

            Debug.Log("ReferenceStatskeeper: created default statistics");
        }

        /// <summary>
        ///     function to save current statistics to disk with updated timestamp
        /// </summary>
        public void SaveStatistics()
        {
            _referenceStatisticsStruct.lastOpenedTimestamp = DateTime.UtcNow.ToString("O");

            try
            {
                var json = JsonUtility.ToJson(_referenceStatisticsStruct, true);
                File.WriteAllText(_savePath, json);
                Debug.Log($"ReferenceStatskeeper: saved statistics to {_savePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ReferenceStatskeeper: failed to save statistics ({ex.Message})");
            }
        }

        /// <summary>
        ///     function to calculate and apply hunger decay based on time elapsed since last session
        /// </summary>
        private void ApplyTimePassed()
        {
            if (string.IsNullOrEmpty(_referenceStatisticsStruct.lastOpenedTimestamp)) return;

            try
            {
                var lastTime = DateTime.Parse(_referenceStatisticsStruct.lastOpenedTimestamp);
                var hoursPassed = (DateTime.UtcNow - lastTime).TotalHours;
                var hungerDecay = (float)(hoursPassed * hungerDecayRatePerHour);

                _referenceStatisticsStruct.hunger = Mathf.Max(0f, _referenceStatisticsStruct.hunger - hungerDecay);
                Debug.Log($"ReferenceStatskeeper: applied {hungerDecay:F1} hunger decay for {hoursPassed:F2} hours passed");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ReferenceStatskeeper: failed to parse timestamp ({ex.Message})");
            }
        }

        /// <summary>
        ///     function to reduce hunger by the specified amount
        /// </summary>
        /// <param name="amount">amount to decrease hunger by</param>
        public void DecreaseHunger(float amount)
        {
            _referenceStatisticsStruct.hunger = Mathf.Max(0f, _referenceStatisticsStruct.hunger - amount);
            Debug.Log($"ReferenceStatskeeper: hunger decreased by {amount:F1}, now {_referenceStatisticsStruct.hunger:F1}");
        }

        /// <summary>
        ///     function to increase hunger (when fed) by the specified amount
        /// </summary>
        /// <param name="amount">amount to increase hunger by</param>
        public void IncreaseHunger(float amount)
        {
            _referenceStatisticsStruct.hunger = Mathf.Min(maxHunger, _referenceStatisticsStruct.hunger + amount);
            Debug.Log($"ReferenceStatskeeper: hunger increased by {amount:F1}, now {_referenceStatisticsStruct.hunger:F1}");
        }

        /// <summary>
        ///     function to modify happiness by the specified delta (positive or negative)
        /// </summary>
        /// <param name="delta">amount to change happiness by</param>
        public void ModifyHappiness(float delta)
        {
            _referenceStatisticsStruct.happiness = Mathf.Clamp(_referenceStatisticsStruct.happiness + delta, 0f, maxHappiness);
            Debug.Log($"ReferenceStatskeeper: happiness modified by {delta:F1}, now {_referenceStatisticsStruct.happiness:F1}");
        }
    }
}