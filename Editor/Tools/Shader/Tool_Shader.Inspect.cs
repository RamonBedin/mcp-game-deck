#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for inspecting and listing shader assets in the Unity project.
    /// Covers detailed shader property, pass, and keyword reporting, as well as project-wide shader discovery with name filtering.
    /// </summary>
    [McpToolType]
    public partial class Tool_Shader
    {
        #region TOOL METHODS

        /// <summary>
        /// Locates a shader by name or asset path and returns a detailed report of its
        /// exposed properties, render passes, and shader keywords.
        /// </summary>
        /// <param name="shaderName">Shader name (e.g. "Universal Render Pipeline/Lit") or asset path.</param>
        /// <returns>Formatted text with the shader's render queue, pass count, properties, passes, and keywords.</returns>
        [McpTool("shader-inspect", Title = "Shader / Inspect")]
        [Description("Inspects a shader and returns its properties, passes, keywords, " + "render queue, and supported features.")]
        public ToolResponse Inspect(
            [Description("Shader name (e.g. 'Universal Render Pipeline/Lit') or asset path.")] string shaderName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(shaderName))
                {
                    return ToolResponse.Error("shaderName is required.");
                }

                var shader = Shader.Find(shaderName);

                if (shader == null)
                {
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderName);
                }

                if (shader == null)
                {
                    return ToolResponse.Error($"Shader '{shaderName}' not found.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Shader: {shader.name}");
                sb.AppendLine($"  Render Queue: {shader.renderQueue}");
                sb.AppendLine($"  Pass Count: {shader.passCount}");
                sb.AppendLine($"  Is Supported: {shader.isSupported}");

                sb.AppendLine();
                int propCount = shader.GetPropertyCount();
                sb.AppendLine($"Properties ({propCount}):");

                for (int i = 0; i < propCount; i++)
                {
                    var propName = shader.GetPropertyName(i);
                    var propType = shader.GetPropertyType(i);
                    var propDesc = shader.GetPropertyDescription(i);
                    sb.AppendLine($"  {propName} ({propType}): {propDesc}");
                }

                sb.AppendLine();
                sb.AppendLine("Passes:");

                for (int i = 0; i < shader.passCount; i++)
                {
                    var passName = shader.FindPassTagValue(i, new ShaderTagId("LightMode"));
                    sb.AppendLine($"  [{i}] {(passName.name != "" ? passName.name : "(unnamed)")}");
                }

                var keywords = shader.keywordSpace.keywords;

                if (keywords.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Keywords ({keywords.Length}):");

                    foreach (var kw in keywords)
                    {
                        sb.AppendLine($"  {kw.name} ({kw.type})");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}