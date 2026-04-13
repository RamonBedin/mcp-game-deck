#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Physics
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new PhysicMaterial asset and writes it to the specified project folder.
        /// </summary>
        /// <param name="name">Display name and file stem for the new material asset.</param>
        /// <param name="savePath">Project-relative folder path where the .asset file is saved.</param>
        /// <param name="dynamicFriction">Dynamic friction coefficient in the range [0, 1].</param>
        /// <param name="staticFriction">Static friction coefficient in the range [0, 1].</param>
        /// <param name="bounciness">Bounciness coefficient — 0 means no bounce, 1 means perfect bounce.</param>
        /// <param name="frictionCombine">Friction combine mode: Average, Minimum, Multiply, or Maximum.</param>
        /// <param name="bounceCombine">Bounce combine mode: Average, Minimum, Multiply, or Maximum.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the asset path and all configured values, or an error for invalid combine modes.</returns>
        [McpTool("physics-create-material", Title = "Physics / Create Physics Material")]
        [Description("Creates a new PhysicMaterial asset and saves it to the project. " + "Configure friction, bounciness, and combine modes.")]
        public ToolResponse CreatePhysicsMaterial(
            [Description("Name for the physics material asset.")] string name,
            [Description("Folder path where the .asset will be saved (e.g. 'Assets/Materials/').")] string savePath = "Assets/",
            [Description("Dynamic friction coefficient (0 to 1).")] float dynamicFriction = 0.6f,
            [Description("Static friction coefficient (0 to 1).")] float staticFriction = 0.6f,
            [Description("Bounciness coefficient (0 = no bounce, 1 = full bounce).")] float bounciness = 0f,
            [Description("Friction combine mode: 'Average', 'Minimum', 'Multiply', or 'Maximum'.")] string frictionCombine = "Average",
            [Description("Bounce combine mode: 'Average', 'Minimum', 'Multiply', or 'Maximum'.")] string bounceCombine = "Average"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!Enum.TryParse<PhysicsMaterialCombine>(frictionCombine, true, out var frictionMode))
                {
                    return ToolResponse.Error($"Invalid frictionCombine: '{frictionCombine}'. " + "Valid values: Average, Minimum, Multiply, Maximum.");
                }

                if (!Enum.TryParse<PhysicsMaterialCombine>(bounceCombine, true, out var bounceMode))
                {
                    return ToolResponse.Error($"Invalid bounceCombine: '{bounceCombine}'. " + "Valid values: Average, Minimum, Multiply, Maximum.");
                }

                var mat = new PhysicsMaterial(name)
                {
                    dynamicFriction = dynamicFriction,
                    staticFriction = staticFriction,
                    bounciness = bounciness,
                    frictionCombine = frictionMode,
                    bounceCombine = bounceMode
                };

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    return ToolResponse.Error("savePath must not be empty.");
                }

                if (!savePath.StartsWith("Assets/") && savePath != "Assets")
                {
                    return ToolResponse.Error("savePath must start with 'Assets/' (e.g. 'Assets/Materials/').");
                }

                if (!savePath.EndsWith("/"))
                {
                    savePath += "/";
                }

                if (!AssetDatabase.IsValidFolder(savePath.TrimEnd('/')))
                {
                    string parentPath = System.IO.Path.GetDirectoryName(savePath.TrimEnd('/'))!.Replace('\\', '/');
                    string folderName = System.IO.Path.GetFileName(savePath.TrimEnd('/'));
                    AssetDatabase.CreateFolder(parentPath, folderName);
                }

                var assetPath = $"{savePath}{name}.physicMaterial";
                AssetDatabase.CreateAsset(mat, assetPath);
                AssetDatabase.SaveAssets();

                var sb = new StringBuilder();
                sb.AppendLine($"Created PhysicMaterial '{name}':");
                sb.AppendLine($"  Path: {assetPath}");
                sb.AppendLine($"  Dynamic Friction: {dynamicFriction}");
                sb.AppendLine($"  Static Friction: {staticFriction}");
                sb.AppendLine($"  Bounciness: {bounciness}");
                sb.AppendLine($"  Friction Combine: {frictionMode}");
                sb.AppendLine($"  Bounce Combine: {bounceMode}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Assigns an existing PhysicMaterial asset to the Collider on the specified GameObject.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the target GameObject.</param>
        /// <param name="materialPath">Project-relative asset path to the PhysicMaterial (e.g. "Assets/Materials/Bouncy.asset").</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the assignment, or an error if the object, collider, or material is not found.</returns>
        [McpTool("physics-assign-material", Title = "Physics / Assign Physics Material")]
        [Description("Assigns a PhysicMaterial asset to a Collider component on a GameObject. " + "The material must already exist as an asset in the project.")]
        public ToolResponse AssignPhysicsMaterial(
            [Description("GameObject name or hierarchy path (e.g. 'Player' or 'Environment/Floor').")] string target,
            [Description("Asset path to the PhysicMaterial (e.g. 'Assets/Materials/Bouncy.asset').")] string materialPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(target);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found: '{target}'");
                }

                if (!go.TryGetComponent<Collider>(out var collider))
                {
                    return ToolResponse.Error($"No Collider component on '{target}'.");
                }

                var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(materialPath);

                if (mat == null)
                {
                    return ToolResponse.Error($"PhysicMaterial not found at: '{materialPath}'");
                }

                collider.sharedMaterial = mat;
                EditorUtility.SetDirty(collider);

                return ToolResponse.Text($"Assigned PhysicMaterial '{mat.name}' to Collider on '{target}'.");
            });
        }

        #endregion
    }
}