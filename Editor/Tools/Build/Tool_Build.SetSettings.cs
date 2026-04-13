#nullable enable
using System.ComponentModel;
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
        /// Sets a named build setting property to the supplied value.
        /// </summary>
        /// <param name="property">
        /// The name of the property to set. Accepted values: product_name, company_name, version,
        /// bundle_id, scripting_backend, defines, development.
        /// </param>
        /// <param name="value">The new value to assign to the property.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the change, or an error response if the
        /// property name is unrecognised or the value is invalid.
        /// </returns>
        [McpTool("build-set-settings", Title = "Build / Set Settings")]
        [Description("Sets a build setting property. Supported properties: product_name, company_name, " + "version, bundle_id, scripting_backend (mono/il2cpp), defines, development (true/false).")]
        public ToolResponse SetSettings(
            [Description("Property to set: product_name, company_name, version, bundle_id, scripting_backend, defines, development")] string property,
            [Description("Value to set for the property.")] string value
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var prop = property.ToLowerInvariant().Trim();
                var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(targetGroup);

                switch (prop)
                {
                    case "product_name":
                        PlayerSettings.productName = value;
                        return ToolResponse.Text($"Product name set to '{value}'.");

                    case "company_name":
                        PlayerSettings.companyName = value;
                        return ToolResponse.Text($"Company name set to '{value}'.");

                    case "version":
                        PlayerSettings.bundleVersion = value;
                        return ToolResponse.Text($"Version set to '{value}'.");

                    case "bundle_id":
                        PlayerSettings.applicationIdentifier = value;
                        return ToolResponse.Text($"Bundle identifier set to '{value}'.");

                    case "scripting_backend":
                        var backend = value.ToLowerInvariant() switch
                        {
                            "il2cpp" => ScriptingImplementation.IL2CPP,
                            "mono" => ScriptingImplementation.Mono2x,
                            _ => (ScriptingImplementation?)null
                        };
                        if (backend == null)
                        {
                            return ToolResponse.Error($"Unknown scripting backend '{value}'. Use 'mono' or 'il2cpp'.");
                        }
                        PlayerSettings.SetScriptingBackend(namedTarget, backend.Value);
                        return ToolResponse.Text($"Scripting backend set to '{value}'.");

                    case "defines":
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget, value);
                        return ToolResponse.Text($"Scripting defines set to '{value}'.");

                    case "development":
                        var isDev = value.ToLowerInvariant() == "true";
                        EditorUserBuildSettings.development = isDev;
                        return ToolResponse.Text($"Development build set to {isDev}.");

                    default:
                        return ToolResponse.Error($"Unknown property '{property}'. Supported: product_name, company_name, version, bundle_id, scripting_backend, defines, development.");
                }
            });
        }

        #endregion
    }
}