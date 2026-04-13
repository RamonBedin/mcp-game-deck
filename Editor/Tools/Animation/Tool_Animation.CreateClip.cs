#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Animation
    {
        #region CREATE CLIP

        /// <summary>
        /// Creates a new <see cref="AnimationClip"/> asset at the specified path.
        /// </summary>
        /// <param name="clipName">Name of the new clip (also used as the asset filename when not included in savePath).</param>
        /// <param name="savePath">Folder or full path where the clip asset will be saved.</param>
        /// <param name="duration">
        /// Duration of the clip in seconds. A loop-time curve event is set at this time so
        /// the clip length is recorded correctly in the asset.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the full asset path of the created clip,
        /// or an error message when creation fails.
        /// </returns>
        [McpTool("animation-create-clip", Title = "Animation / Create Clip")]
        [Description("Creates a new AnimationClip asset. Saves to savePath/clipName.anim (or savePath directly if it ends with .anim).")]
        public ToolResponse CreateClip(
            [Description("Name for the new AnimationClip asset (e.g. 'PlayerRun').")] string clipName,
            [Description("Destination folder or full .anim path (e.g. 'Assets/Animations'). Default 'Assets/Animations'.")] string savePath = "Assets/Animations",
            [Description("Duration of the clip in seconds. Default 1.0.")] float duration = 1.0f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(clipName))
                {
                    return ToolResponse.Error("clipName is required.");
                }

                if (duration <= 0f)
                {
                    return ToolResponse.Error($"duration must be greater than zero. Got {duration}.");
                }

                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/'.");
                }

                string assetPath;

                if (savePath.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = savePath;
                }
                else
                {
                    string safeName = string.IsNullOrWhiteSpace(clipName) ? "NewClip" : clipName;
                    assetPath = savePath.TrimEnd('/') + "/" + safeName + ".anim";
                }

                string? folder = Path.GetDirectoryName(assetPath);

                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                var clip = new AnimationClip
                {
                    name = clipName
                };

                AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
                {
                    stopTime      = duration,
                    loopTime      = false,
                    startTime     = 0f,
                    cycleOffset   = 0f,
                    keepOriginalPositionY = false,
                    keepOriginalPositionXZ = false,
                    keepOriginalOrientation = false,
                    heightFromFeet = false,
                    mirror         = false,
                    loopBlend      = false
                });

                AssetDatabase.CreateAsset(clip, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return ToolResponse.Text($"AnimationClip '{clipName}' created at '{assetPath}' (duration: {duration}s).");
            });
        }

        #endregion
    }
}