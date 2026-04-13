#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine.Profiling;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Profiler
    {
        #region TOOL METHODS

        /// <summary>
        /// Gets current profiler status and memory usage overview from the Unity Profiler API.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing profiler enabled/supported flags
        /// and reserved, allocated, mono heap, and temp allocator sizes in MB.
        /// </returns>
        [McpTool("profiler-status", Title = "Profiler / Status")]
        [Description("Gets current profiler status including whether it is enabled, " + "supported, memory usage stats (total reserved, allocated, mono heap).")]
        public ToolResponse Status()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Profiler Status:");
                sb.AppendLine($"  Enabled: {Profiler.enabled}");
                sb.AppendLine($"  Supported: {Profiler.supported}");
                sb.AppendLine();
                sb.AppendLine("Memory Overview:");
                sb.AppendLine($"  Total Reserved: {Profiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Total Allocated: {Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Total Unused Reserved: {Profiler.GetTotalUnusedReservedMemoryLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Mono Heap Size: {Profiler.GetMonoHeapSizeLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Mono Used Size: {Profiler.GetMonoUsedSizeLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Temp Allocator Size: {Profiler.GetTempAllocatorSize() / (1024.0 * 1024.0):F2} MB");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}