/*
 * author: arwen
 * date: 22/12/2025
 * description:
 * game manager for scenes and audio
 */

using System.Collections;
using PokkatCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     game manager for managing scenes and audio
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>
    ///     singleton instance
    /// </summary>
    public static GameManager Instance;

    // scene mgmt
    public string menuSceneName = "MenuPage";
    public string coreGameplaySceneName = "CoreGameplay";

    // audio sources
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectSource;

    // audio volume
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float effectVolume = 1f;

    private bool loading;

    /// <summary>
    ///     singleton pattern
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeAudioSources();
        Logkat.Out("GameManager initialized and set to persist across scenes");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    ///     create audio sources
    /// </summary>
    private void InitializeAudioSources()
    {
        if (musicSource == null)
        {
            var musicObj = new GameObject("MusicSource");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (effectSource == null)
        {
            var effectObj = new GameObject("SFXSource");
            effectObj.transform.SetParent(transform);
            effectSource = effectObj.AddComponent<AudioSource>();
            effectSource.loop = false;
            effectSource.playOnAwake = false;
        }

        UpdateAudioVolumes();
    }

    /// <summary>
    ///     apply volume settings
    /// </summary>
    private void UpdateAudioVolumes()
    {
        if (musicSource != null) musicSource.volume = masterVolume * musicVolume;

        if (effectSource != null) effectSource.volume = masterVolume * effectVolume;
    }

    /// <summary>
    ///     load menu scene
    /// </summary>
    public void LoadMenuScene()
    {
        LoadScene(menuSceneName);
    }

    /// <summary>
    ///     load core gameplay scene
    /// </summary>
    public void LoadCoreGameplayScene()
    {
        LoadScene(coreGameplaySceneName);
    }

    /// <summary>
    ///     load scene by name
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (loading)
        {
            Logkat.Warn($"Already loading a scene - ignoring request to load {sceneName}");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Logkat.Err("Cannot load scene - scene name is null or empty");
            return;
        }

        Logkat.Out($"Loading scene: {sceneName}");
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    /// <summary>
    ///     load scene asynchronously
    /// </summary>
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        loading = true;

        var asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        if (asyncLoad == null)
        {
            Logkat.Err($"Failed to start loading scene: {sceneName}");
            loading = false;
            yield break;
        }

        while (!asyncLoad.isDone) yield return null;

        Logkat.Out($"Scene loaded: {sceneName}");
        loading = false;
    }

    /// <summary>
    ///     quit application
    /// </summary>
    public void QuitGame()
    {
        Logkat.Out("Quitting game");
        Application.Quit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    ///     play music track
    /// </summary>
    public void PlayMusic(AudioClip musicClip, bool loop = true)
    {
        if (musicClip == null)
        {
            Logkat.Err("Cannot play music - audio clip is null");
            return;
        }

        if (musicSource == null)
        {
            Logkat.Err("Cannot play music - music source is null");
            return;
        }

        musicSource.clip = musicClip;
        musicSource.loop = loop;
        musicSource.Play();

        Logkat.Out($"Playing music: {musicClip.name}");
    }

    /// <summary>
    ///     stop music
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
            Logkat.Out("Music stopped");
        }
    }

    /// <summary>
    ///     pause music
    /// </summary>
    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Pause();
            Logkat.Out("Music paused");
        }
    }

    /// <summary>
    ///     resume music
    /// </summary>
    public void ResumeMusic()
    {
        if (musicSource != null && !musicSource.isPlaying)
        {
            musicSource.UnPause();
            Logkat.Out("Music resumed");
        }
    }

    /// <summary>
    ///     play sound effect
    /// </summary>
    public void PlayEffect(AudioClip effectClip, float volumeScale = 1f)
    {
        if (effectClip == null)
        {
            Logkat.Err("Cannot play SFX - audio clip is null");
            return;
        }

        if (effectSource == null)
        {
            Logkat.Err("Cannot play SFX - SFX source is null");
            return;
        }

        effectSource.PlayOneShot(effectClip, volumeScale);
        Logkat.Dev($"Playing SFX: {effectClip.name}");
    }

    /// <summary>
    ///     play sound effect at position
    /// </summary>
    public void PlayEffectAtLocation(AudioClip effectClip, Vector3 position, float soundVolume = 1f)
    {
        if (effectClip == null)
        {
            Logkat.Err("Cannot play SFX - audio clip is null");
            return;
        }

        var finalVolume = masterVolume * effectVolume * soundVolume;
        AudioSource.PlayClipAtPoint(effectClip, position, finalVolume);
        Logkat.Dev($"Playing SFX at point: {effectClip.name}");
    }

    /// <summary>
    ///     set master volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAudioVolumes();
        Logkat.Out($"Master volume set to: {masterVolume:F2}");
    }

    /// <summary>
    ///     set music volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateAudioVolumes();
        Logkat.Out($"Music volume set to: {musicVolume:F2}");
    }

    /// <summary>
    ///     set sfx volume
    /// </summary>
    public void SetEffectVolume(float volume)
    {
        effectVolume = Mathf.Clamp01(volume);
        UpdateAudioVolumes();
        Logkat.Out($"SFX volume set to: {effectVolume:F2}");
    }

    /// <summary>
    ///     get music volume
    /// </summary>
    public float GetMusicVolume()
    {
        return musicVolume;
    }

    /// <summary>
    ///     get sfx volume
    /// </summary>
    public float GetEffectVolume()
    {
        return effectVolume;
    }

    /// <summary>
    ///     get master volume
    /// </summary>
    public float GetMasterVolume()
    {
        return masterVolume;
    }

    /// <summary>
    ///     load menu
    /// </summary>
    public void OnLoadMenu()
    {
        Logkat.Out("loading menu");
        LoadMenuScene();
    }

    /// <summary>
    ///     loading game scene
    /// </summary>
    public void OnStartGame()
    {
        Logkat.Out("starting game");
        LoadCoreGameplayScene();
    }

    /// <summary>
    ///     close game
    /// </summary>
    public void OnQuit()
    {
        Logkat.Out("quitting");
        QuitGame();
    }

    /// <summary>
    ///     toggle music pause
    /// </summary>
    public void OnButtonToggleMusic()
    {
        if (musicSource != null)
        {
            if (musicSource.isPlaying)
                PauseMusic();
            else
                ResumeMusic();
        }
    }

    /// <summary>
    ///     UI hook to set master volume from slider (can be called from slider OnValueChanged).
    /// </summary>
    public void OnSliderMasterVolume(float volume)
    {
        SetMasterVolume(volume);
    }

    /// <summary>
    ///     UI hook to set music volume from slider (can be called from slider OnValueChanged).
    /// </summary>
    public void OnSliderMusicVolume(float volume)
    {
        SetMusicVolume(volume);
    }

    /// <summary>
    ///     UI hook to set SFX volume from slider (can be called from slider OnValueChanged).
    /// </summary>
    public void OnSliderSFXVolume(float volume)
    {
        SetEffectVolume(volume);
    }
}