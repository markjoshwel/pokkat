using PokkatCore;
using TMPro;
using UnityEngine;

public class CoreGameplayInterfaceInterop : MonoBehaviour
{
    [SerializeField] private TMP_Text interfaceText;

    [SerializeField] private CoreGameplay coreGameplay;

    private void Awake()
    {
        if (!interfaceText) Logkat.Panic("interface text not assigned");
        if (!coreGameplay) Logkat.Panic("core gameplay not assigned");
    }

    private void Update()
    {
        var message = "";
        switch (coreGameplay.gameState)
        {
            case CoreGameplayState.WaitingForAnything:
            case CoreGameplayState.HasPlaneWaitingForTracker:
                message = "Scan the Pokkat tracker to spawn a cat!";
                break;
            case CoreGameplayState.HasTrackerWaitingForPlane:
                message = "Move your phone around to detect surfaces!";
                break;
            case CoreGameplayState.NekoWaitingForPlanes:
                message = "Move your phone around to detect more surfaces!";
                break;
            case CoreGameplayState.Ok:
                message = "";
                break;
            default:
                Logkat.Panic("unreachable");
                break;
        }

        interfaceText.text = "Pokkat Core Gameplay\n"
                             + "Hunger: (Not Implemented Yet)\n"
                             + "Happiness: (Not Implemented Yet)\n"
                             + $"Game State: {coreGameplay.gameState}\n" // here for debugging; please don't put this in the actual ui
                             + $"{message}";
    }
}