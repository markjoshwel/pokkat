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

        // get stats from statskeeper (with null safety)
        var hungerText = coreGameplay.stats != null
            ? $"{coreGameplay.stats.hunger * 100:F0}%"
            : "N/A";
        var happinessText = coreGameplay.stats != null
            ? $"{coreGameplay.stats.happiness * 100:F0}%"
            : "N/A";

        interfaceText.text = "Pokkat Core Gameplay\n"
                             + $"Hunger: {hungerText}\n"
                             + $"Happiness: {happinessText}\n"
                             + $"Game State: {coreGameplay.gameState}\n" // here for debugging; please don't put this in the actual ui
                             + $"{message}";
    }
}