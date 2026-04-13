#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Build
    {
        #region TOOL METHODS

        /// <summary>
        /// Switches the active build target to the specified platform, triggering an asset reimport.
        /// </summary>
        /// <param name="target">
        /// The platform identifier string. Accepted values: windows64, osx, linux64, android, ios, webgl, uwp, tvos.
        /// </param>
        /// <param name="subtarget">
        /// Build subtarget variant. Accepted values: player, server. Defaults to player.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the switch, or an error response if the target
        /// is unrecognised or the required platform module is not installed.
        /// </returns>
        [McpTool("build-switch-platform", Title = "Build / Switch Platform")]
        [Description("Switches the active build target platform. This triggers a reimport of assets " + "for the new platform and may take some time.")]
        public ToolResponse SwitchPlatform(
            [Description("Target platform: windows64, osx, linux64, android, ios, webgl, uwp, tvos")] string target,
            [Description("Subtarget: 'player' or 'server'. Default is 'player'.")] string subtarget = "player"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var buildTarget = ParseBuildTarget(target);

                if (buildTarget == null)
                {
                    return ToolResponse.Error($"Unknown build target '{target}'. Valid: windows64, osx, linux64, android, ios, webgl, uwp, tvos.");
                }

                var targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget.Value);
                int subtargetValue = subtarget.ToLowerInvariant() switch
                {
                    "server" => (int)StandaloneBuildSubtarget.Server,
                    _ => (int)StandaloneBuildSubtarget.Player
                };

                EditorUserBuildSettings.standaloneBuildSubtarget = (StandaloneBuildSubtarget)subtargetValue;

                bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, buildTarget.Value);

                if (success)
                {
                    return ToolResponse.Text($"Switched to {buildTarget.Value} ({subtarget}). Reimport in progress.");
                }

                return ToolResponse.Error($"Failed to switch to {target}. The platform module may not be installed.");
            });
        }

        #endregion

        #region HELPER METHODS

        /// <summary>
        /// Converts a platform name string to the corresponding <see cref="BuildTarget"/> value.
        /// Accepts common aliases such as "win64", "mac", "linux", etc.
        /// </summary>
        /// <param name="target">Case-insensitive platform name or alias (e.g. "windows64", "osx", "webgl").</param>
        /// <returns>
        /// The matching <see cref="BuildTarget"/>, or <c>null</c> if the string does not match any known platform.
        /// </returns>
        private static BuildTarget? ParseBuildTarget(string target)
        {
            return target.ToLowerInvariant() switch
            {
                "windows64" or "windows" or "win64" or "win" => BuildTarget.StandaloneWindows64,
                "osx" or "macos" or "mac" => BuildTarget.StandaloneOSX,
                "linux64" or "linux" => BuildTarget.StandaloneLinux64,
                "android" => BuildTarget.Android,
                "ios" => BuildTarget.iOS,
                "webgl" => BuildTarget.WebGL,
                "uwp" => BuildTarget.WSAPlayer,
                "tvos" => BuildTarget.tvOS,
                _ => null
            };
        }

        #endregion
    }
}