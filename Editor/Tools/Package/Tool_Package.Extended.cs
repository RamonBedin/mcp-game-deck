#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.PackageManager;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Package
    {
        #region TOOL METHODS

        /// <summary>Gets detailed info about an installed package.</summary>
        /// <param name="packageId">Package identifier to look up (e.g. "com.unity.textmeshpro").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with display name, version, source, description, category,
        /// and dependency list, or an error if the package is not installed or the request fails.
        /// </returns>
        [McpTool("package-get-info", Title = "Package / Get Info", ReadOnlyHint = true)]
        [Description("Returns detailed information about an installed package.")]
        public ToolResponse GetInfo(
            [Description("Package ID (e.g. 'com.unity.textmeshpro').")] string packageId
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return ToolResponse.Error("packageId is required.");
                }

                var request = Client.List(true);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status != StatusCode.Success)
                {
                    return ToolResponse.Error($"Failed to list packages: {request.Error?.message}");
                }

                foreach (var pkg in request.Result)
                {
                    if (pkg.name == packageId)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Package: {pkg.displayName}");
                        sb.AppendLine($"  Name: {pkg.name}");
                        sb.AppendLine($"  Version: {pkg.version}");
                        sb.AppendLine($"  Source: {pkg.source}");
                        sb.AppendLine($"  Description: {pkg.description}");
                        sb.AppendLine($"  Category: {pkg.category}");

                        if (pkg.dependencies.Length > 0)
                        {
                            sb.AppendLine($"  Dependencies ({pkg.dependencies.Length}):");

                            for (int i = 0; i < pkg.dependencies.Length; i++)
                            {
                                sb.AppendLine($"    {pkg.dependencies[i].name}@{pkg.dependencies[i].version}");
                            }
                        }

                        return ToolResponse.Text(sb.ToString());
                    }
                }

                return ToolResponse.Error($"Package '{packageId}' not found.");
            });
        }

        /// <summary>Embeds a package for local editing.</summary>
        /// <param name="packageId">Package identifier to embed (e.g. "com.unity.textmeshpro").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the resolved path of the embedded package,
        /// or an error if the operation fails.
        /// </returns>
        [McpTool("package-embed", Title = "Package / Embed")]
        [Description("Embeds (copies) a registry package into the Packages folder for local editing.")]
        public ToolResponse Embed(
            [Description("Package ID to embed.")] string packageId
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return ToolResponse.Error("packageId is required.");
                }

                var request = Client.Embed(packageId);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                return request.Status == StatusCode.Success ? ToolResponse.Text($"Embedded '{packageId}' to {request.Result.resolvedPath}.") : ToolResponse.Error($"Failed to embed: {request.Error?.message}");
            });
        }

        /// <summary>Forces package resolution.</summary>
        /// <returns>A <see cref="ToolResponse"/> confirming that resolution was triggered.</returns>
        [McpTool("package-resolve", Title = "Package / Resolve")]
        [Description("Forces Unity Package Manager to resolve all packages.")]
        public ToolResponse Resolve()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Client.Resolve();
                return ToolResponse.Text("Package resolution triggered.");
            });
        }

        /// <summary>Package manager health check.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming Package Manager is reachable and reporting the count,
        /// or an error if the Package Manager request fails.
        /// </returns>
        [McpTool("package-ping", Title = "Package / Ping", ReadOnlyHint = true)]
        [Description("Checks Package Manager status and installed package count.")]
        public ToolResponse PackagePing()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var request = Client.List(true);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status != StatusCode.Success)
                {
                    return ToolResponse.Error($"Package Manager error: {request.Error?.message}");
                }

                int count = 0;

                foreach (var _ in request.Result)
                {
                    count++;
                }

                return ToolResponse.Text($"Package Manager OK. {count} packages installed.");
            });
        }

        /// <summary>Lists scoped registries from manifest.json.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the raw scopedRegistries JSON array,
        /// an informational message if none are configured, or an error if manifest.json is not found.
        /// </returns>
        [McpTool("package-list-registries", Title = "Package / List Registries", ReadOnlyHint = true)]
        [Description("Lists scoped registries configured in Packages/manifest.json.")]
        public ToolResponse ListRegistries()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string manifestPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "manifest.json");

                if (!System.IO.File.Exists(manifestPath))
                {
                    return ToolResponse.Error("manifest.json not found.");
                }

                string content = System.IO.File.ReadAllText(manifestPath);
                int idx = content.IndexOf("\"scopedRegistries\"");

                if (idx < 0)
                {
                    return ToolResponse.Text("No scoped registries configured.");
                }

                int arrStart = content.IndexOf('[', idx);

                if (arrStart < 0)
                {
                    return ToolResponse.Text("No scoped registries configured.");
                }

                int depth = 0;
                int arrEnd = arrStart;

                for (int i = arrStart; i < content.Length; i++)
                {
                    if (content[i] == '[')
                    {
                        depth++;
                    }
                    else if (content[i] == ']')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            arrEnd = i;
                            break;
                        }
                    }
                }

                string registries = content.Substring(arrStart, arrEnd - arrStart + 1);
                return ToolResponse.Text($"Scoped Registries:\n{registries}");
            });
        }

        /// <summary>Adds a scoped registry to manifest.json.</summary>
        /// <param name="name">Display name for the registry.</param>
        /// <param name="url">URL of the scoped registry server.</param>
        /// <param name="scopes">Comma-separated list of package scopes served by this registry (e.g. "com.company,com.other").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the registry was added,
        /// or an error if name/url are missing or manifest.json is not found.
        /// </returns>
        [McpTool("package-add-registry", Title = "Package / Add Registry")]
        [Description("Adds a scoped registry to Packages/manifest.json.")]
        public ToolResponse AddRegistry(
            [Description("Registry name.")] string name,
            [Description("Registry URL.")] string url,
            [Description("Scopes as comma-separated (e.g. 'com.company,com.other').")] string scopes = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                {
                    return ToolResponse.Error("name and url are required.");
                }

                string manifestPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "manifest.json");

                if (!System.IO.File.Exists(manifestPath))
                {
                    return ToolResponse.Error("manifest.json not found.");
                }

                string content = System.IO.File.ReadAllText(manifestPath);
                string[] scopeArr = string.IsNullOrWhiteSpace(scopes) ? new[] { "" } : scopes.Split(',');
                var scopeJson = new StringBuilder("[");

                for (int i = 0; i < scopeArr.Length; i++)
                {
                    if (i > 0) scopeJson.Append(",");
                    scopeJson.Append($"\"{scopeArr[i].Trim()}\"");
                }

                scopeJson.Append("]");
                string entry = $"{{\"name\":\"{name}\",\"url\":\"{url}\",\"scopes\":{scopeJson}}}";
                int idx = content.IndexOf("\"scopedRegistries\"");

                if (idx < 0)
                {
                    int depIdx = content.IndexOf("\"dependencies\"");

                    if (depIdx < 0)
                    {
                        return ToolResponse.Error("Cannot find dependencies in manifest.json.");
                    }

                    content = content.Insert(depIdx, $"\"scopedRegistries\":[{entry}],\n  ");
                }
                else
                {
                    int arrStart = content.IndexOf('[', idx);
                    content = content.Insert(arrStart + 1, entry + ",");
                }

                System.IO.File.WriteAllText(manifestPath, content);
                Client.Resolve();
                return ToolResponse.Text($"Added scoped registry '{name}' ({url}).");
            });
        }

        /// <summary>Removes a scoped registry from manifest.json.</summary>
        /// <param name="nameOrUrl">Name or URL of the registry to remove.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with instructions to manually remove the entry,
        /// or an error if the registry identifier is not found in the manifest.
        /// </returns>
        [McpTool("package-remove-registry", Title = "Package / Remove Registry")]
        [Description("Removes a scoped registry from Packages/manifest.json by name or URL.")]
        public ToolResponse RemoveRegistry(
            [Description("Registry name or URL to remove.")] string nameOrUrl
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(nameOrUrl))
                {
                    return ToolResponse.Error("nameOrUrl is required.");
                }

                string manifestPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "manifest.json");

                if (!System.IO.File.Exists(manifestPath))
                {
                    return ToolResponse.Error("manifest.json not found.");
                }

                string content = System.IO.File.ReadAllText(manifestPath);

                if (content.IndexOf(nameOrUrl) < 0)
                {
                    return ToolResponse.Error($"Registry '{nameOrUrl}' not found in manifest.");
                }

                return ToolResponse.Text($"To remove registry '{nameOrUrl}', manually edit Packages/manifest.json and remove the entry from scopedRegistries array, then resolve packages.");
            });
        }

        /// <summary>Gets status of package operations.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the installed package count and an OK status,
        /// or an error message if the Package Manager request fails.
        /// </returns>
        [McpTool("package-status", Title = "Package / Status", ReadOnlyHint = true)]
        [Description("Returns current Package Manager status and any pending operations.")]
        public ToolResponse Status()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Package Manager Status:");
                sb.AppendLine("  Status: Checking...");
                var request = Client.List(true);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status == StatusCode.Success)
                {
                    int installed = 0;

                    foreach (var _ in request.Result)
                    {
                        installed++;
                    }

                    sb.AppendLine($"  Installed Packages: {installed}");
                    sb.AppendLine($"  Status: OK");
                }
                else
                {
                    sb.AppendLine($"  Error: {request.Error?.message}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}