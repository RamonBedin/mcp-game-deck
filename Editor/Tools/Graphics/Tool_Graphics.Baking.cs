#nullable enable
using System.ComponentModel;
using System.Text;
using System.Globalization;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for graphics settings, quality levels, lightmap baking, render statistics,
    /// post-processing volumes, and light/reflection probe management.
    /// </summary>
    [McpToolType]
    public partial class Tool_Graphics
    {
        #region TOOLS METHODS

        /// <summary>Starts lightmap baking.</summary>
        /// <param name="asyncBake">When true, baking runs asynchronously. Default true.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming whether async or blocking bake was started.</returns>
        [McpTool("graphics-bake-start", Title = "Graphics / Bake Start")]
        [Description("Starts lightmap baking. Set asyncBake=false for blocking bake.")]
        public ToolResponse BakeStart(
            [Description("Async bake. Default true.")] bool asyncBake = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (asyncBake)
                {
                    Lightmapping.BakeAsync();
                }
                else
                {
                    Lightmapping.Bake();
                }

                return ToolResponse.Text(asyncBake ? "Async bake started." : "Bake completed.");
            });
        }

        /// <summary>Cancels ongoing lightmap bake.</summary>
        /// <returns>A <see cref="ToolResponse"/> confirming the bake was cancelled.</returns>
        [McpTool("graphics-bake-cancel", Title = "Graphics / Bake Cancel")]
        [Description("Cancels any ongoing lightmap baking operation.")]
        public ToolResponse BakeCancel()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Lightmapping.Cancel();
                return ToolResponse.Text("Bake cancelled.");
            });
        }

        /// <summary>Gets current bake status.</summary>
        /// <returns>A <see cref="ToolResponse"/> indicating whether lightmap baking is currently running.</returns>
        [McpTool("graphics-bake-status", Title = "Graphics / Bake Status", ReadOnlyHint = true)]
        [Description("Returns whether lightmap baking is currently in progress.")]
        public ToolResponse BakeStatus()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                bool baking = Lightmapping.isRunning;
                return ToolResponse.Text($"Baking: {baking}");
            });
        }

        /// <summary>Clears baked lightmap data.</summary>
        /// <returns>A <see cref="ToolResponse"/> confirming all baked data was cleared.</returns>
        [McpTool("graphics-bake-clear", Title = "Graphics / Bake Clear")]
        [Description("Clears all baked lightmap data from the current scene.")]
        public ToolResponse BakeClear()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Lightmapping.Clear();
                return ToolResponse.Text("Baked data cleared.");
            });
        }

        /// <summary>Bakes a single reflection probe.</summary>
        /// <param name="instanceId">Unity instance ID of the probe's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the probe's GameObject. Used when instanceId is 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the probe was baked,
        /// or an error if the GameObject or ReflectionProbe component is not found.
        /// </returns>
        [McpTool("graphics-bake-reflection-probe", Title = "Graphics / Bake Reflection Probe")]
        [Description("Bakes a specific Reflection Probe in the scene.")]
        public ToolResponse BakeReflectionProbe(
            [Description("Instance ID of the probe GO.")] int instanceId = 0,
            [Description("Path of the probe GO.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                if (!go.TryGetComponent<ReflectionProbe>(out var probe))
                {
                    return ToolResponse.Error("No ReflectionProbe component.");
                }

                Lightmapping.BakeReflectionProbe(probe, AssetDatabase.GetAssetPath(probe.bakedTexture));
                return ToolResponse.Text($"Baked reflection probe on '{go.name}'.");
            });
        }

        /// <summary>Gets lightmap bake settings.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with a formatted summary of the current lightmapper,
        /// bounce count, resolution, and padding settings.
        /// </returns>
        [McpTool("graphics-bake-get-settings", Title = "Graphics / Bake Get Settings", ReadOnlyHint = true)]
        [Description("Returns current lightmap bake settings.")]
        public ToolResponse BakeGetSettings()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Lightmap Settings:");
                sb.AppendLine($"  Lightmapper: {(Lightmapping.lightingSettings != null ? Lightmapping.lightingSettings.lightmapper : null)}");
                sb.AppendLine($"  Bounces: {(Lightmapping.lightingSettings != null ? Lightmapping.lightingSettings.maxBounces : null)}");
                sb.AppendLine($"  Resolution: {(Lightmapping.lightingSettings != null ? Lightmapping.lightingSettings.lightmapResolution : null)}");
                sb.AppendLine($"  Padding: {(Lightmapping.lightingSettings != null ? Lightmapping.lightingSettings.lightmapPadding : null)}");
                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Creates a Reflection Probe.</summary>
        /// <param name="name">Name to assign to the new GameObject. Default "ReflectionProbe".</param>
        /// <param name="posX">World-space X position. Default 0.</param>
        /// <param name="posY">World-space Y position. Default 1.</param>
        /// <param name="posZ">World-space Z position. Default 0.</param>
        /// <param name="sizeX">Probe box size on the X axis. Default 10.</param>
        /// <param name="sizeY">Probe box size on the Y axis. Default 10.</param>
        /// <param name="sizeZ">Probe box size on the Z axis. Default 10.</param>
        /// <param name="resolution">Cubemap resolution of the probe. Default 128.</param>
        /// <param name="hdr">Whether to use HDR rendering for the probe. Default true.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the probe's name and position.</returns>
        [McpTool("graphics-bake-create-reflection-probe", Title = "Graphics / Create Reflection Probe")]
        [Description("Creates a Reflection Probe in the scene.")]
        public ToolResponse CreateReflectionProbe(
            [Description("Name.")] string name = "ReflectionProbe",
            [Description("X position.")] float posX = 0f,
            [Description("Y position.")] float posY = 1f,
            [Description("Z position.")] float posZ = 0f,
            [Description("Box size X.")] float sizeX = 10f,
            [Description("Box size Y.")] float sizeY = 10f,
            [Description("Box size Z.")] float sizeZ = 10f,
            [Description("Resolution.")] int resolution = 128,
            [Description("HDR.")] bool hdr = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = new GameObject(name);
                go.transform.position = new Vector3(posX, posY, posZ);

                var probe = go.AddComponent<ReflectionProbe>();
                probe.size = new Vector3(sizeX, sizeY, sizeZ);
                probe.resolution = resolution;
                probe.hdr = hdr;

                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                return ToolResponse.Text($"Created Reflection Probe '{name}' at ({posX},{posY},{posZ}).");
            });
        }

        /// <summary>Creates a Light Probe Group.</summary>
        /// <param name="name">Name to assign to the new GameObject. Default "LightProbeGroup".</param>
        /// <param name="posX">World-space X position of the group origin. Default 0.</param>
        /// <param name="posY">World-space Y position of the group origin. Default 0.</param>
        /// <param name="posZ">World-space Z position of the group origin. Default 0.</param>
        /// <param name="gridX">Number of probes along the X axis. Default 3.</param>
        /// <param name="gridY">Number of probes along the Y axis. Default 2.</param>
        /// <param name="gridZ">Number of probes along the Z axis. Default 3.</param>
        /// <param name="spacing">Distance between adjacent probes in Unity units. Default 2.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the group name and total probe count.</returns>
        [McpTool("graphics-bake-create-light-probes", Title = "Graphics / Create Light Probes")]
        [Description("Creates a Light Probe Group with a grid layout.")]
        public ToolResponse CreateLightProbes(
            [Description("Name.")] string name = "LightProbeGroup",
            [Description("X position.")] float posX = 0f,
            [Description("Y position.")] float posY = 0f,
            [Description("Z position.")] float posZ = 0f,
            [Description("Grid count X.")] int gridX = 3,
            [Description("Grid count Y.")] int gridY = 2,
            [Description("Grid count Z.")] int gridZ = 3,
            [Description("Spacing between probes.")] float spacing = 2f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = new GameObject(name);
                go.transform.position = new Vector3(posX, posY, posZ);

                var group = go.AddComponent<LightProbeGroup>();
                var positions = new Vector3[gridX * gridY * gridZ];
                int idx = 0;

                for (int x = 0; x < gridX; x++)
                {
                    for (int y = 0; y < gridY; y++)
                    {
                        for (int z = 0; z < gridZ; z++)
                        {
                            positions[idx++] = new Vector3(x * spacing, y * spacing, z * spacing);
                        }
                    }
                }

                group.probePositions = positions;

                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                return ToolResponse.Text($"Created Light Probe Group '{name}' with {positions.Length} probes.");
            });
        }

        /// <summary>Sets lightmap bake settings.</summary>
        /// <param name="maxBounces">Maximum indirect light bounces. Pass -1 to leave unchanged.</param>
        /// <param name="resolution">Lightmap texel resolution. Pass -1 to leave unchanged.</param>
        /// <param name="padding">Texel padding between lightmap charts. Pass -1 to leave unchanged.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the updated bounce, resolution, and padding values,
        /// or an error if no LightingSettings asset is found.
        /// </returns>
        [McpTool("graphics-bake-set-settings", Title = "Graphics / Bake Set Settings")]
        [Description("Sets lightmap bake settings: bounces, resolution, padding.")]
        public ToolResponse BakeSetSettings(
            [Description("Max bounces. -1=unchanged.")] int maxBounces = -1,
            [Description("Lightmap resolution. -1=unchanged.")] int resolution = -1,
            [Description("Lightmap padding. -1=unchanged.")] int padding = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var settings = Lightmapping.lightingSettings;

                if (settings == null)
                {
                    return ToolResponse.Error("No LightingSettings asset found.");
                }

                if (maxBounces >= 0)
                {
                    settings.maxBounces = maxBounces;
                }

                if (resolution > 0)
                {
                    settings.lightmapResolution = resolution;
                }

                if (padding >= 0)
                {
                    settings.lightmapPadding = padding;
                }

                EditorUtility.SetDirty(settings);
                return ToolResponse.Text($"Updated bake settings: bounces={settings.maxBounces}, resolution={settings.lightmapResolution}, padding={settings.lightmapPadding}.");
            });
        }

        /// <summary>Sets light probe positions manually.</summary>
        /// <param name="instanceId">Unity instance ID of the LightProbeGroup's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the LightProbeGroup's GameObject. Used when instanceId is 0.</param>
        /// <param name="positionsJson">JSON array of [x,y,z] float arrays (e.g. "[[0,0,0],[1,2,3]]").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the number of probe positions applied,
        /// or an error if the GameObject, component, or JSON is invalid.
        /// </returns>
        [McpTool("graphics-bake-set-probe-positions", Title = "Graphics / Set Probe Positions")]
        [Description("Sets custom positions for a LightProbeGroup from a JSON array of [x,y,z] arrays.")]
        public ToolResponse SetProbePositions(
            [Description("Instance ID of the LightProbeGroup GO.")] int instanceId = 0,
            [Description("Path of the LightProbeGroup GO.")] string objectPath = "",
            [Description("JSON array of positions: '[[0,0,0],[1,2,3]]'.")] string positionsJson = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);
                if (go == null) return ToolResponse.Error("GameObject not found.");

                if (!go.TryGetComponent<LightProbeGroup>(out var group))
                {
                    return ToolResponse.Error("No LightProbeGroup component.");
                }

                if (string.IsNullOrWhiteSpace(positionsJson))
                {
                    return ToolResponse.Error("positionsJson is required.");
                }

                var positions = new System.Collections.Generic.List<Vector3>();
                string stripped = positionsJson.Replace(" ", "").Replace("\n", "");
                int i = 0;

                while (i < stripped.Length)
                {
                    int start = stripped.IndexOf('[', i);

                    if (start < 0)
                    {
                        break;
                    }

                    if (start == 0 || stripped[start - 1] == '[')
                    {
                        i = start + 1;
                        continue;
                    }

                    int end = stripped.IndexOf(']', start);

                    if (end < 0)
                    {
                        break;
                    }

                    string inner = stripped.Substring(start + 1, end - start - 1);
                    string[] parts = inner.Split(',');

                    if (parts.Length >= 3)
                    {
                        if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                        {
                            positions.Add(new Vector3(x, y, z));
                        }
                    }

                    i = end + 1;
                }

                if (positions.Count == 0)
                {
                    return ToolResponse.Error("No valid positions parsed from JSON.");
                }

                Undo.RecordObject(group, "Set Probe Positions");
                group.probePositions = positions.ToArray();

                return ToolResponse.Text($"Set {positions.Count} probe positions on '{go.name}'.");
            });
        }

        #endregion
    }
}