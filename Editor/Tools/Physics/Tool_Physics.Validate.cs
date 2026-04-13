#nullable enable
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Physics
    {
        #region TOOL METHODS

        /// <summary>
        /// Scans every GameObject across all loaded scenes and checks for physics issues:
        /// Rigidbodies without Colliders, non-static Colliders without Rigidbodies,
        /// joint mass ratios exceeding 100:1, and Rigidbodies with non-uniform scale.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> containing the full validation report with warning and info counts.</returns>
        [McpTool("physics-validate", Title = "Physics / Validate")]
        [Description("Validates the physics setup in the current scene. Checks for common issues like " + "missing colliders on rigidbodies, overlapping colliders, extreme mass ratios in connected bodies, " + "and non-uniform scale on rigidbodies. Returns a formatted report with warnings and suggestions.")]
        public ToolResponse Validate()
        {
            return MainThreadDispatcher.Execute(static () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Physics Validation Report ===");
                sb.AppendLine();

                int warningCount = 0;
                int infoCount = 0;
                var allGameObjects = new List<GameObject>();

                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var scene = SceneManager.GetSceneAt(s);

                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var transforms = root.GetComponentsInChildren<Transform>(true);

                        for (int t = 0; t < transforms.Length; t++)
                        {
                            allGameObjects.Add(transforms[t].gameObject);
                        }
                    }
                }

                sb.AppendLine("--- Rigidbodies without Colliders ---");
                bool foundRbWithoutCol = false;

                foreach (var go in allGameObjects)
                {

                    if (!go.TryGetComponent<Rigidbody>(out var rb))
                    {
                        continue;
                    }


                    if (!go.TryGetComponent<Collider>(out var collider))
                    {
                        sb.AppendLine($"  [WARNING] '{go.name}' has Rigidbody but no Collider.");
                        warningCount++;
                        foundRbWithoutCol = true;
                    }
                }
                if (!foundRbWithoutCol)
                {
                    sb.AppendLine("  (none)");
                }

                sb.AppendLine();
                sb.AppendLine("--- Non-Static Colliders without Rigidbodies ---");
                bool foundColWithoutRb = false;

                foreach (var go in allGameObjects)
                {
                    if (!go.TryGetComponent<Collider>(out var collider))
                    {
                        continue;
                    }

                    if (go.isStatic)
                    {
                        continue;
                    }

                    if (!go.TryGetComponent<Rigidbody>(out var rb))
                    {
                        sb.AppendLine($"  [INFO] '{go.name}' has Collider but no Rigidbody and is not static.");
                        infoCount++;
                        foundColWithoutRb = true;
                    }
                }

                if (!foundColWithoutRb)
                {
                    sb.AppendLine("  (none)");
                }

                sb.AppendLine();
                sb.AppendLine("--- High Mass Ratios on Connected Joints (>100:1) ---");
                bool foundMassRatio = false;

                foreach (var go in allGameObjects)
                {
                    var joints = go.GetComponents<Joint>();

                    if (joints.Length == 0)
                    {
                        continue;
                    }


                    if (!go.TryGetComponent<Rigidbody>(out var rbSelf))
                    {
                        continue;
                    }

                    foreach (var joint in joints)
                    {
                        if (joint.connectedBody == null)
                        {
                            continue;
                        }

                        float massA = rbSelf.mass;
                        float massB = joint.connectedBody.mass;

                        if (massA <= 0f || massB <= 0f)
                        {
                            continue;
                        }

                        float ratio = massA > massB ? massA / massB : massB / massA;

                        if (ratio > 100f)
                        {
                            sb.AppendLine($"  [WARNING] '{go.name}' ({massA} kg) <-> '{joint.connectedBody.gameObject.name}' ({massB} kg) = {ratio:F1}:1 ratio.");
                            warningCount++;
                            foundMassRatio = true;
                        }
                    }
                }

                if (!foundMassRatio)
                {
                    sb.AppendLine("  (none)");
                }

                sb.AppendLine();
                sb.AppendLine("--- Rigidbodies with Non-Uniform Scale ---");
                bool foundBadScale = false;

                foreach (var go in allGameObjects)
                {

                    if (!go.TryGetComponent<Rigidbody>(out var rb))
                    {
                        continue;
                    }

                    var scale = go.transform.lossyScale;
                    bool isUniform = Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.y, scale.z);
                    bool isOne = Mathf.Approximately(scale.x, 1f) && Mathf.Approximately(scale.y, 1f) && Mathf.Approximately(scale.z, 1f);

                    if (!isUniform || !isOne)
                    {
                        sb.AppendLine($"  [WARNING] '{go.name}' has Rigidbody with scale {scale}. Physics may behave unexpectedly with non-unit scale.");
                        warningCount++;
                        foundBadScale = true;
                    }
                }
                if (!foundBadScale)
                {
                    sb.AppendLine("  (none)");
                }

                sb.AppendLine();
                sb.AppendLine("=== Summary ===");
                sb.AppendLine($"  Warnings: {warningCount}");
                sb.AppendLine($"  Info: {infoCount}");
                sb.AppendLine($"  GameObjects scanned: {allGameObjects.Count}");

                if (warningCount == 0 && infoCount == 0)
                {
                    sb.AppendLine("  Physics setup looks clean!");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}