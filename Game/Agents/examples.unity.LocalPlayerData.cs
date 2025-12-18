using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
///     local player data structure/class
/// </summary>
public class LocalPlayerData
{
    /// <summary>
    ///     maximum number of the best online scores to keep track of
    /// </summary>
    public const int MaxBestScores = 10;

    /// <summary>
    ///     maximum number of recent local scores to keep track of
    /// </summary>
    public const int MaxRecentScores = 10;

    /// <summary>
    ///     the gamma value used in the exponential user rating calculation
    /// </summary>
    private const float ExponentialUserRatingGamma = 2f;

    /// <summary>
    ///     queue of the best online scores,
    ///     used in user rating calculation and accuracy display stats
    /// </summary>
    public readonly Queue<Score> BestOnlineScores = new(20);

    /// <summary>
    ///     queue of the 10 most recent local scores
    /// </summary>
    public readonly Queue<Score> RecentLocalScores = new(10);

    /// <summary>
    ///     queue of the 10 most recent online scores,
    ///     used in user rating calculation and accuracy display stats
    /// </summary>
    public readonly Queue<Score> RecentOnlineScores = new(10);

    /// <summary>
    ///     last known email used
    /// </summary>
    public string LastKnownEmail = "";

    /// <summary>
    ///     last known username used
    /// </summary>
    public string LastKnownUsername = "Guest";

    /// <summary>
    ///     loads player data from player prefs and database
    /// </summary>
    public void LoadFromTheWorld(Action<LocalPlayerData> callback)
    {
        // load user data, possibly from the backend
        var possibleUser = GameManager.Instance.Backend.GetUser();
        var currentKnownEmail = string.Empty;
        var currentKnownUsername = string.Empty;
        if (possibleUser != null)
        {
            currentKnownEmail = possibleUser.Email;
            currentKnownUsername = GameManager.Instance.Backend.GetUsername();
        }

        var lastStoredEmail = PlayerPrefs.GetString("LastKnownEmail", "");
        var lastStoredUsername = PlayerPrefs.GetString("LastKnownUsername", "Guest");
        LastKnownEmail = string.IsNullOrEmpty(currentKnownEmail) ? lastStoredEmail : currentKnownEmail;
        LastKnownUsername = string.IsNullOrEmpty(currentKnownUsername) ? lastStoredUsername : currentKnownUsername;

        // load local scores
        RecentLocalScores.Clear();
        for (var idx = 0; idx < 10; idx++)
        {
            var timestampRaw = PlayerPrefs.GetString($"RecentLocalScores_{idx}_Timestamp", "");
            if (timestampRaw == "") continue;

            var timestamp = DateTime.TryParseExact(timestampRaw,
                "s",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var t)
                ? t
                : DateTime.MinValue;

            if (timestamp == DateTime.MinValue) continue;

            var noOfRounds = PlayerPrefs.GetInt($"RecentLocalScores_{idx}_NoOfRounds", -1);
            var l = PlayerPrefs.GetFloat($"RecentLocalScores_{idx}_AvgLightnessAccuracy", -1f);
            var c = PlayerPrefs.GetFloat($"RecentLocalScores_{idx}_AvgChromaAccuracy", -1f);
            var h = PlayerPrefs.GetFloat($"RecentLocalScores_{idx}_AvgHueAccuracy", -1f);
            var e = PlayerPrefs.GetFloat($"RecentLocalScores_{idx}_AvgPerceivedAccuracy", -1f);

            // if any of the values are invalid, don't add the score
            if (noOfRounds < 0 || l < 0 || c < 0 || h < 0 || e < 0) continue;

            RegisterLocalScore(new Score(timestamp, Math.Max(1, noOfRounds), Math.Clamp(l, 0f, 100f),
                Math.Clamp(c, 0f, 100f), Math.Clamp(h, 0f, 100f)));
        }

        Debug.Log(
            $"loaded lpdata from the local world ({LastKnownUsername} <{LastKnownEmail}> with RLS.Count={RecentLocalScores.Count}, ROS.Count={RecentOnlineScores.Count}");
        callback(this);

        // load online scores
        RecentOnlineScores.Clear();
        GameManager.Instance.Backend.GetRecentScores((_, recentOnlineScores) =>
        {
            foreach (var onlineScore in recentOnlineScores)
            {
                if (RecentOnlineScores.Count > 10) RecentOnlineScores.Dequeue();
                RecentOnlineScores.Enqueue(onlineScore);
            }

            Debug.Log(
                $"loaded lpdata from the online world (now {LastKnownUsername} <{LastKnownEmail}> with RLS.Count={RecentLocalScores.Count}, ROS.Count={RecentOnlineScores.Count}");

            callback(this);
        });
    }

