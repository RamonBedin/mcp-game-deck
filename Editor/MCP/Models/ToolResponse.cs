#nullable enable

using System;
using System.Text;
using UnityEngine;

namespace GameDeck.MCP.Models
{
    /// <summary>
    /// Represents the response returned by an MCP tool method.
    /// This is the standalone replacement for <c>ResponseCallTool</c> from
    /// <c>com.IvanMurzak.McpPlugin.Common.Model</c>. Migration is a type rename only —
    /// the static factory API is identical.
    /// </summary>
    /// <remarks>
    /// Use the static factory methods to construct instances rather than the constructor directly.
    /// <list type="bullet">
    ///   <item><description><see cref="Text"/> — plain text result</description></item>
    ///   <item><description><see cref="Error"/> — error message, sets <see cref="IsError"/> to <c>true</c></description></item>
    ///   <item><description><see cref="Success"/> — JSON-serialized data object</description></item>
    ///   <item><description><see cref="Image"/> — base64-encoded image</description></item>
    ///   <item><description><see cref="Json"/> — JSON text from any object</description></item>
    /// </list>
    /// </remarks>
    public sealed class ToolResponse
    {
        #region CONSTRUCTOR

        private ToolResponse(string content, bool isError, string? mimeType, byte[]? imageData, string? altText = null)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            IsError = isError;
            MimeType = mimeType;
            AltText = altText;
        }

        #endregion

        #region CONSTANTS

        private const string MIME_TYPE_JSON = "application/json";
        private const string EMPTY_JSON_OBJECT = "{}";

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets a value indicating whether this response represents a tool error.
        /// When <c>true</c> the MCP client will surface the response as an error to the caller.
        /// </summary>
        public bool IsError { get; private set; }

        /// <summary>
        /// Gets the primary text or JSON content of the response.
        /// For image responses this contains the base64-encoded representation.
        /// </summary>
        public string Content { get; private set; }

        /// <summary>
        /// Gets the MIME type of the response content, or <c>null</c> for plain text responses.
        /// Common values are <c>"application/json"</c>, <c>"image/png"</c>, <c>"image/jpeg"</c>.
        /// </summary>
        public string? MimeType { get; private set; }

        /// <summary>
        /// Gets the optional alt-text description for image responses, or <c>null</c> for
        /// non-image responses. Kept separate from <see cref="Content"/> so that the base64
        /// data is never mixed with descriptive text.
        /// </summary>
        public string? AltText { get; private set; }

        #endregion

        #region STATIC FACTORY METHODS

        /// <summary>
        /// Creates a plain-text success response.
        /// </summary>
        /// <param name="text">The text content to return to the MCP client. Must not be <c>null</c>.</param>
        /// <returns>A <see cref="ToolResponse"/> with <see cref="IsError"/> set to <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <c>null</c>.</exception>
        public static ToolResponse Text(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new ToolResponse(content: text, isError: false, mimeType: null, imageData: null);
        }

        /// <summary>
        /// Creates an error response. The MCP client will surface this as a tool failure.
        /// </summary>
        /// <param name="message">A human-readable description of the error. Must not be <c>null</c>.</param>
        /// <returns>A <see cref="ToolResponse"/> with <see cref="IsError"/> set to <c>true</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <c>null</c>.</exception>
        public static ToolResponse Error(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new ToolResponse(content: message, isError: true, mimeType: null, imageData: null);
        }

        /// <summary>
        /// Creates a JSON success response from any object. The object is serialized with
        /// <see cref="JsonUtility"/> when possible; otherwise <c>ToString()</c> is used as fallback.
        /// </summary>
        /// <param name="data">The data object to serialize. Must not be <c>null</c>.</param>
        /// <returns>A <see cref="ToolResponse"/> whose <see cref="Content"/> contains JSON text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <c>null</c>.</exception>
        public static ToolResponse Success(object data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var json = SerializeToJson(data);
            return new ToolResponse(content: json, isError: false, mimeType: MIME_TYPE_JSON, imageData: null);
        }

        /// <summary>
        /// Creates a base64-encoded image response.
        /// </summary>
        /// <param name="data">The raw image bytes. Must not be <c>null</c> or empty.</param>
        /// <param name="mimeType">The MIME type of the image, e.g. <c>"image/png"</c>. Must not be <c>null</c>.</param>
        /// <param name="alt">Optional alt-text description of the image for accessibility.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> whose <see cref="Content"/> is the base64 string,
        /// <see cref="MimeType"/> is the supplied MIME type.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="mimeType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is empty.</exception>
        public static ToolResponse Image(byte[] data, string mimeType, string alt = "")
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (mimeType == null)
            {
                throw new ArgumentNullException(nameof(mimeType));
            }

            if (data.Length == 0)
            {
                throw new ArgumentException("Image data must not be empty.", nameof(data));
            }

            var base64 = Convert.ToBase64String(data);
            var altText = string.IsNullOrEmpty(alt) ? null : alt;
            return new ToolResponse(content: base64, isError: false, mimeType: mimeType, imageData: data, altText: altText);
        }

        /// <summary>
        /// Creates a JSON-text response from any object. Delegates to <see cref="Success"/>
        /// — provided as a semantic alias for call sites that want to emphasise the JSON format.
        /// </summary>
        /// <param name="obj">The object to serialize. Must not be <c>null</c>.</param>
        /// <returns>A <see cref="ToolResponse"/> whose <see cref="Content"/> contains JSON text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is <c>null</c>.</exception>
        public static ToolResponse Json(object obj)
        {
            return Success(obj);
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Serializes <paramref name="obj"/> to a JSON string.
        /// Uses <see cref="JsonUtility"/> for Unity objects and falls back to a manual
        /// key-value representation for primitive types and plain C# objects that
        /// <see cref="JsonUtility"/> cannot handle.
        /// </summary>
        /// <param name="obj">The object to serialize. Must not be <c>null</c>.</param>
        /// <returns>A non-null JSON string representing the object.</returns>
        private static string SerializeToJson(object obj)
        {
            try
            {
                var json = JsonUtility.ToJson(obj, prettyPrint: false);

                if (!string.IsNullOrEmpty(json) && json != EMPTY_JSON_OBJECT)
                {
                    return json;
                }
            }
            catch(Exception ex)
            {
                Debug.LogWarning($"[ToolResponse] JsonUtility.ToJson failed for {obj.GetType().Name}, falling back to manual serialization: {ex.Message}");
            }

            var text = obj.ToString() ?? string.Empty;
            var sb = new StringBuilder(text.Length + 16);
            sb.Append("{\"value\":\"");

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    sb.Append("\\\"");
                }
                else if (c == '\\')
                {
                    sb.Append("\\\\");
                }
                else if (c == '\n')
                {
                    sb.Append("\\n");
                }
                else if (c == '\r')
                {
                    sb.Append("\\r");
                }
                else if (c == '\t')
                {
                    sb.Append("\\t");
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append("\"}");
            return sb.ToString();
        }

        #endregion
    }
}
