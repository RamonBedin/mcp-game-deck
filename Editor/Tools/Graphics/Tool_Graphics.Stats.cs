#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using Unity.Profiling;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Graphics
    {
        #region TOOL METHODS

        /// <summary>Gets rendering statistics.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with batch count, SetPass calls, triangle count, vertex count,
        /// screen resolution, and target FPS. Note: values require Play mode and at least one rendered frame.
        /// </returns>
        [McpTool("graphics-stats-get", Title = "Graphics / Stats Get", ReadOnlyHint = true)]
        [Description("Returns rendering stats: batches, draw calls, triangles, vertices from the last frame.")]
        public ToolResponse StatsGet()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Rendering Stats (last frame):");

                using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
                using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
                using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
                using (var verts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count"))
                {
                    sb.AppendLine($"  Batches: {batches.LastValue}");
                    sb.AppendLine($"  SetPass Calls: {setPass.LastValue}");
                    sb.AppendLine($"  Triangles: {tris.LastValue}");
                    sb.AppendLine($"  Vertices: {verts.LastValue}");
                    sb.AppendLine("  Note: Values require Play mode and at least one rendered frame to be accurate.");
                }

                sb.AppendLine($"  Screen: {Screen.width}x{Screen.height}");
                sb.AppendLine($"  Target FPS: {Application.targetFrameRate}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Gets GPU memory usage info.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with system memory, graphics memory, GPU name and type,
        /// max texture size, NPOT support, and active RenderTexture.
        /// </returns>
        [McpTool("graphics-stats-get-memory", Title = "Graphics / Stats Memory", ReadOnlyHint = true)]
        [Description("Returns GPU/graphics memory information.")]
        public ToolResponse StatsGetMemory()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Graphics Memory:");
                sb.AppendLine($"  System Memory: {SystemInfo.systemMemorySize} MB");
                sb.AppendLine($"  Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
                sb.AppendLine($"  GPU: {SystemInfo.graphicsDeviceName}");
                sb.AppendLine($"  GPU Type: {SystemInfo.graphicsDeviceType}");
                sb.AppendLine($"  Max Texture Size: {SystemInfo.maxTextureSize}");
                sb.AppendLine($"  NPOT Support: {SystemInfo.npotSupport}");
                sb.AppendLine($"  Render Texture Count: {(RenderTexture.active != null ? RenderTexture.active.name : null ?? "none")}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Gets pipeline info.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with pipeline type, active quality level, all available quality levels,
        /// color space, shadow resolution/distance, and anti-aliasing setting.
        /// </returns>
        [McpTool("graphics-pipeline-get-info", Title = "Graphics / Pipeline Info", ReadOnlyHint = true)]
        [Description("Returns current render pipeline info: type, quality level, shader tier.")]
        public ToolResponse PipelineGetInfo()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var sb = new StringBuilder();
                sb.AppendLine("Render Pipeline:");
                sb.AppendLine($"  Type: {(rp != null ? rp.GetType().Name : "Built-in")}");
                sb.AppendLine($"  Quality Level: {QualitySettings.GetQualityLevel()} ({QualitySettings.names[QualitySettings.GetQualityLevel()]})");
                var names = QualitySettings.names;
                var sb2 = new StringBuilder();

                for (int j = 0; j < names.Length; j++)
                {
                    if (j > 0)
                    {
                        sb2.Append(", ");
                    }

                    sb2.Append(names[j]);
                }

                string levelList = sb2.ToString();
                sb.AppendLine($"  Quality Levels: {levelList}");
                sb.AppendLine($"  Color Space: {QualitySettings.activeColorSpace}");
                sb.AppendLine($"  Shadow Resolution: {QualitySettings.shadowResolution}");
                sb.AppendLine($"  Shadow Distance: {QualitySettings.shadowDistance}");
                sb.AppendLine($"  Anti Aliasing: {QualitySettings.antiAliasing}x");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Lists available ProfilerRecorder counter names.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with two sections: known Render counters and known Memory counters
        /// available for use with <see cref="Unity.Profiling.ProfilerRecorder"/>.
        /// </returns>
        [McpTool("graphics-stats-list-counters", Title = "Graphics / List Counters", ReadOnlyHint = true)]
        [Description("Lists available ProfilerRecorder counter names for the Render category.")]
        public ToolResponse ListCounters()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Known Render Counters:");
                string[] counters = {"Batches Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count", "Visible Skinned Meshes Count", "Render Textures Count", "Render Textures Bytes", "Used Buffers Count", "Used Buffers Bytes"};

                for (int i = 0; i < counters.Length; i++)
                {
                    sb.AppendLine($"  {counters[i]}");
                }

                sb.AppendLine();
                sb.AppendLine("Known Memory Counters:");
                string[] memCounters = {"Total Used Memory", "Total Reserved Memory", "GC Used Memory", "GC Reserved Memory", "Gfx Used Memory", "Gfx Reserved Memory"};

                for (int i = 0; i < memCounters.Length; i++)
                {
                    sb.AppendLine($"  {memCounters[i]}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Sets Scene View debug draw mode.</summary>
        /// <param name="mode">Draw mode to apply: Textured, Wireframe, or TexturedWire. Default "Textured".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the new mode,
        /// or an error if no Scene View is currently active.
        /// </returns>
        [McpTool("graphics-stats-set-debug", Title = "Graphics / Set Debug Mode")]
        [Description("Sets the Scene View debug visualization mode (e.g. wireframe, overdraw).")]
        public ToolResponse SetDebugMode(
            [Description("Mode: 'Textured', 'Wireframe', 'TexturedWire'. Default 'Textured'.")] string mode = "Textured"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sv = UnityEditor.SceneView.lastActiveSceneView;

                if (sv == null)
                {
                    return ToolResponse.Error("No active SceneView.");
                }

                sv.cameraMode = mode.ToLowerInvariant() switch
                {
                    "wireframe" => UnityEditor.SceneView.GetBuiltinCameraMode(UnityEditor.DrawCameraMode.Wireframe),
                    "texturedwire" => UnityEditor.SceneView.GetBuiltinCameraMode(UnityEditor.DrawCameraMode.TexturedWire),
                    _ => UnityEditor.SceneView.GetBuiltinCameraMode(UnityEditor.DrawCameraMode.Textured),
                };

                sv.Repaint();
                return ToolResponse.Text($"Scene View mode set to '{mode}'.");
            });
        }

        #endregion
    }
}