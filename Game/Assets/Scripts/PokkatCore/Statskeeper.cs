/*
 * author: mark joshwel
 * date: 11/12/2024
 * description: manages persistent game statistics with JSON serialization
 */

using System;
using System.IO;
using UnityEngine;

namespace PokkatCore
{
    /// <summary>
    ///     serializable structure holding game statistics persisted to disk
    /// </summary>
    [Serializable]
    public struct Statistics
    {
        public float hunger;
        public float happiness;
        public string lastOpenedTimestamp;
    }

    /// <summary>
    ///     manages persistent game statistics including hunger decay over time
    /// </summary>
    public class Statskeeper : MonoBehaviour
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

        private Statistics _statistics;

        /// <summary>
        ///     the current hunger value
        /// </summary>
        public float currentHunger => _statistics.hunger;

        /// <summary>
        ///     the current happiness value
        /// </summary>
        public float currentHappiness => _statistics.happiness;

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
                    _statistics = JsonUtility.FromJson<Statistics>(json);
                    Debug.Log($"Statskeeper: loaded statistics from {_savePath}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Statskeeper: failed to load statistics ({ex.Message})");
                }

            _statistics = new Statistics
            {
                hunger = initialHunger,
                happiness = initialHappiness,
                lastOpenedTimestamp = DateTime.UtcNow.ToString("O")
            };

            Debug.Log("Statskeeper: created default statistics");
        }

        /// <summary>
        ///     function to save current statistics to disk with updated timestamp
        /// </summary>
        public void SaveStatistics()
        {
            _statistics.lastOpenedTimestamp = DateTime.UtcNow.ToString("O");

            try
            {
                var json = JsonUtility.ToJson(_statistics, true);
                File.WriteAllText(_savePath, json);
                Debug.Log($"Statskeeper: saved statistics to {_savePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Statskeeper: failed to save statistics ({ex.Message})");
            }
        }

        /// <summary>
        ///     function to calculate and apply hunger decay based on time elapsed since last session
        /// </summary>
        private void ApplyTimePassed()
        {
            if (string.IsNullOrEmpty(_statistics.lastOpenedTimestamp)) return;

            try
            {
                var lastTime = DateTime.Parse(_statistics.lastOpenedTimestamp);
                var hoursPassed = (DateTime.UtcNow - lastTime).TotalHours;
                var hungerDecay = (float)(hoursPassed * hungerDecayRatePerHour);

                _statistics.hunger = Mathf.Max(0f, _statistics.hunger - hungerDecay);
                Debug.Log($"Statskeeper: applied {hungerDecay:F1} hunger decay for {hoursPassed:F2} hours passed");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Statskeeper: failed to parse timestamp ({ex.Message})");
            }
        }

        /// <summary>
        ///     function to reduce hunger by the specified amount
        /// </summary>
        /// <param name="amount">amount to decrease hunger by</param>
        public void DecreaseHunger(float amount)
        {
            _statistics.hunger = Mathf.Max(0f, _statistics.hunger - amount);
            Debug.Log($"Statskeeper: hunger decreased by {amount:F1}, now {_statistics.hunger:F1}");
        }

        /// <summary>
        ///     function to increase hunger (when fed) by the specified amount
        /// </summary>
        /// <param name="amount">amount to increase hunger by</param>
        public void IncreaseHunger(float amount)
        {
            _statistics.hunger = Mathf.Min(maxHunger, _statistics.hunger + amount);
            Debug.Log($"Statskeeper: hunger increased by {amount:F1}, now {_statistics.hunger:F1}");
        }

        /// <summary>
        ///     function to modify happiness by the specified delta (positive or negative)
        /// </summary>
        /// <param name="delta">amount to change happiness by</param>
        public void ModifyHappiness(float delta)
        {
            _statistics.happiness = Mathf.Clamp(_statistics.happiness + delta, 0f, maxHappiness);
            Debug.Log($"Statskeeper: happiness modified by {delta:F1}, now {_statistics.happiness:F1}");
        }
    }
}