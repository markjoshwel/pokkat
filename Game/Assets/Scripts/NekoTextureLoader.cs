/*
 * author: mark joshwel
 * date: 16/11/2025
 * description: discovers and caches paired open and closed neko textures from resources
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Represents a pair of open/closed eye textures for the neko.
/// </summary>
public struct NekoTexture
{
    /// <summary>
    ///     Numeric identifier inferred from the Resources filename.
    /// </summary>
    public int Id;

    /// <summary>
    ///     Texture used when the neko’s eyes are open.
    /// </summary>
    public Texture EyesOpen;

    /// <summary>
    ///     Texture used when the neko’s eyes are closed.
    /// </summary>
    public Texture EyesClosed;

    /// <summary>
    ///     Indicates whether both texture references are present.
    /// </summary>
    public bool isValid => EyesOpen && EyesClosed;
}

/// <summary>
///     Discovers and caches neko textures stored under <c>Resources/NekoTextures</c>.
/// </summary>
public class NekoTextureLoader
{
    /// <summary>
    ///     Prefix used for log statements so texture loader activity can be filtered in the console.
    /// </summary>
    private const string LoggingPrefix = "(Pokkat) NekoTextureLoader:";

    /// <summary>
    ///     Resources folder path where neko textures are expected to reside.
    /// </summary>
    private const string TexturesPath = "NekoTextures";

    /// <summary>
    ///     Cache of resolved neko textures keyed by their numeric identifiers.
    /// </summary>
    private readonly Dictionary<int, NekoTexture> _nekoTextures = new();

    /// <summary>
    ///     Enables verbose logging for discovery and loading steps.
    /// </summary>
    public bool LoggingEnabled { get; set; } = true;

    /// <summary>
    ///     All IDs that yielded valid open/closed texture pairs during <see cref="Prefind" />.
    /// </summary>
    public int[] AvailableTextureIds { get; private set; } = Array.Empty<int>();

    /// <summary>
    ///     Scans for texture pairs stored under Resources/NekoTextures with a 00-99 naming convention.
    /// </summary>
    public void Prefind()
    {
        var validIds = new List<int>();

        for (var id = 0; id < 100; id++)
        {
            var idString = id.ToString("D2");
            var eyesOpenPath = $"{TexturesPath}/Tex_Neko_Body_{idString}";
            var eyesClosedPath = $"{TexturesPath}/Tex_Neko_Body_{idString}_eyeclose";

            var eyesOpenTexture = Resources.Load<Texture>(eyesOpenPath);
            var eyesClosedTexture = Resources.Load<Texture>(eyesClosedPath);

            if (eyesOpenTexture != null && eyesClosedTexture != null) validIds.Add(id);
        }

        AvailableTextureIds = validIds.ToArray();

        if (LoggingEnabled) Debug.Log($"{LoggingPrefix} Found {AvailableTextureIds.Length} valid texture pairs");
    }

    /// <summary>
    ///     Loads the requested texture pair (if needed) and returns the cached result.
    /// </summary>
    /// <param name="id">Two-digit numeric identifier (00-99).</param>
    /// <returns>Cached <see cref="NekoTexture" /> or default if missing.</returns>
    public NekoTexture ResolveTextureOrDefault(int id)
    {
        LoadTexture(id);
        return _nekoTextures.GetValueOrDefault(id);
    }

    /// <summary>
    ///     Loads and caches a texture pair if it has not been requested previously.
    /// </summary>
    /// <param name="id">Two-digit numeric identifier (00-99).</param>
    /// <returns><c>true</c> when both textures were loaded successfully.</returns>
    private bool LoadTexture(int id)
    {
        if (_nekoTextures.ContainsKey(id)) return true;

        var idString = id.ToString("D2");
        var eyesOpenPath = $"{TexturesPath}/Tex_Neko_Body_{idString}";
        var eyesClosedPath = $"{TexturesPath}/Tex_Neko_Body_{idString}_eyeclose";

        var eyesOpenTexture = Resources.Load<Texture>(eyesOpenPath);
        var eyesClosedTexture = Resources.Load<Texture>(eyesClosedPath);

        if (!eyesOpenTexture || !eyesClosedTexture)
        {
            Debug.LogError($"{LoggingPrefix} Failed to load Neko textures for id {id}. " +
                           $"EyesOpen: {(!eyesOpenTexture ? "missing" : "found")}, " +
                           $"EyesClosed: {(!eyesClosedTexture ? "missing" : "found")}");
            return false;
        }

        var nekoTexture = new NekoTexture
        {
            Id = id,
            EyesOpen = eyesOpenTexture,
            EyesClosed = eyesClosedTexture
        };

        _nekoTextures[id] = nekoTexture;
        return true;
    }
}