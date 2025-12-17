/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: manages persistent game statistics with hooks for local
 *              and firebase-based storage
 */

using UnityEngine;

namespace PokkatCore
{
    public class Statskeeper : MonoBehaviour
    {
        private void Awake()
        {
            Logkat.Out("Statskeeper: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("Statskeeper: Start/Configure OK");
        }
    }
}