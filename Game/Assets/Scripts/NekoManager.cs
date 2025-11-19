/*
 * author: mark joshwel
 * date: 16/11/2025
 * description: applies resolved neko textures to every renderer in the character hierarchy
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Applies textures to every renderer that forms the neko character.
/// </summary>
public class NekoManager : MonoBehaviour
{
    /// <summary>
    ///     Prefix used for log statements so neko management activity can be filtered in the console.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) NekoManager:";

    /// <summary>
    ///     Texture ID loaded on Start.
    /// </summary>
    [SerializeField] private int initialTextureId;

    /// <summary>
    ///     Enables verbose logging regarding renderer discovery and texture application.
    /// </summary>
    [SerializeField] private bool loggingEnabled = true;

    /// <summary>
    ///     Cached renderer collection used when applying resolved neko textures.
    /// </summary>
    private readonly List<Renderer> _renderers = new();

    /// <summary>
    ///     Loads the configured texture ID and applies it to every renderer.
    /// </summary>
    private void Start()
    {
        var textureLoader = new NekoTextureLoader { LoggingEnabled = loggingEnabled };
        var texture = textureLoader.ResolveTextureOrDefault(initialTextureId);

        if (texture.isValid)
        {
            SetAllMainTextures(texture.EyesOpen);

            if (loggingEnabled) Debug.Log($"{LoggingPrefix} Applied initial texture ID {initialTextureId}");

            return;
        }

        if (loggingEnabled) Debug.LogWarning($"{LoggingPrefix} Initial texture ID {initialTextureId} is not valid");
    }

    /// <summary>
    ///     Caches all renderer components that belong to the neko instance.
    /// </summary>
    private void OnEnable()
    {
        _renderers.Clear();
        _renderers.AddRange(GetComponentsInChildren<Renderer>(true));

        if (loggingEnabled) Debug.Log($"{LoggingPrefix} Cached {_renderers.Count} renderers");
    }

    /// <summary>
    ///     Clears cached renderer references when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        _renderers.Clear();
    }

    /// <summary>
    ///     Assigns the supplied texture to every cached renderer.
    /// </summary>
    /// <param name="texture">Texture to use for the neko materials.</param>
    public void SetAllMainTextures(Texture texture)
    {
        if (!texture)
        {
            Debug.LogWarning($"{LoggingPrefix} Attempted to set a null texture");
            return;
        }

        if (_renderers.Count == 0)
        {
            if (loggingEnabled) Debug.LogWarning($"{LoggingPrefix} No cached renderers to apply textures to");

            return;
        }

        foreach (var rdr in _renderers) rdr.material.mainTexture = texture;
    }
}