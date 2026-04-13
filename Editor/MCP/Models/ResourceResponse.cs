#nullable enable

using System;

namespace GameDeck.MCP.Models
{
    /// <summary>
    /// Represents the content returned by an MCP resource handler.
    /// This is the standalone replacement for <c>ResponseResourceContent</c> from
    /// <c>com.IvanMurzak.McpPlugin.Common.Model</c>. Migration is a type rename only —
    /// the static factory API and the <see cref="MakeArray"/> convenience method are identical.
    /// </summary>
    /// <remarks>
    /// MCP resource methods return <c>ResourceResponse[]</c>. Use <see cref="MakeArray"/>
    /// to wrap a single instance into a single-element array, matching the expected return type.
    /// </remarks>
    public sealed class ResourceResponse
    {
        #region CONSTRUCTOR

        private ResourceResponse(string uri, string mimeType, string text)
        {
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the URI that identifies this resource content, as requested by the MCP client.
        /// Typically matches the route template with parameters substituted in,
        /// e.g. <c>"mcp-game-deck://assets/t:Prefab"</c>.
        /// </summary>
        public string Uri { get; private set; }

        /// <summary>
        /// Gets the MIME type of the text content, e.g. <c>"text/plain"</c> or
        /// <c>"application/json"</c>. Used by MCP clients to interpret the <see cref="Text"/> value.
        /// </summary>
        public string MimeType { get; private set; }

        /// <summary>
        /// Gets the text content of the resource response.
        /// For JSON resources this is a JSON string; for plain-text resources it is human-readable prose.
        /// </summary>
        public string Text { get; private set; }

        #endregion

        #region STATIC FACTORY METHODS

        /// <summary>
        /// Creates a text-based resource response.
        /// This is the primary factory method and the direct replacement for
        /// <c>ResponseResourceContent.CreateText(uri, mimeType, text)</c>.
        /// </summary>
        /// <param name="uri">
        /// The resource URI, e.g. <c>"mcp-game-deck://assets/t:Prefab"</c>. Must not be <c>null</c>.
        /// </param>
        /// <param name="mimeType">
        /// The MIME type of the content, e.g. <c>"text/plain"</c> or <c>"application/json"</c>.
        /// Must not be <c>null</c>.
        /// </param>
        /// <param name="text">The content to return to the MCP client. Must not be <c>null</c>.</param>
        /// <returns>A fully initialized <see cref="ResourceResponse"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any parameter is <c>null</c>.
        /// </exception>
        public static ResourceResponse CreateText(string uri, string mimeType, string text)
        {
            return new ResourceResponse(uri: uri, mimeType: mimeType, text: text);
        }

        #endregion

        #region INSTANCE METHODS

        /// <summary>
        /// Wraps this instance in a single-element array.
        /// MCP resource methods must return <c>ResourceResponse[]</c>; this helper eliminates
        /// the boilerplate of <c>new[] { response }</c> at every call site.
        /// </summary>
        /// <returns>A new array containing only this <see cref="ResourceResponse"/>.</returns>
        /// <example>
        /// <code>
        /// return ResourceResponse.CreateText(uri, "text/plain", content).MakeArray();
        /// </code>
        /// </example>
        public ResourceResponse[] MakeArray()
        {
            return new ResourceResponse[] { this };
        }

        #endregion
    }
}
