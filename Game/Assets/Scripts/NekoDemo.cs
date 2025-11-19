/*
 * author: mark joshwel
 * date: 16/11/2025
 * description: cycles through the neko texture pairs for the showcase scene
 */

using System.Collections;
using UnityEngine;

/// <summary>
///     Demonstrates neko texture swapping in a loop for the showcase scene.
/// </summary>
public class NekoDemo : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so texture cycle events can be filtered in the console.
    /// </summary>
    [Header("Neko Texture Change Demo")]
    [Space(10)]
#if UNITY_EDITOR
    [HelpBox(ErrorMessageInit, HelpBoxMessageType.Error)]
#endif

    private const string LoggingPrefix = "(Pokkat) NekoDemo:";

    /// <summary>
    ///     Error message shown when the Neko prefab cannot be located in the scene.
    /// </summary>
    private const string ErrorMessageInit = "ensure the NekoPrefab-based cat object is tagged under 'Neko'";

    /// <summary>
    ///     Error message shown when the expected <see cref="NekoManager" /> component is missing.
    /// </summary>
    private const string ErrorMessageManagerNotFound = "could not get NekoManager component";

    /// <summary>
    ///     Minimum allowable wait interval to prevent zero-delay coroutine loops.
    /// </summary>
    private const float MinimumIntervalSeconds = 0.1f;

    /// <summary>
    ///     Enables verbose logging of lifecycle and texture swaps.
    /// </summary>
    [SerializeField] private bool loggingEnabled = true;

    /// <summary>
    ///     Seconds to wait before toggling to the next expression/texture.
    /// </summary>
    [SerializeField] private float changeIntervalSeconds = 3f;

    /// <summary>
    ///     Coroutine handle used to manage the ongoing texture cycling routine.
    /// </summary>
    private Coroutine _changeRoutine;

    /// <summary>
    ///     Cached reference to the scene's <see cref="NekoManager" /> component.
    /// </summary>
    private NekoManager _nekoManager;

    /// <summary>
    ///     Loader responsible for resolving available neko texture pairs.
    /// </summary>
    private NekoTextureLoader _textureLoader;

    /// <summary>
    ///     Finds required dependencies, validates available textures, and starts cycling.
    /// </summary>
    private void OnEnable()
    {
        changeIntervalSeconds = Mathf.Max(changeIntervalSeconds, MinimumIntervalSeconds);

        var neko = GameObject.FindGameObjectWithTag("Neko");
        if (!neko)
        {
            Debug.LogError($"{LoggingPrefix} {ErrorMessageInit}");
            enabled = false;
            return;
        }

        _nekoManager = neko.GetComponentInChildren<NekoManager>();
        if (!_nekoManager)
        {
            Debug.LogError($"{LoggingPrefix} {ErrorMessageManagerNotFound}");
            enabled = false;
            return;
        }

        _textureLoader = new NekoTextureLoader { LoggingEnabled = loggingEnabled };
        _textureLoader.Prefind();

        if (_textureLoader.AvailableTextureIds.Length == 0)
        {
            Debug.LogError($"{LoggingPrefix} no textures found");
            enabled = false;
            return;
        }

        if (loggingEnabled)
            Debug.Log(
                $"{LoggingPrefix} texture cycling initialised with {_textureLoader.AvailableTextureIds.Length} entries");

        _changeRoutine = StartCoroutine(ChangeTextureRoutine());
    }

    /// <summary>
    ///     Stops the cycling coroutine and clears cached component references.
    /// </summary>
    private void OnDisable()
    {
        if (_changeRoutine != null)
        {
            StopCoroutine(_changeRoutine);
            _changeRoutine = null;
        }

        _nekoManager = null;
        _textureLoader = null;
    }

    /// <summary>
    ///     Clamps inspector-edited interval values to safe limits.
    /// </summary>
    private void OnValidate()
    {
        if (changeIntervalSeconds < MinimumIntervalSeconds) changeIntervalSeconds = MinimumIntervalSeconds;
    }

    /// <summary>
    ///     Iterates over every available texture ID and toggles between open/closed eye variants.
    /// </summary>
    private IEnumerator ChangeTextureRoutine()
    {
        var wait = new WaitForSeconds(changeIntervalSeconds);

        while (_textureLoader != null && _textureLoader.AvailableTextureIds.Length > 0)
            foreach (var textureId in _textureLoader.AvailableTextureIds)
            {
                if (!_nekoManager) yield break;

                var texture = _textureLoader.ResolveTextureOrDefault(textureId);
                if (!texture.isValid) continue;

                _nekoManager.SetAllMainTextures(texture.EyesOpen);
                if (loggingEnabled) Debug.Log($"{LoggingPrefix} switched to texture {textureId} (open)");

                yield return wait;

                _nekoManager.SetAllMainTextures(texture.EyesClosed);
                if (loggingEnabled) Debug.Log($"{LoggingPrefix} switched to texture {textureId} (closed)");

                yield return wait;
            }

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} texture cycling completed or resources became unavailable");
    }
}