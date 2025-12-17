/*
 * author: mark joshwel
 * date: 17/12/2025
 * description: logger to standardise debug output for the sake of logcat filtering
 */

using System.Runtime.CompilerServices;
using UnityEngine;

namespace PokkatCore
{
    public static class Logkat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Out(string message)
        {
            Debug.Log($"(Pokkat) OUT: {message}");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Err(string message)
        {
            Debug.LogError($"(Pokkat) ERROR: {message}");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warn(string message)
        {
            Debug.LogWarning($"(Pokkat) WARN: {message}");
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Panic(string message)
        {
            Debug.LogError($"(Pokkat) PANIC: {message}");
            throw new System.Exception(message);
        }
    }
}