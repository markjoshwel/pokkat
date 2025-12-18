/*
 * author: mark joshwel
 * date: 17/12/2025
 * description: logger to standardise debug output for the sake of logcat filtering
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PokkatCore
{
    public static class Logkat
    {
        /// <summary>
        ///     whether verbose/development logging is enabled
        /// </summary>
        public const bool VerboseLogging = true;

        /// <summary>
        ///     minimum seconds between identical log messages (spam prevention)
        /// </summary>
        private const float RepeatCooldownSeconds = 1.0f;

        /// <summary>
        ///     cache of recent messages with their last log time
        /// </summary>
        private static readonly Dictionary<string, float> RecentMessages = new();

        /// <summary>
        ///     checks if a message was logged recently (within cooldown period)
        /// </summary>
        private static bool HasRecentlyBeenLogged(string message)
        {
            var currentTime = Time.unscaledTime;

            // check if message exists in cache and is within cooldown
            if (RecentMessages.TryGetValue(message, out var lastTime))
                if (currentTime - lastTime < RepeatCooldownSeconds)
                    return true;

            // update cache with current time
            RecentMessages[message] = currentTime;

            // periodically clean old entries to prevent unbounded growth
            if (RecentMessages.Count > 100)
                CleanOldEntries(currentTime);

            return false;
        }

        /// <summary>
        ///     removes cache entries older than cooldown period
        /// </summary>
        private static void CleanOldEntries(float currentTime)
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in RecentMessages)
                if (currentTime - kvp.Value >= RepeatCooldownSeconds)
                    keysToRemove.Add(kvp.Key);

            foreach (var key in keysToRemove)
                RecentMessages.Remove(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Out(string message)
        {
            if (HasRecentlyBeenLogged(message)) return;
            Debug.Log($"(Pokkat) OUT: {message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Dev(string message)
        {
            if (!VerboseLogging) return;
            if (HasRecentlyBeenLogged(message)) return;
            Debug.Log($"(Pokkat Verbose) DEV: {message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Err(string message)
        {
            if (HasRecentlyBeenLogged(message)) return;
            Debug.LogError($"(Pokkat) ERROR: {message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warn(string message)
        {
            if (HasRecentlyBeenLogged(message)) return;
            Debug.LogWarning($"(Pokkat) WARN: {message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Panic(string message)
        {
            // panic always logs - never suppressed
            Debug.LogError($"(Pokkat) PANIC: {message}");
            throw new Exception(message);
        }
    }
}