    /// <summary>
    ///     saves player data to player prefs
    /// </summary>
    public void SaveToTheWorld()
    {
        PlayerPrefs.SetString("LastKnownEmail", LastKnownEmail);
        PlayerPrefs.SetString("LastKnownUsername", LastKnownUsername);

        var idx = 0;
        foreach (var score in RecentLocalScores)
        {
            PlayerPrefs.SetString($"RecentLocalScores_{idx}_Timestamp",
                score.Timestamp.ToString("s", CultureInfo.InvariantCulture));
            PlayerPrefs.SetInt($"RecentLocalScores_{idx}_NoOfRounds", score.NoOfRounds);
            PlayerPrefs.SetFloat($"RecentLocalScores_{idx}_AvgLightnessAccuracy", score.AvgLightnessAccuracy);
            PlayerPrefs.SetFloat($"RecentLocalScores_{idx}_AvgChromaAccuracy", score.AvgChromaAccuracy);
            PlayerPrefs.SetFloat($"RecentLocalScores_{idx}_AvgHueAccuracy", score.AvgHueAccuracy);
            PlayerPrefs.SetFloat($"RecentLocalScores_{idx}_AvgPerceivedAccuracy", score.AvgPerceivedAccuracy);
            idx++;
        }

        Debug.Log("saved lpdata to player prefs"); // online scores are already saved in the backend
    }

    /// <summary>
    ///     registers a score to the player's local data
    /// </summary>
    /// <param name="score">the score to register</param>
    public void RegisterLocalScore(Score score)
    {
        while (RecentLocalScores.Count >= MaxRecentScores) RecentLocalScores.Dequeue();
        RecentLocalScores.Enqueue(score);
    }

    /// <summary>
    ///     scaling function for user ratings
    /// </summary>
    /// <returns>the scaled user rating <c>double</c></returns>
    public static double UserRatingScalingF(double rating)
    {
        var rawUserRating = Math.Clamp(rating, 0d, 100d);
        var exponentialRating = 100d * Math.Pow(rawUserRating / 100d, ExponentialUserRatingGamma);
        return Math.Clamp(exponentialRating, 0d, 100d);
    }

    /// <summary>
    ///     calculates the user rating based on whatever local data is available
    /// </summary>
    /// <returns>the user rating (0-100f)</returns>
    public float CalculateUserRating()
    {
        // user rating is like CHUNITHMs rating system
        // where best + recent scores are averaged out
        // in this case 20 best scores, and 10 recent scores are used

        // ensure the scores don't exceed their arbitrary limits
        while (RecentOnlineScores.Count > MaxRecentScores) RecentOnlineScores.Dequeue();
        while (RecentLocalScores.Count > MaxRecentScores) RecentLocalScores.Dequeue();
        while (BestOnlineScores.Count > MaxBestScores) BestOnlineScores.Dequeue();

        // if online scores are available, use them
        var recentScores = RecentOnlineScores.Count > 0 ? RecentOnlineScores : RecentLocalScores;
        var bestScores = BestOnlineScores;

        var totalRating = 0d;

        foreach (var score in recentScores.Take(MaxRecentScores))
            totalRating += (score.AvgLightnessAccuracy + score.AvgChromaAccuracy + score.AvgHueAccuracy +
                            score.AvgPerceivedAccuracy) / 4d;

        foreach (var score in bestScores.Take(MaxBestScores))
            totalRating += (score.AvgLightnessAccuracy + score.AvgChromaAccuracy + score.AvgHueAccuracy +
                            score.AvgPerceivedAccuracy) / 4d;

        var rating = UserRatingScalingF(totalRating /= MaxRecentScores + MaxBestScores);
        Debug.Log($"locally calculated user rating: lin: {totalRating} -> exp: {rating}");

        return (float)rating;
    }

