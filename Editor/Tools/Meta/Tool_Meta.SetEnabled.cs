#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Meta
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds the tool method whose <see cref="McpToolAttribute"/> has the specified
        /// <paramref name="toolId"/> and sets its <c>Enabled</c> property to
        /// <paramref name="enabled"/>. Because attribute instances are shared objects in the
        /// .NET reflection model (cached per method), this change persists for the lifetime of
        /// the AppDomain session but is not serialized to disk.
        /// </summary>
        /// <param name="toolId">The MCP tool ID to enable or disable (e.g. 'gameobject-create').</param>
        /// <param name="enabled">True to enable the tool; false to disable it. Default true.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the state change,
        /// a notice when the attribute does not expose a writable <c>Enabled</c> property,
        /// or an error when the tool ID is not found.
        /// </returns>
        [McpTool("tool-set-enabled", Title = "Meta / Set Tool Enabled")]
        [Description("Enables or disables a registered MCP tool by its ID for the current session. " + "The change persists in memory only and is not written to disk. " + "Use tool-list-all to discover tool IDs.")]
        public ToolResponse SetEnabled(
            [Description("The MCP tool ID to enable or disable (e.g. 'gameobject-create').")] string toolId,
            [Description("True to enable the tool, false to disable it. Default true.")] bool enabled = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(toolId))
                {
                    return ToolResponse.Error("toolId is required.");
                }

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type[] types;
                    try
                    {
                        types = assemblies[a].GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types ?? Array.Empty<Type>();
                    }

                    for (int t = 0; t < types.Length; t++)
                    {
                        Type type = types[t];

                        if (type == null)
                        {
                            continue;
                        }

                        object[] typeAttrs = type.GetCustomAttributes(typeof(McpToolTypeAttribute), inherit: false);

                        if (typeAttrs.Length == 0)
                        {
                            continue;
                        }

                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                        for (int m = 0; m < methods.Length; m++)
                        {
                            object[] methodAttrs = methods[m].GetCustomAttributes(typeof(McpToolAttribute), inherit: false);

                            if (methodAttrs.Length == 0)
                            {
                                continue;
                            }

                            McpToolAttribute attr = (McpToolAttribute)methodAttrs[0];

                            if (!string.Equals(attr.Id, toolId, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            PropertyInfo? enabledProp = typeof(McpToolAttribute).GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);

                            if (enabledProp == null || !enabledProp.CanWrite)
                            {
                                return ToolResponse.Text("Tool enable/disable not supported at runtime. " + "The McpToolAttribute.Enabled property is read-only at this scope.");
                            }

                            bool previous = attr.Enabled;
                            enabledProp.SetValue(attr, enabled);

                            return ToolResponse.Text( $"Tool '{toolId}': Enabled changed from {previous} to {attr.Enabled}.");
                        }
                    }
                }

                return ToolResponse.Error($"Tool '{toolId}' not found. Use tool-list-all to see registered tool IDs.");
            });
        }

        #endregion
    }
}