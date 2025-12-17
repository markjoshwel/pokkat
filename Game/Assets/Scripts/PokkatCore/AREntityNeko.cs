/*
 * author: mark joshwel
 * date: 11/12/2025
 * description: neko character with state machine, NavMesh navigation, and procedural animations
 */

using UnityEngine;

namespace PokkatCore
{
    public class AREntityNeko : MonoBehaviour
    {
        private void Awake()
        {
            Logkat.Out("AREntityNeko: Awake/Setup OK");
        }

        private void Start()
        {
            Logkat.Out("AREntityNeko: Start/Configure OK");
        }
    }
}