    /// <summary>
    ///     safely get a float value from a dictionary
    /// </summary>
    /// <param name="data">the dictionary to get the value from</param>
    /// <param name="key">the key to get the value from</param>
    /// <returns>the float value</returns>
    /// <exception cref="ArgumentException">thrown if the key is not found, or the value is not a valid float</exception>
    public static float GetFloatyKey(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var possibleFloat)) throw new ArgumentException($"{key} not found");
        return possibleFloat switch
        {
            double f => (float)f,
            long l => l,
            _ => throw new ArgumentException($"data['{key}'] not a valid float")
        };
    }

    public struct Score
    {
        /// <summary>
        ///     timestamp of the score
        /// </summary>
        public DateTime Timestamp;

        /// <summary>
        ///     number of rounds played (0-100)
        /// </summary>
        public readonly int NoOfRounds;

        /// <summary>
        ///     average lightness accuracy across all rounds (0-100)
        /// </summary>
        public readonly float AvgLightnessAccuracy;

        /// <summary>
        ///     average chroma accuracy across all rounds (0-100)
        /// </summary>
        public readonly float AvgChromaAccuracy;

        /// <summary>
        ///     average hue accuracy across all rounds (0-100)
        /// </summary>
        public readonly float AvgHueAccuracy;

        /// <summary>
        ///     average perceived accuracy across all rounds (0-100)
        /// </summary>
        public readonly float AvgPerceivedAccuracy;

        /// <summary>
        ///     constructor for the score struct
        /// </summary>
        /// <param name="timestamp">timestamp of the score</param>
        /// <param name="noOfRounds">number of rounds played (0-100)</param>
        /// <param name="l">average lightness accuracy across all rounds (0-100)</param>
        /// <param name="c">average chroma accuracy across all rounds (0-100)</param>
        /// <param name="h">average hue accuracy across all rounds (0-100)</param>
        /// ///
        /// <param name="e">average perceived accuracy across all rounds (0-100)</param>
        public Score(DateTime timestamp = new(), int noOfRounds = 1, float l = 100.0f, float c = 100.0f,
            float h = 100.0f, float e = 100.0f)
        {
            Timestamp = timestamp;
            NoOfRounds = noOfRounds;
            AvgLightnessAccuracy = l;
            AvgChromaAccuracy = c;
            AvgHueAccuracy = h;
            AvgPerceivedAccuracy = e;
        }

        /// <summary>
        ///     dict-based constructor for the score struct
        /// </summary>
        /// <param name="data">dictionary of the score data</param>
        /// <exception cref="ArgumentException">thrown if the dictionary is malformed or missing data</exception>
        public Score(Dictionary<string, object> data)
        {
            // try to safely construct the score from a backend-provided dictionary
            // for each value, if it's not found, or not a valid value, throw an exception

            if (!data.ContainsKey("timestamp") || data["timestamp"] is not long timestamp)
                throw new ArgumentException("data['timestamp'] not found or invalid");
            if (!data.ContainsKey("noOfRounds") || data["noOfRounds"] is not long noOfRounds)
                throw new ArgumentException("data['noOfRounds'] not found or invalid");

            var avgLightnessAccuracy = GetFloatyKey(data, "avgLightnessAccuracy");
            var avgChromaAccuracy = GetFloatyKey(data, "avgChromaAccuracy");
            var avgHueAccuracy = GetFloatyKey(data, "avgHueAccuracy");
            var avgPerceivedAccuracy = GetFloatyKey(data, "avgPerceivedAccuracy");

            Timestamp = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            NoOfRounds = (int)noOfRounds;
            AvgLightnessAccuracy = avgLightnessAccuracy;
            AvgChromaAccuracy = avgChromaAccuracy;
            AvgHueAccuracy = avgHueAccuracy;
            AvgPerceivedAccuracy = avgPerceivedAccuracy;
        }

        /// <summary>
        ///     converts the score struct to a dictionary safe for the backend
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "timestamp", new DateTimeOffset(Timestamp).ToUnixTimeSeconds() },
                { "noOfRounds", NoOfRounds },
                { "avgLightnessAccuracy", AvgLightnessAccuracy },
                { "avgChromaAccuracy", AvgChromaAccuracy },
                { "avgHueAccuracy", AvgHueAccuracy },
                { "avgPerceivedAccuracy", AvgPerceivedAccuracy }
            };
        }

        /// <summary>
        ///     converts the score struct to a dictionary safe for the backend
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> ToDictionary(string userId)
        {
            return new Dictionary<string, object>
            {
                { "userId", userId },
                { "timestamp", new DateTimeOffset(Timestamp).ToUnixTimeSeconds() },
                { "noOfRounds", NoOfRounds },
                { "avgLightnessAccuracy", AvgLightnessAccuracy },
                { "avgChromaAccuracy", AvgChromaAccuracy },
                { "avgHueAccuracy", AvgHueAccuracy },
                { "avgPerceivedAccuracy", AvgPerceivedAccuracy }
            };
        }
    }
}
