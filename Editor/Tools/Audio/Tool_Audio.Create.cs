#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating and configuring AudioSource components in the scene.
    /// </summary>
    [McpToolType]
    public partial class Tool_Audio
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new GameObject with an AudioSource component, optionally loading an
        /// AudioClip from the Asset Database.  Configures playOnAwake, loop, and volume.
        /// Registers the operation with Undo.
        /// </summary>
        /// <param name="name">Name for the new GameObject. Default "AudioSource".</param>
        /// <param name="clipPath">
        /// Asset-relative path to an AudioClip (e.g. "Assets/Audio/Music.mp3").
        /// Leave empty to create the AudioSource without a clip assigned.
        /// </param>
        /// <param name="posX">World-space X position. Default 0.</param>
        /// <param name="posY">World-space Y position. Default 0.</param>
        /// <param name="posZ">World-space Z position. Default 0.</param>
        /// <param name="playOnAwake">Whether the AudioSource plays automatically on scene load. Default true.</param>
        /// <param name="loop">Whether the AudioSource loops. Default false.</param>
        /// <param name="volume">Playback volume (0–1). Default 1.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> summarising the created AudioSource, including a warning
        /// when the clip path was provided but the asset could not be found.
        /// </returns>
        [McpTool("audio-create", Title = "Audio / Create")]
        [Description("Creates a new GameObject with an AudioSource component. " + "Optionally assigns an AudioClip from an asset path. " + "Configures playOnAwake, loop, and volume. The operation is registered with Undo.")]
        public ToolResponse CreateAudio(
            [Description("Name for the new GameObject. Default 'AudioSource'.")] string name = "AudioSource",
            [Description("Asset-relative path to an AudioClip (e.g. 'Assets/Audio/Music.mp3'). " + "Leave empty to skip clip assignment.")] string clipPath = "",
            [Description("World-space X position. Default 0.")] float posX = 0f,
            [Description("World-space Y position. Default 0.")] float posY = 0f,
            [Description("World-space Z position. Default 0.")] float posZ = 0f,
            [Description("Whether the AudioSource plays automatically on scene load. Default true.")] bool playOnAwake = true,
            [Description("Whether the AudioSource loops. Default false.")] bool loop = false,
            [Description("Playback volume in the range 0 to 1. Default 1.")] float volume = 1f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string goName = string.IsNullOrWhiteSpace(name) ? "AudioSource" : name;

                var go     = new GameObject(goName);
                go.transform.position = new Vector3(posX, posY, posZ);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = playOnAwake;
                source.loop = loop;
                source.volume = Mathf.Clamp01(volume);

                var sb = new StringBuilder();
                string clipWarning = string.Empty;

                if (!string.IsNullOrWhiteSpace(clipPath))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);

                    if (clip == null)
                    {
                        clipWarning = $"  Warning: AudioClip not found at '{clipPath}' — clip not assigned.";
                    }
                    else
                    {
                        source.clip = clip;
                        sb.AppendLine($"  Clip: {clipPath}");
                    }
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create AudioSource {goName}");
                Selection.activeGameObject = go;

                sb.Insert(0, $"Created AudioSource '{goName}':\n");
                sb.AppendLine($"  Position:    ({posX}, {posY}, {posZ})");
                sb.AppendLine($"  PlayOnAwake: {playOnAwake}");
                sb.AppendLine($"  Loop:        {loop}");
                sb.AppendLine($"  Volume:      {source.volume}");
                sb.AppendLine($"  InstanceId:  {go.GetInstanceID()}");

                if (!string.IsNullOrEmpty(clipWarning))
                {
                    sb.AppendLine(clipWarning);
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}