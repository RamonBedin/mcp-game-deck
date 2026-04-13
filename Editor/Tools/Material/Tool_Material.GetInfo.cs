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
    public partial class Tool_Material
    {
        #region TOOL METHODS

        /// <summary>
        /// Loads a Material by asset path or name and returns its shader name, render queue,
        /// and a full listing of all shader properties with their names and types.
        /// </summary>
        /// <param name="assetPath">Direct asset path to the material file (e.g. 'Assets/Materials/Red.mat').</param>
        /// <param name="materialName">Name of the material to search for when assetPath is empty.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the shader name, render queue, property count,
        /// and a list of each property's name and type, or an error when the material is not found.
        /// </returns>
        [McpTool("material-get-info", Title = "Material / Get Info", ReadOnlyHint = true)]
        [Description("Returns the shader name, render queue, and full property list of a Material asset. " + "Properties include their shader name and type (Float, Color, Texture, Vector, Int).")]
        public ToolResponse GetInfo(
            [Description("Asset path of the material (e.g. 'Assets/Materials/Red.mat'). Leave empty to search by name.")] string assetPath = "",
            [Description("Name of the material to search for when assetPath is not provided.")] string materialName = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Material? material = null;

                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    if (!assetPath.StartsWith("Assets/"))
                    {
                        return ToolResponse.Error("assetPath must start with 'Assets/'.");
                    }

                    material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                    if (material == null)
                    {
                        return ToolResponse.Error($"Material not found at '{assetPath}'.");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(materialName))
                {
                    string[] guids = AssetDatabase.FindAssets($"t:Material {materialName}");

                    for (int i = 0; i < guids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        var candidate = AssetDatabase.LoadAssetAtPath<Material>(path);

                        if (candidate != null && candidate.name == materialName)
                        {
                            material = candidate;
                            assetPath = path;
                            break;
                        }
                    }

                    if (material == null)
                    {
                        return ToolResponse.Error($"Material named '{materialName}' not found in the project.");
                    }
                }
                else
                {
                    return ToolResponse.Error("Either assetPath or materialName is required.");
                }

                var shader = material.shader;
                var sb = new StringBuilder();
                sb.AppendLine($"Material: {material.name}");
                sb.AppendLine($"  Asset Path: {assetPath}");
                sb.AppendLine($"  Shader: {shader.name}");
                sb.AppendLine($"  Render Queue: {material.renderQueue}");
                sb.AppendLine($"  Enable Instancing: {material.enableInstancing}");
                sb.AppendLine($"  Double Sided GI: {material.doubleSidedGI}");

                int propCount = shader.GetPropertyCount();
                sb.AppendLine($"  Property Count: {propCount}");

                if (propCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Shader Properties:");

                    for (int i = 0; i < propCount; i++)
                    {
                        string propName = shader.GetPropertyName(i);
                        var propType = shader.GetPropertyType(i);
                        string propDesc = shader.GetPropertyDescription(i);
                        var flags = shader.GetPropertyFlags(i);
                        bool isHidden = (flags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0;

                        sb.Append($"  [{i}] {propName} ({propType})");

                        if (!string.IsNullOrWhiteSpace(propDesc))
                        {
                            sb.Append($" - {propDesc}");
                        }

                        if (isHidden)
                        {
                            sb.Append(" [hidden]");
                        }
                        sb.AppendLine();

                        switch (propType)
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                sb.AppendLine($"       Value: {material.GetFloat(propName):F4}");
                                break;

                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                Color c = material.GetColor(propName);
                                sb.AppendLine($"       Value: ({c.r:F3}, {c.g:F3}, {c.b:F3}, {c.a:F3})");
                                break;

                            case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                Vector4 v = material.GetVector(propName);
                                sb.AppendLine($"       Value: ({v.x:F3}, {v.y:F3}, {v.z:F3}, {v.w:F3})");
                                break;

                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                var tex = material.GetTexture(propName);
                                sb.AppendLine($"       Value: {(tex != null ? tex.name : "(none)")}");
                                break;

                            case UnityEngine.Rendering.ShaderPropertyType.Int:
                                sb.AppendLine($"       Value: {material.GetInt(propName)}");
                                break;
                        }
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}