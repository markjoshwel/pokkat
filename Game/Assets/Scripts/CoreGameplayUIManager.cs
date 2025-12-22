/*
 * author: arwen
 * date: 22/12/2025
 * description: ui manager
 */
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PokkatCore;
public class CoreGameplayUIManager : MonoBehaviour
{
    [SerializeField] private CoreGameplay coreGameplay;
    [SerializeField] private Image hungerFill;
    [SerializeField] private Image happinessFill;
    [SerializeField] private TMP_Text promptText;

    /// <summary>
    ///     set bars initially
    /// </summary>
    void Start()
    {
        if (!coreGameplay || !coreGameplay.stats) return;
        SetBars(coreGameplay.stats.hunger, coreGameplay.stats.happiness);
    }
    
    /// <summary>
    /// updating hunger + happiness bars
    /// </summary>
    void Update()
    {
        // don't do anything if core gameplay isn't found
        if (!coreGameplay || !coreGameplay.stats) return;
        
        SetBars(coreGameplay.stats.hunger, coreGameplay.stats.happiness);
        
        Logkat.Dev($"CoreGameplayUIManager: coreGameplay.stats.hunger={coreGameplay.stats.hunger}, coreGameplay.stats.happiness={coreGameplay.stats.happiness}");
        
        // updating user instruction prompts
        string prompt = PromptText(coreGameplay.gameState);
        promptText.text = prompt;
        
        //dont show prompt panel if no prompts
        bool showPanel = !string.IsNullOrEmpty(prompt);
        promptText.transform.parent.gameObject.SetActive(showPanel);
    }
    
    void SetBars(float hungerPercent, float happinessPercent)
    {
        hungerFill.fillMethod = Image.FillMethod.Horizontal;
        hungerFill.fillAmount = hungerPercent;

        happinessFill.fillMethod = Image.FillMethod.Horizontal;
        happinessFill.fillAmount = happinessPercent;
    }

    // different prompt text for different neko states
    private string PromptText(CoreGameplayState state)
    {
        switch (state)
        {
            case CoreGameplayState.WaitingForAnything:
            case CoreGameplayState.HasPlaneWaitingForTracker:
                return "Scan the Pokkat tracker to spawn a cat!";
            case CoreGameplayState.HasTrackerWaitingForPlane:
                return "Move your phone around to detect surfaces!";
            case CoreGameplayState.NekoWaitingForPlanes:
                return "Move your phone around to detect more surfaces!";
            case CoreGameplayState.Ok:
                return 
                    "Tap anywhere on your screen to place a bowl!\n"
                    + "Tap your cat to pet and increase happiness!";
            case CoreGameplayState.OkSatiated:
                return "The cat is satiated, you can come back again later!";
            default:
                return "";
        }
    }

    public void OnExitGameButton()
    {
        Logkat.Dev("CoreGameplayUIManager: exiting game");
        GameManager.Instance.OnLoadMenu();
    }

    public void OnPetButton()
    {
        Logkat.Dev("CoreGameplayUIManager: pet");
        coreGameplay.PetNeko();
    }
}
