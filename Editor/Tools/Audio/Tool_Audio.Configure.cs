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
    public partial class Tool_Audio
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds an existing AudioSource component by instance ID or hierarchy path and applies
        /// the supplied property overrides. Only parameters with non-sentinel values are written;
        /// all others are left unchanged. Registers the change with Undo.
        /// </summary>
        /// <param name="instanceId">Instance ID of the target GameObject. 0 to locate by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="volume">New volume (0–1). -1 to leave unchanged.</param>
        /// <param name="pitch">New pitch. -1 to leave unchanged.</param>
        /// <param name="spatialBlend">3D blend (0 = fully 2D, 1 = fully 3D). -1 to leave unchanged.</param>
        /// <param name="minDistance">Minimum distance for 3D attenuation. -1 to leave unchanged.</param>
        /// <param name="maxDistance">Maximum distance for 3D attenuation. -1 to leave unchanged.</param>
        /// <param name="playOnAwake">1 = true, 0 = false, -1 = leave unchanged.</param>
        /// <param name="loop">1 = true, 0 = false, -1 = leave unchanged.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each property that was changed, or an error when the
        /// target cannot be found or has no AudioSource component.
        /// </returns>
        [McpTool("audio-configure", Title = "Audio / Configure")]
        [Description("Configures an existing AudioSource component identified by instanceId or objectPath. " + "Only supplied non-sentinel values are applied. " + "volume/pitch/spatialBlend/minDistance/maxDistance: -1 = skip. " + "playOnAwake/loop: -1 = skip, 0 = false, 1 = true.")]
        public ToolResponse ConfigureAudio(
            [Description("Instance ID of the target GameObject. Use 0 to locate by objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Scene/Audio/Ambience'). " + "Used when instanceId is 0.")] string objectPath = "",
            [Description("Volume (0–1). -1 to leave unchanged.")] float volume = -1f,
            [Description("Pitch. -1 to leave unchanged.")] float pitch = -1f,
            [Description("Spatial blend (0 = 2D, 1 = 3D). -1 to leave unchanged.")] float spatialBlend = -1f,
            [Description("Minimum distance for 3D attenuation. -1 to leave unchanged.")] float minDistance = -1f,
            [Description("Maximum distance for 3D attenuation. -1 to leave unchanged.")] float maxDistance = -1f,
            [Description("PlayOnAwake: 1 = true, 0 = false, -1 = unchanged.")] int playOnAwake = -1,
            [Description("Loop: 1 = true, 0 = false, -1 = unchanged.")] int loop = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = ResolveAudioGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error(BuildAudioNotFoundError(instanceId, objectPath));
                }

                if (!go.TryGetComponent<AudioSource>(out var source))
                {
                    return ToolResponse.Error($"GameObject '{go.name}' has no AudioSource component.");
                }

                Undo.RecordObject(source, $"Configure AudioSource {go.name}");

                var sb = new StringBuilder();
                sb.AppendLine($"Configured AudioSource '{go.name}':");

                if (volume >= 0f)
                {
                    source.volume = Mathf.Clamp01(volume);
                    sb.AppendLine($"  Volume:       {source.volume}");
                }

                if (pitch >= 0f)
                {
                    source.pitch = pitch;
                    sb.AppendLine($"  Pitch:        {pitch}");
                }

                if (spatialBlend >= 0f)
                {
                    source.spatialBlend = Mathf.Clamp01(spatialBlend);
                    sb.AppendLine($"  SpatialBlend: {source.spatialBlend}");
                }

                if (minDistance >= 0f)
                {
                    source.minDistance = minDistance;
                    sb.AppendLine($"  MinDistance:  {minDistance}");
                }

                if (maxDistance >= 0f)
                {
                    source.maxDistance = maxDistance;
                    sb.AppendLine($"  MaxDistance:  {maxDistance}");
                }

                if (playOnAwake == 0 || playOnAwake == 1)
                {
                    source.playOnAwake = playOnAwake == 1;
                    sb.AppendLine($"  PlayOnAwake:  {source.playOnAwake}");
                }

                if (loop == 0 || loop == 1)
                {
                    source.loop = loop == 1;
                    sb.AppendLine($"  Loop:         {source.loop}");
                }

                EditorUtility.SetDirty(source);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Resolves a <see cref="GameObject"/> from an instance ID or a hierarchy path.
        /// Returns <c>null</c> when no match is found.
        /// </summary>
        /// <param name="instanceId">Instance ID to look up. Use 0 to skip.</param>
        /// <param name="objectPath">Hierarchy path passed to <c>GameObject.Find</c>. Used when instanceId is 0.</param>
        /// <returns>The matching <see cref="GameObject"/>, or <c>null</c>.</returns>
        private static GameObject? ResolveAudioGameObject(int instanceId, string objectPath)
        {
            if (instanceId != 0)
            {
                var obj = EditorUtility.EntityIdToObject(instanceId);

                if (obj is GameObject go)
                {
                    return go;
                }

                if (obj is UnityEngine.Component comp)
                {
                    return comp.gameObject;
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(objectPath))
            {
                return GameObject.Find(objectPath);
            }

            return null;
        }

        /// <summary>
        /// Builds a human-readable "not found" error message for the given lookup inputs.
        /// </summary>
        /// <param name="instanceId">Instance ID that was used. 0 means it was not used.</param>
        /// <param name="objectPath">Path that was used. Empty means it was not used.</param>
        /// <returns>A descriptive error string.</returns>
        private static string BuildAudioNotFoundError(int instanceId, string objectPath)
        {
            if (instanceId != 0)
            {
                return $"No GameObject found for instanceId {instanceId}.";
            }

            if (!string.IsNullOrWhiteSpace(objectPath))
            {
                return $"No GameObject found at path '{objectPath}'.";
            }

            return "Provide instanceId or objectPath to identify the target GameObject.";
        }

        #endregion
    }
}