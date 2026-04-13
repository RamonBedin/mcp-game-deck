#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.Profiling;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Profiler
    {
        #region TOOL METHODS

        /// <summary>
        /// Gets the runtime memory size of a Unity object found by name in the scene hierarchy
        /// or loaded assets, and reports native memory usage broken down by component.
        /// </summary>
        /// <param name="objectName">Name of the GameObject to measure memory for.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing native memory in KB for the GameObject,
        /// each component, mesh, material, and main texture, plus a total in KB and MB.
        /// </returns>
        [McpTool("profiler-get-object-memory", Title = "Profiler / Get Object Memory")]
        [Description("Gets the runtime memory size of a Unity object found by name in the scene hierarchy " + "or loaded assets. Reports native and managed memory usage.")]
        public ToolResponse GetObjectMemory(
            [Description("Name of the GameObject or asset to measure memory for.")] string objectName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    return ToolResponse.Error("objectName is required.");
                }

                var go = GameObject.Find(objectName);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject '{objectName}' not found in scene.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Memory for '{objectName}':");

                long totalNative = 0;
                long goSize = Profiler.GetRuntimeMemorySizeLong(go);
                sb.AppendLine($"  GameObject: {goSize / 1024f:F2} KB");
                totalNative += goSize;
                var components = go.GetComponents<UnityEngine.Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        continue;
                    }

                    long compSize = Profiler.GetRuntimeMemorySizeLong(components[i]);
                    sb.AppendLine($"  {components[i].GetType().Name}: {compSize / 1024f:F2} KB");
                    totalNative += compSize;
                }

                var meshFilter = go.GetComponent<MeshFilter>();

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    long meshSize = Profiler.GetRuntimeMemorySizeLong(meshFilter.sharedMesh);
                    sb.AppendLine($"  Mesh ({meshFilter.sharedMesh.name}): {meshSize / 1024f:F2} KB");
                    totalNative += meshSize;
                }

                var renderer = go.GetComponent<Renderer>();

                if (renderer != null && renderer.sharedMaterial != null)
                {
                    long matSize = Profiler.GetRuntimeMemorySizeLong(renderer.sharedMaterial);
                    sb.AppendLine($"  Material ({renderer.sharedMaterial.name}): {matSize / 1024f:F2} KB");
                    totalNative += matSize;

                    if (renderer.sharedMaterial.mainTexture != null)
                    {
                        long texSize = Profiler.GetRuntimeMemorySizeLong(renderer.sharedMaterial.mainTexture);
                        sb.AppendLine($"  MainTexture ({renderer.sharedMaterial.mainTexture.name}): {texSize / 1024f:F2} KB");
                        totalNative += texSize;
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"  Total Native Memory: {totalNative / 1024f:F2} KB ({totalNative / (1024f * 1024f):F2} MB)");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}