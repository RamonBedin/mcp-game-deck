#nullable enable
using System.ComponentModel;
using System.Text;
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
        /// Returns a formatted summary of all active build settings from EditorUserBuildSettings and PlayerSettings.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the current build target, product name, company name,
        /// bundle identifier, version, scripting backend, scripting defines, development build flag, and
        /// all scenes registered in Build Settings.
        /// </returns>
        [McpTool("build-get-settings", Title = "Build / Get Settings")]
        [Description("Gets current build settings including active build target, product name, company name, " + "bundle identifier, version, scripting backend, architecture, and scripting defines.")]
        public ToolResponse GetSettings()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Build Settings:");
                sb.AppendLine($"  Active Build Target: {EditorUserBuildSettings.activeBuildTarget}");
                sb.AppendLine($"  Build Target Group: {EditorUserBuildSettings.selectedBuildTargetGroup}");
                sb.AppendLine($"  Product Name: {PlayerSettings.productName}");
                sb.AppendLine($"  Company Name: {PlayerSettings.companyName}");
                sb.AppendLine($"  Version: {PlayerSettings.bundleVersion}");
#if UNITY_ANDROID
                sb.AppendLine($"  Bundle Version Code: {PlayerSettings.Android.bundleVersionCode}");
#endif
#if UNITY_IOS
                sb.AppendLine($"  Build Number: {PlayerSettings.iOS.buildNumber}");
#endif
                sb.AppendLine($"  Bundle Identifier: {PlayerSettings.applicationIdentifier}");

                var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);
                sb.AppendLine($"  Scripting Backend: {PlayerSettings.GetScriptingBackend(namedTarget)}");
                sb.AppendLine($"  Scripting Defines: {PlayerSettings.GetScriptingDefineSymbols(namedTarget)}");
                sb.AppendLine($"  Development Build: {EditorUserBuildSettings.development}");

                sb.AppendLine();
                sb.AppendLine("Build Scenes:");
                var scenes = EditorBuildSettings.scenes;

                for (int i = 0; i < scenes.Length; i++)
                {
                    var scene = scenes[i];
                    sb.AppendLine($"  [{i}] {scene.path} (enabled: {scene.enabled})");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}