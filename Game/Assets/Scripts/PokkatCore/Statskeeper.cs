/*
 * author: mark joshwel
 * date: 19/12/2025
 * description: manages persistent game statistics (hunger, happiness) with time decay,
 *              local PlayerPrefs persistence, and backend integration hooks for Firebase sync.
 *              tamagotchi-style virtual pet stat system
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PokkatCore
{
    /// <summary>
    ///     persistent stat keeper for tamagotchi-style hunger/happiness tracking.
    ///     fires OnStatsChanged after modifications for backend upload,
    ///     fires OnLoad on Start() for backend to override with LoadFromDict()
    /// </summary>
    public class Statskeeper : MonoBehaviour
    {
        #region Time Decay

        /// <summary>
        ///     applies time-based decay to stats based on hours elapsed since last update.
        ///     hunger and happiness decrease by 0.1 per hour, floored at 0
        /// </summary>
        private void ApplyTimeDecay()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastUpdateTimestamp;
            var hours = (float)elapsed.TotalHours;

            if (hours <= 0) return;

            var decay = hours * DecayPerHour;
            var oldHunger = hunger;
            var oldHappiness = happiness;

            hunger = Mathf.Max(0f, hunger - decay);
            happiness = Mathf.Max(0f, happiness - decay);
            _lastUpdateTimestamp = now;

            Logkat.Dev($"Statskeeper: ApplyTimeDecay, hours={hours:F2}, decay={decay:F2}, " +
                       $"hunger={oldHunger:F2}->{hunger:F2}, happiness={oldHappiness:F2}->{happiness:F2}");

            if (isDead)
                Logkat.Warn("Statskeeper: neko is dead (hunger=0)");
        }

        #endregion

        #region Constants

        private const string PrefsKeyHunger = "Pokkat_Hunger";
        private const string PrefsKeyHappiness = "Pokkat_Happiness";
        private const string PrefsKeyTimestamp = "Pokkat_LastUpdate";
        private const string PrefsKeyTextureId = "Pokkat_TextureId";

        private const float DecayPerHour = 0.1f;
        private const float FeedAmount = 0.5f;
        private const float PlayAmount = 0.5f;
        private const float PetAmount = 0.05f;
        private const int DefaultTextureId = 22;

        #endregion

        #region Private Fields

        private DateTime _lastUpdateTimestamp = DateTime.UtcNow;

        #endregion

        #region Public Properties

        /// <summary>
        ///     current hunger level (0-1, 0 = dead)
        /// </summary>
        public float hunger { get; private set; } = 1f;

        /// <summary>
        ///     current happiness level (0-1)
        /// </summary>
        public float happiness { get; private set; } = 1f;

        /// <summary>
        ///     timestamp of last stat update (UTC)
        /// </summary>
        public DateTime lastUpdateTimestamp => _lastUpdateTimestamp;

        /// <summary>
        ///     stored neko texture id for persistence across sessions
        /// </summary>
        public int nekoTextureId { get; private set; } = DefaultTextureId;

        /// <summary>
        ///     whether the neko is dead (hunger reached 0)
        /// </summary>
        public bool isDead => hunger <= 0f;

        #endregion

        #region Events

        /// <summary>
        ///     fired after any stat modification - backend can subscribe to upload data.
        ///     backend should call SaveToDict() to get serialisable data
        /// </summary>
        public event Action<Statskeeper> OnStatsChanged;

        /// <summary>
        ///     fired on Start() after local load - backend can subscribe to override with LoadFromDict().
        ///     backend should fetch from Firebase and call LoadFromDict() if data exists
        /// </summary>
        public event Action<Statskeeper> OnLoad;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Logkat.Out("Statskeeper: Awake/Setup OK");
        }

        private void Start()
        {
            LoadLocally();
            Logkat.Out("Statskeeper: Start/Configure OK");

            // fire OnLoad so backend can override with LoadFromDict()
            OnLoad?.Invoke(this);
        }

        #endregion

        #region Stat Modification Methods

        /// <summary>
        ///     record that the neko was fed - increases hunger by 0.5
        /// </summary>
        public void RecordFed()
        {
            hunger = Mathf.Clamp01(hunger + FeedAmount);
            _lastUpdateTimestamp = DateTime.UtcNow;
            SaveLocally();
            OnStatsChanged?.Invoke(this);
            Logkat.Dev($"Statskeeper: RecordFed, hunger={hunger:F2}");
        }

        /// <summary>
        ///     record that the neko played with a friend - increases happiness by 0.5
        /// </summary>
        public void RecordPlayedWithFriend()
        {
            happiness = Mathf.Clamp01(happiness + PlayAmount);
            _lastUpdateTimestamp = DateTime.UtcNow;
            SaveLocally();
            OnStatsChanged?.Invoke(this);
            Logkat.Dev($"Statskeeper: RecordPlayedWithFriend, happiness={happiness:F2}");
        }

        /// <summary>
        ///     record that the neko was petted - increases happiness by 0.05
        /// </summary>
        public void RecordPetted()
        {
            happiness = Mathf.Clamp01(happiness + PetAmount);
            _lastUpdateTimestamp = DateTime.UtcNow;
            SaveLocally();
            OnStatsChanged?.Invoke(this);
            Logkat.Dev($"Statskeeper: RecordPetted, happiness={happiness:F2}");
        }

        /// <summary>
        ///     sets the neko texture id for persistence
        /// </summary>
        public void SetTextureId(int textureId)
        {
            nekoTextureId = textureId;
            SaveLocally();
            OnStatsChanged?.Invoke(this);
            Logkat.Dev($"Statskeeper: SetTextureId={textureId}");
        }

        #endregion

        #region Local Persistence

        /// <summary>
        ///     saves current stats to PlayerPrefs
        /// </summary>
        private void SaveLocally()
        {
            PlayerPrefs.SetFloat(PrefsKeyHunger, hunger);
            PlayerPrefs.SetFloat(PrefsKeyHappiness, happiness);
            PlayerPrefs.SetString(PrefsKeyTimestamp, _lastUpdateTimestamp.ToString("o", CultureInfo.InvariantCulture));
            PlayerPrefs.SetInt(PrefsKeyTextureId, nekoTextureId);
            PlayerPrefs.Save();
            Logkat.Dev($"Statskeeper: SaveLocally OK, hunger={hunger:F2}, happiness={happiness:F2}, " +
                       $"textureId={nekoTextureId}, timestamp={_lastUpdateTimestamp:o}");
        }

        /// <summary>
        ///     loads stats from PlayerPrefs and applies time decay
        /// </summary>
        private void LoadLocally()
        {
            // check if we have saved data
            if (!PlayerPrefs.HasKey(PrefsKeyHunger))
            {
                Logkat.Dev("Statskeeper: no local data found, initialising with 100% stats");
                hunger = 1f;
                happiness = 1f;
                _lastUpdateTimestamp = DateTime.UtcNow;
                nekoTextureId = DefaultTextureId;
                Logkat.Dev($"Statskeeper: initialised defaults, hunger={hunger:F2}, happiness={happiness:F2}, " +
                           $"textureId={nekoTextureId}, timestamp={_lastUpdateTimestamp:o}");
                SaveLocally();
                return;
            }

            hunger = PlayerPrefs.GetFloat(PrefsKeyHunger, 1f);
            happiness = PlayerPrefs.GetFloat(PrefsKeyHappiness, 1f);
            nekoTextureId = PlayerPrefs.GetInt(PrefsKeyTextureId, DefaultTextureId);

            var timestampStr = PlayerPrefs.GetString(PrefsKeyTimestamp, "");
            if (DateTime.TryParse(timestampStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out var parsed))
                _lastUpdateTimestamp = parsed;
            else
                _lastUpdateTimestamp = DateTime.UtcNow;

            Logkat.Dev(
                $"Statskeeper: LoadLocally, hunger={hunger:F2}, happiness={happiness:F2}, " +
                $"textureId={nekoTextureId}, timestamp={_lastUpdateTimestamp:o}");

            // apply decay for time passed while app was closed
            ApplyTimeDecay();
        }

        #endregion

        #region Backend Serialisation

        /// <summary>
        ///     serialises current stats to dictionary for backend upload.
        ///     timestamp is stored as Unix epoch (seconds) for Firebase compatibility
        /// </summary>
        public Dictionary<string, object> SaveToDict()
        {
            return new Dictionary<string, object>
            {
                { "hunger", hunger },
                { "happiness", happiness },
                { "timestamp", new DateTimeOffset(_lastUpdateTimestamp).ToUnixTimeSeconds() },
                { "textureId", nekoTextureId }
            };
        }

        /// <summary>
        ///     loads stats from backend dictionary, applies time decay, saves locally.
        ///     called by backend after fetching from Firebase to override local data
        /// </summary>
        public void LoadFromDict(Dictionary<string, object> data)
        {
            if (data == null)
            {
                Logkat.Warn("Statskeeper: LoadFromDict called with null data");
                return;
            }

            // parse hunger
            if (data.TryGetValue("hunger", out var hungerObj))
                hunger = Convert.ToSingle(hungerObj);

            // parse happiness
            if (data.TryGetValue("happiness", out var happinessObj))
                happiness = Convert.ToSingle(happinessObj);

            // parse timestamp (Unix epoch seconds)
            if (data.TryGetValue("timestamp", out var timestampObj))
            {
                var epochSeconds = Convert.ToInt64(timestampObj);
                _lastUpdateTimestamp = DateTimeOffset.FromUnixTimeSeconds(epochSeconds).UtcDateTime;
            }

            // parse texture id
            if (data.TryGetValue("textureId", out var textureIdObj))
                nekoTextureId = Convert.ToInt32(textureIdObj);

            Logkat.Dev(
                $"Statskeeper: LoadFromDict, hunger={hunger:F2}, happiness={happiness:F2}, textureId={nekoTextureId}");

            // apply decay for time passed since backend data was saved
            ApplyTimeDecay();

            // save to local and notify listeners
            SaveLocally();
            OnStatsChanged?.Invoke(this);
        }

        #endregion
    }
}