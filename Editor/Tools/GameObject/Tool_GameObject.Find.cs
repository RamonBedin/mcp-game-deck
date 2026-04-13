#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Searches all GameObjects in the active scene and returns a list of matches with their
        /// names and instance IDs. Supports four search strategies: by_name, by_tag, by_layer,
        /// and by_component.
        /// </summary>
        /// <param name="searchTerm">
        /// The value to search for. Meaning depends on searchMethod:
        /// by_name → substring match (case-insensitive);
        /// by_tag → exact tag string;
        /// by_layer → layer name or layer index as string;
        /// by_component → simple or fully-qualified component type name.
        /// </param>
        /// <param name="searchMethod">
        /// Strategy to use: "by_name", "by_tag", "by_layer", or "by_component". Default "by_name".
        /// </param>
        /// <param name="includeInactive">When true, inactive GameObjects are included in the search. Default false.</param>
        /// <param name="maxResults">Maximum number of results to return. Default 50, max 500.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each match as "name (instanceId: X)",
        /// or an error when the searchMethod is invalid or the searchTerm is empty.
        /// </returns>
        [McpTool("gameobject-find", Title = "GameObject / Find", ReadOnlyHint = true)]
        [Description("Searches all GameObjects in the active scene and returns name + instance ID for each match. " + "Search methods: 'by_name' (case-insensitive substring), 'by_tag' (exact tag), " + "'by_layer' (layer name or index), 'by_component' (type name). " + "Results are capped at maxResults.")]
        public ToolResponse FindGameObjects(
            [Description("Value to search for. Meaning depends on searchMethod: " + "by_name = substring of name; by_tag = exact tag; " + "by_layer = layer name or index; by_component = component type name.")] string searchTerm,
            [Description("Search strategy: 'by_name', 'by_tag', 'by_layer', or 'by_component'. Default 'by_name'.")] string searchMethod = "by_name",
            [Description("When true, inactive GameObjects are included in results. Default false.")] bool includeInactive = false,
            [Description("Maximum number of results to return. Default 50.")] int maxResults = 50
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return ToolResponse.Error("searchTerm is required.");
                }

                if (maxResults <= 0)
                {
                    maxResults = 50;
                }

                if (maxResults > 500)
                {
                    maxResults = 500;
                }

                var method = searchMethod.Trim().ToLowerInvariant();
                var sb = new StringBuilder();
                int found = 0;

                switch (method)
                {
                    case "by_name":
                    {
                        string lower = searchTerm.ToLowerInvariant();
                        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                        found = SearchByName(rootObjects, lower, includeInactive, maxResults, sb);
                        break;
                    }

                    case "by_tag":
                    {
                        if (!includeInactive)
                        {
                            GameObject[] tagged;
                            try
                            {
                                tagged = GameObject.FindGameObjectsWithTag(searchTerm);
                            }
                            catch (UnityException ex)
                            {
                                return ToolResponse.Error($"Tag '{searchTerm}' is not defined: {ex.Message}");
                            }

                            for (int i = 0; i < tagged.Length && found < maxResults; i++)
                            {
                                AppendMatch(sb, tagged[i]);
                                found++;
                            }
                        }
                        else
                        {
                            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                            found = SearchByTag(rootObjects, searchTerm, maxResults, sb);
                        }
                        break;
                    }

                    case "by_layer":
                    {
                        int layerIndex = -1;
                        if (int.TryParse(searchTerm, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsedLayer))
                        {
                            layerIndex = parsedLayer;
                        }
                        else
                        {
                            layerIndex = LayerMask.NameToLayer(searchTerm);
                        }
                        if (layerIndex < 0)
                        {
                            return ToolResponse.Error($"Layer '{searchTerm}' not found. Provide a valid layer name or index.");
                        }
                        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                        found = SearchByLayer(rootObjects, layerIndex, includeInactive, maxResults, sb);
                        break;
                    }

                    case "by_component":
                    {
                        var resolvedType = ResolveGameObjectSearchType(searchTerm);
                        if (resolvedType == null)
                        {
                            return ToolResponse.Error($"Could not resolve component type '{searchTerm}'. " + "Ensure the type name is correct and the assembly is loaded.");
                        }
                        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                        found = SearchByComponent(rootObjects, resolvedType, includeInactive, maxResults, sb);
                        break;
                    }

                    default:
                        return ToolResponse.Error($"Unknown searchMethod '{searchMethod}'. " + "Valid values: by_name, by_tag, by_layer, by_component.");
                }

                var result = new StringBuilder();
                result.AppendLine($"Found {found} GameObject(s) (method: {method}, term: '{searchTerm}'" + (found == maxResults ? $", capped at {maxResults}" : "") + "):");
                result.Append(sb);

                return ToolResponse.Text(result.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Attempts to resolve a component type by name for use in by_component searches.
        /// Searches common Unity namespaces before performing a full assembly scan.
        /// </summary>
        /// <param name="typeName">Simple or fully-qualified component type name.</param>
        /// <returns>
        /// The matching <see cref="System.Type"/> assignable to <see cref="Component"/>,
        /// or <c>null</c> if not found.
        /// </returns>
        private static System.Type? ResolveGameObjectSearchType(string typeName)
        {
            string[] prefixes = { "UnityEngine.", "UnityEngine.UI.", "UnityEngine.Rendering.", "" };
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            for (int p = 0; p < prefixes.Length; p++)
            {
                string fullName = prefixes[p] + typeName;

                for (int a = 0; a < assemblies.Length; a++)
                {
                    var type = assemblies[a].GetType(fullName, throwOnError: false, ignoreCase: true);

                    if (type != null && typeof(UnityEngine.Component).IsAssignableFrom(type))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Appends a single match line in the format "  name (instanceId: X)" to the builder.
        /// </summary>
        /// <param name="sb">The output builder to append to.</param>
        /// <param name="go">The matching GameObject.</param>
        private static void AppendMatch(StringBuilder sb, GameObject go)
        {
            sb.AppendLine($"  {go.name} (instanceId: {go.GetInstanceID()})");
        }

        /// <summary>
        /// Recursively searches a set of root GameObjects for a case-insensitive name substring match.
        /// </summary>
        /// <param name="objects">Root GameObjects to search from.</param>
        /// <param name="lowerTerm">Lowercase search substring.</param>
        /// <param name="includeInactive">Whether to visit inactive objects.</param>
        /// <param name="maxResults">Result cap.</param>
        /// <param name="sb">Output builder.</param>
        /// <returns>Number of matches appended.</returns>
        private static int SearchByName(GameObject[] objects, string lowerTerm, bool includeInactive, int maxResults, StringBuilder sb)
        {
            int count = 0;

            for (int i = 0; i < objects.Length && count < maxResults; i++)
            {
                count += SearchByNameRecursive(objects[i], lowerTerm, includeInactive, maxResults, sb, count);
            }

            return count;
        }

        /// <summary>
        /// Recursively visits a GameObject and its children for a name substring match.
        /// </summary>
        /// <param name="go">The GameObject to inspect, including its children.</param>
        /// <param name="lowerTerm">Lowercase search substring to match against each object's name.</param>
        /// <param name="includeInactive">When false, inactive GameObjects are skipped.</param>
        /// <param name="maxResults">Maximum total number of matches allowed across the full traversal.</param>
        /// <param name="sb">Output builder to which matching entries are appended.</param>
        /// <param name="currentCount">Number of matches already recorded before this call.</param>
        /// <returns>Number of matches appended during this recursive call and its descendants.</returns>
        private static int SearchByNameRecursive(GameObject go, string lowerTerm, bool includeInactive, int maxResults, StringBuilder sb, int currentCount)
        {
            if (!includeInactive && !go.activeInHierarchy)
            {
                return 0;
            }

            int added = 0;

            if (go.name.ToLowerInvariant().Contains(lowerTerm) && currentCount + added < maxResults)
            {
                AppendMatch(sb, go);
                added++;
            }

            var t = go.transform;

            for (int i = 0; i < t.childCount && currentCount + added < maxResults; i++)
            {
                added += SearchByNameRecursive(t.GetChild(i).gameObject, lowerTerm, includeInactive, maxResults, sb, currentCount + added);
            }

            return added;
        }

        /// <summary>
        /// Recursively searches a set of root GameObjects (including inactive) by tag.
        /// </summary>
        /// <param name="objects">Root GameObjects to search from.</param>
        /// <param name="tag">Exact tag to match.</param>
        /// <param name="maxResults">Result cap.</param>
        /// <param name="sb">Output builder.</param>
        /// <returns>Number of matches appended.</returns>
        private static int SearchByTag(GameObject[] objects, string tag, int maxResults, StringBuilder sb)
        {
            int count = 0;

            for (int i = 0; i < objects.Length && count < maxResults; i++)
            {
                count += SearchByTagRecursive(objects[i], tag, maxResults, sb, count);
            }

            return count;
        }

        /// <summary>
        /// Recursively visits a GameObject and its children for an exact tag match.
        /// </summary>
        /// <param name="go">The GameObject to inspect, including its children.</param>
        /// <param name="tag">Exact tag string to match against each object.</param>
        /// <param name="maxResults">Maximum total number of matches allowed across the full traversal.</param>
        /// <param name="sb">Output builder to which matching entries are appended.</param>
        /// <param name="currentCount">Number of matches already recorded before this call.</param>
        /// <returns>Number of matches appended during this recursive call and its descendants.</returns>
        private static int SearchByTagRecursive(GameObject go, string tag, int maxResults, StringBuilder sb, int currentCount)
        {
            int added = 0;

            if (go.CompareTag(tag) && currentCount + added < maxResults)
            {
                AppendMatch(sb, go);
                added++;
            }

            var t = go.transform;

            for (int i = 0; i < t.childCount && currentCount + added < maxResults; i++)
            {
                added += SearchByTagRecursive(t.GetChild(i).gameObject, tag, maxResults, sb, currentCount + added);
            }

            return added;
        }

        /// <summary>
        /// Recursively searches a set of root GameObjects by layer index.
        /// </summary>
        /// <param name="objects">Root GameObjects to search from.</param>
        /// <param name="layerIndex">Unity layer index to match.</param>
        /// <param name="includeInactive">Whether to visit inactive objects.</param>
        /// <param name="maxResults">Result cap.</param>
        /// <param name="sb">Output builder.</param>
        /// <returns>Number of matches appended.</returns>
        private static int SearchByLayer(GameObject[] objects, int layerIndex, bool includeInactive, int maxResults, StringBuilder sb)
        {
            int count = 0;

            for (int i = 0; i < objects.Length && count < maxResults; i++)
            {
                count += SearchByLayerRecursive(objects[i], layerIndex, includeInactive, maxResults, sb, count);
            }

            return count;
        }

        /// <summary>
        /// Recursively visits a GameObject and its children for a layer index match.
        /// </summary>
        /// <param name="go">The GameObject to inspect, including its children.</param>
        /// <param name="layerIndex">Unity layer index to match against each object's layer.</param>
        /// <param name="includeInactive">When false, inactive GameObjects are skipped.</param>
        /// <param name="maxResults">Maximum total number of matches allowed across the full traversal.</param>
        /// <param name="sb">Output builder to which matching entries are appended.</param>
        /// <param name="currentCount">Number of matches already recorded before this call.</param>
        /// <returns>Number of matches appended during this recursive call and its descendants.</returns>
        private static int SearchByLayerRecursive(GameObject go, int layerIndex, bool includeInactive, int maxResults, StringBuilder sb, int currentCount)
        {
            if (!includeInactive && !go.activeInHierarchy)
            {
                return 0;
            }

            int added = 0;

            if (go.layer == layerIndex && currentCount + added < maxResults)
            {
                AppendMatch(sb, go);
                added++;
            }

            var t = go.transform;

            for (int i = 0; i < t.childCount && currentCount + added < maxResults; i++)
            {
                added += SearchByLayerRecursive(t.GetChild(i).gameObject, layerIndex, includeInactive, maxResults, sb, currentCount + added);
            }

            return added;
        }

        /// <summary>
        /// Recursively searches a set of root GameObjects for a specific component type.
        /// </summary>
        /// <param name="objects">Root GameObjects to search from.</param>
        /// <param name="type">Component type to look for.</param>
        /// <param name="includeInactive">Whether to visit inactive objects.</param>
        /// <param name="maxResults">Result cap.</param>
        /// <param name="sb">Output builder.</param>
        /// <returns>Number of matches appended.</returns>
        private static int SearchByComponent(GameObject[] objects, System.Type type, bool includeInactive, int maxResults, StringBuilder sb)
        {
            int count = 0;

            for (int i = 0; i < objects.Length && count < maxResults; i++)
            {
                count += SearchByComponentRecursive(objects[i], type, includeInactive, maxResults, sb, count);
            }

            return count;
        }

        /// <summary>
        /// Recursively visits a GameObject and its children checking for the target component type.
        /// </summary>
        /// <param name="go">The GameObject to inspect, including its children.</param>
        /// <param name="type">Component type to look for on each GameObject.</param>
        /// <param name="includeInactive">When false, inactive GameObjects are skipped.</param>
        /// <param name="maxResults">Maximum total number of matches allowed across the full traversal.</param>
        /// <param name="sb">Output builder to which matching entries are appended.</param>
        /// <param name="currentCount">Number of matches already recorded before this call.</param>
        /// <returns>Number of matches appended during this recursive call and its descendants.</returns>
        private static int SearchByComponentRecursive(GameObject go, System.Type type, bool includeInactive, int maxResults, StringBuilder sb, int currentCount)
        {
            if (!includeInactive && !go.activeInHierarchy)
            {
                return 0;
            }

            int added = 0;

            if (go.GetComponent(type) != null && currentCount + added < maxResults)
            {
                AppendMatch(sb, go);
                added++;
            }

            var t = go.transform;

            for (int i = 0; i < t.childCount && currentCount + added < maxResults; i++)
            {
                added += SearchByComponentRecursive(t.GetChild(i).gameObject, type, includeInactive, maxResults, sb, currentCount + added);
            }

            return added;
        }

        #endregion
    }
}