#nullable enable

using System;
using System.Collections.Generic;
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.MCP.Registry
{
    /// <summary>
    /// In-memory registry that stores all discovered <see cref="McpResourceInfo"/> instances
    /// and provides lookup by route template and by concrete URI.
    /// </summary>
    /// <remarks>
    /// Resources are keyed by their <see cref="McpResourceInfo.Route"/> template string (e.g.
    /// <c>"mcp-game-deck://gameobject/{name}"</c>).  Template matching via
    /// <see cref="FindByUri"/> does a linear scan and is O(n) in the number of registered
    /// resources, which is acceptable given that the total number of resources is small.
    /// Not thread-safe — all access is expected on the Unity main thread.
    /// </remarks>
    public class ResourceRegistry
    {
        #region CONSTANTS

        private const char URI_SEGMENT_SEPARATOR = '/';
        private const char TEMPLATE_PARAM_OPEN = '{';
        private const char TEMPLATE_PARAM_CLOSE = '}';
        private const int MIN_PARAMETER_SEGMENT_LENGTH = 2;

        #endregion

        #region FIELDS

        private readonly Dictionary<string, McpResourceInfo> _resources = new();

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the number of resources currently registered.
        /// </summary>
        public int Count => _resources.Count;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Determines whether a route template matches a set of URI segments produced by
        /// splitting a concrete URI on <c>'/'</c>.
        /// </summary>
        /// <param name="routeTemplate">The route template string, e.g. <c>"mcp-game-deck://assets/{filter}"</c>.</param>
        /// <param name="uriSegments">The pre-split segments of the concrete URI.</param>
        /// <returns><c>true</c> if the template matches; otherwise <c>false</c>.</returns>
        private static bool MatchesTemplate(string routeTemplate, string[] uriSegments)
        {
            string[] templateSegments = routeTemplate.Split(URI_SEGMENT_SEPARATOR);

            if (templateSegments.Length != uriSegments.Length)
            {
                return false;
            }

            for (int i = 0; i < templateSegments.Length; i++)
            {
                string templateSegment = templateSegments[i];

                bool isParameter = templateSegment.Length >= MIN_PARAMETER_SEGMENT_LENGTH && templateSegment[0] == TEMPLATE_PARAM_OPEN && templateSegment[^1] == TEMPLATE_PARAM_CLOSE;

                if (isParameter)
                {
                    if (uriSegments[i].Length == 0)
                    {
                        return false;
                    }
                }
                else
                {
                    if (!string.Equals(templateSegment, uriSegments[i], StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Registers a resource by its <see cref="McpResourceInfo.Route"/> template.
        /// If a resource with the same route is already registered, the duplicate is ignored
        /// and a warning is logged to the Unity console.
        /// </summary>
        /// <param name="resource">The resource metadata to register. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="resource"/> is <c>null</c>.</exception>
        public void Register(McpResourceInfo resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (_resources.TryGetValue(resource.Route, out McpResourceInfo? existing))
            {
                Debug.LogWarning(
                    $"[ResourceRegistry] Duplicate resource route \"{resource.Route}\" — " +
                    $"skipping registration of {resource.DeclaringType.FullName}.{resource.Method.Name}. " +
                    $"Already registered by {existing.DeclaringType.FullName}.");
                return;
            }

            _resources[resource.Route] = resource;
        }

        /// <summary>
        /// Finds a registered resource whose route template matches the given concrete URI.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Template matching works by splitting both the route template and the URI on the
        /// <c>'/'</c> character and comparing segment by segment:
        /// </para>
        /// <list type="bullet">
        ///   <item>A literal segment (no braces) must match the corresponding URI segment exactly.</item>
        ///   <item>A parameter segment (e.g. <c>{name}</c>) matches any non-empty URI segment.</item>
        /// </list>
        /// <para>
        /// The method returns the first route template whose segment count equals the URI segment
        /// count and all literal segments match. If multiple templates could match (ambiguous
        /// routes), the first match found wins. Registration order is unspecified because the
        /// backing store is a dictionary — avoid registering ambiguous routes.
        /// </para>
        /// <para>
        /// The scheme separator <c>"://"</c> is treated as part of the first segment split;
        /// e.g. <c>"mcp-game-deck://gameobject/{name}"</c> splits into
        /// <c>["mcp-game-deck:", "", "gameobject", "{name}"]</c> and the URI is split
        /// identically, so segment counts and positions align correctly.
        /// </para>
        /// </remarks>
        /// <param name="uri">
        /// The concrete resource URI from the MCP request,
        /// e.g. <c>"mcp-game-deck://gameobject/Player"</c>.
        /// </param>
        /// <returns>
        /// The first <see cref="McpResourceInfo"/> whose route template matches <paramref name="uri"/>,
        /// or <c>null</c> if no registered route matches.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <c>null</c>.</exception>
        public McpResourceInfo? FindByUri(string uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            string[] uriSegments = uri.Split(URI_SEGMENT_SEPARATOR);

            foreach (KeyValuePair<string, McpResourceInfo> pair in _resources)
            {
                if (MatchesTemplate(pair.Key, uriSegments))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a snapshot list of all registered resources in an unspecified order.
        /// </summary>
        /// <remarks>
        /// Returns a new <see cref="List{T}"/> each time so callers cannot mutate internal state.
        /// </remarks>
        /// <returns>A new list containing every registered <see cref="McpResourceInfo"/>.</returns>
        public List<McpResourceInfo> GetAllResources()
        {
            List<McpResourceInfo> result = new(_resources.Count);

            foreach (KeyValuePair<string, McpResourceInfo> pair in _resources)
            {
                result.Add(pair.Value);
            }

            return result;
        }

        #endregion
    }
}
