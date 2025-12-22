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
    /// updating hunger + happiness bars
    /// </summary>
    void Update()
    {
        // don't do anything if core gameplay isn't found
        if (coreGameplay == null || coreGameplay.stats == null)
            return;
        
        // adjust hunger + happiness bars according to percentage
        hungerFill.fillAmount = coreGameplay.stats.hunger;
        happinessFill.fillAmount = coreGameplay.stats.happiness;
        
        // updating user instruction prompts
        string prompt = PromptText(coreGameplay.gameState);
        promptText.text = prompt;
        
        //dont show prompt panel if no prompts
        bool showPanel = !string.IsNullOrEmpty(prompt);
        promptText.transform.parent.gameObject.SetActive(showPanel);
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
            default:
                return "";
        }
    }

    public void OnExitGameButton()
    {
        Logkat.Dev("exiting game");
        GameManager.Instance.OnLoadMenu();
    }

    public void OnPetButton()
    {
        Logkat.Dev("pet ui button pressed");
        coreGameplay.PetNeko();
    }
}
