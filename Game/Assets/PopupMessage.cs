using UnityEngine;
using TMPro;

public class PopupMessage : MonoBehaviour
{
    public TextMeshProUGUI messageText;
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.5f;
    public float displayTime = 2f;

    void Awake()
    {
        canvasGroup.alpha = 0;
    }

    public void ShowMessage(string msg)
    {
        messageText.text = msg;
        StopAllCoroutines();
        StartCoroutine(FadeRoutine());
    }

    private System.Collections.IEnumerator FadeRoutine()
    {
        // Fade in
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
            yield return null;
        }

        // Wait
        yield return new WaitForSeconds(displayTime);

        // Fade out
        t = 0;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeDuration);
            yield return null;
        }
    }
}

