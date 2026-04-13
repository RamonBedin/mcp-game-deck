#nullable enable
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for fetching, parsing, and opening Unity ScriptReference and Manual
    /// documentation pages via HTTP. Returns structured summaries with descriptions,
    /// signatures, parameters, and code examples.
    /// </summary>
    [McpToolType]
    public partial class Tool_UnityDocs
    {
        #region FIELDS

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Fetches Unity ScriptReference documentation for a class or member and returns a parsed
        /// summary with description, signatures, and parameters.
        /// </summary>
        /// <param name="className">Unity class name (e.g. 'Physics', 'Transform', 'Rigidbody').</param>
        /// <param name="memberName">Optional member name (method or property, e.g. 'Raycast', 'position').</param>
        /// <param name="version">Unity version for versioned docs (e.g. '6000.0'). Empty for latest.</param>
        /// <returns>A <see cref="ToolResponse"/> with parsed documentation content, or an error.</returns>
        [McpTool("unity-docs-get", Title = "Unity Docs / Get")]
        [Description("Fetches Unity ScriptReference documentation for a class or member and returns " + "a parsed summary with description, signatures, and parameters. Works offline-friendly " + "by extracting key information from the HTML page.")]
        public async Task<ToolResponse> GetDoc(
            [Description("Unity class name (e.g. 'Physics', 'Transform', 'Rigidbody').")] string className,
            [Description("Optional member name (method or property, e.g. 'Raycast', 'position').")] string memberName = "",
            [Description("Unity version for versioned docs (e.g. '6000.0'). Empty for latest.")] string version = ""
        )
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return ToolResponse.Error("className is required.");
            }

            string page = string.IsNullOrWhiteSpace(memberName) ? $"{className}.html" : $"{className}.{memberName}.html";
            string baseUrl = string.IsNullOrWhiteSpace(version) ? "https://docs.unity3d.com/ScriptReference" : $"https://docs.unity3d.com/{version}/Documentation/ScriptReference";

            string url = $"{baseUrl}/{page}";
            try
            {
                var (html, resolvedUrl) = await FetchDocHtml(url, baseUrl, className, memberName);

                if (html == null)
                {
                    return ToolResponse.Text($"Documentation not found for '{className}" + (string.IsNullOrWhiteSpace(memberName) ? "" : $".{memberName}") + "'. Check the class/member name spelling.");
                }

                var parsed = ParseScriptReferenceHtml(html);
                var sb = new StringBuilder();

                sb.AppendLine($"Unity Documentation: {className}" + (string.IsNullOrWhiteSpace(memberName) ? "" : $".{memberName}"));
                sb.AppendLine($"URL: {resolvedUrl}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(parsed._description))
                {
                    sb.AppendLine("Description:");
                    sb.AppendLine($"  {parsed._description}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(parsed._signature))
                {
                    sb.AppendLine("Signature:");
                    sb.AppendLine($"  {parsed._signature}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(parsed._parameters))
                {
                    sb.AppendLine("Parameters:");
                    sb.AppendLine(parsed._parameters);
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(parsed._returns))
                {
                    sb.AppendLine("Returns:");
                    sb.AppendLine($"  {parsed._returns}");
                    sb.AppendLine();
                }

                if (!string.IsNullOrWhiteSpace(parsed._example))
                {
                    sb.AppendLine("Example:");
                    sb.AppendLine(parsed._example);
                }

                return ToolResponse.Text(sb.ToString());
            }
            catch (Exception ex)
            {
                return ToolResponse.Error($"Failed to fetch documentation: {ex.Message}");
            }
        }

        #endregion

        #region PRIVATE HELPER

        /// <summary>
        /// Fetches HTML for a Unity ScriptReference page, falling back to the property-style
        /// (dash-separator) URL when the dot-style URL returns a non-success status.
        /// Updates <paramref name="resolvedUrl"/> to the URL that succeeded.
        /// </summary>
        /// <param name="dotUrl">Dot-style URL to try first.</param>
        /// <param name="baseUrl">Base docs URL used to build the fallback property URL.</param>
        /// <param name="className">Class name used to build the fallback URL.</param>
        /// <param name="memberName">Member name used to build the fallback URL.</param>
        /// <param name="resolvedUrl">Updated to the URL that returned a successful response.</param>
        /// <returns>HTML string on success; <c>null</c> if neither URL succeeds.</returns>
        private static async Task<(string? html, string resolvedUrl)> FetchDocHtml(string dotUrl, string baseUrl, string className, string memberName)
        {
            using var response = await _httpClient.GetAsync(dotUrl);

            if (response.IsSuccessStatusCode)
            {
                return (await response.Content.ReadAsStringAsync(), dotUrl);
            }

            if (!string.IsNullOrWhiteSpace(memberName))
            {
                var propUrl = $"{baseUrl}/{className}-{memberName}.html";
                using var propResponse = await _httpClient.GetAsync(propUrl);

                if (propResponse.IsSuccessStatusCode)
                {
                    return (await propResponse.Content.ReadAsStringAsync(), propUrl);
                }
            }

            return (null, dotUrl);
        }

        /// <summary>
        /// Parses a Unity ScriptReference HTML page and extracts structured sections:
        /// description, signature, parameters, return value, and first code example.
        /// </summary>
        /// <param name="html">Raw HTML content of the ScriptReference page.</param>
        /// <returns>A <see cref="DocParseResult"/> with the extracted documentation sections.</returns>
        private static DocParseResult ParseScriptReferenceHtml(string html)
        {
            var result = new DocParseResult();
            var descMatch = Regex.Match(html, @"<div[^>]*class=""subsection""[^>]*>.*?Description.*?<p>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (descMatch.Success)
            {
                result._description = StripHtmlTags(descMatch.Groups[1].Value).Trim();
            }

            var sigMatch = Regex.Match(html, @"<div[^>]*class=""signature-CS[^""]*""[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (sigMatch.Success)
            {
                result._signature = StripHtmlTags(sigMatch.Groups[1].Value).Trim();
            }

            var paramMatches = Regex.Matches(html,@"<td[^>]*class=""[^""]*name[^""]*""[^>]*>(.*?)</td>\s*<td[^>]*class=""[^""]*desc[^""]*""[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (paramMatches.Count > 0)
            {
                var paramSb = new StringBuilder();

                foreach (Match m in paramMatches)
                {
                    var name = StripHtmlTags(m.Groups[1].Value).Trim();
                    var desc = StripHtmlTags(m.Groups[2].Value).Trim();
                    paramSb.AppendLine($"  {name}: {desc}");
                }

                result._parameters = paramSb.ToString().TrimEnd();
            }

            var retMatch = Regex.Match(html, @"Returns.*?<p>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (retMatch.Success)
            {
                result._returns = StripHtmlTags(retMatch.Groups[1].Value).Trim();
            }

            var exMatch = Regex.Match(html, @"<pre[^>]*class=""[^""]*codeExampleCS[^""]*""[^>]*>(.*?)</pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (exMatch.Success)
            {
                result._example = StripHtmlTags(exMatch.Groups[1].Value).Trim();
            }

            return result;
        }

        /// <summary>
        /// Removes all HTML tags from a string and decodes common HTML entities
        /// (&amp;lt;, &amp;gt;, &amp;amp;, &amp;quot;, &amp;#39;) back to their literal characters.
        /// </summary>
        /// <param name="html">HTML-encoded string to clean.</param>
        /// <returns>Plain text with tags removed and entities decoded.</returns>
        private static string StripHtmlTags(string html)
        {
            return Regex.Replace(html, "<[^>]+>", "").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'");
        }

        /// <summary>
        /// Holds the parsed sections extracted from a Unity ScriptReference HTML page.
        /// </summary>
        private struct DocParseResult
        {
            public string _description;
            public string _signature;
            public string _parameters;
            public string _returns;
            public string _example;
        }

        #endregion
    }
}
