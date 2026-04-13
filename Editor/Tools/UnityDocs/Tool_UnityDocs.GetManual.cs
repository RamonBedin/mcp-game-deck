#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UnityDocs
    {
        #region TOOL METHODS

        /// <summary>
        /// Fetches a Unity Manual page by slug and returns a parsed summary with title,
        /// sections, and code examples.
        /// </summary>
        /// <param name="slug">Manual page slug (e.g. 'execution-order', 'physics-overview').</param>
        /// <param name="version">Unity version for versioned docs (e.g. '6000.0'). Empty for latest.</param>
        /// <returns>A <see cref="ToolResponse"/> with parsed manual content, or an error.</returns>
        [McpTool("unity-docs-manual", Title = "Unity Docs / Get Manual Page")]
        [Description("Fetches a Unity Manual page by slug and returns a parsed summary with title, " + "sections, and code examples. Useful for conceptual documentation like 'execution-order', " + "'physics-overview', or 'UIE-USS-Properties-Reference'.")]
        public async Task<ToolResponse> GetManual(
            [Description("Manual page slug (e.g. 'execution-order', 'physics-overview', " + "'UIE-USS-Properties-Reference'). This is the last part of the URL path.")] string slug,
            [Description("Unity version for versioned docs (e.g. '6000.0'). Empty for latest.")] string version = ""
        )
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return ToolResponse.Error("slug is required.");
            }

            string baseUrl = string.IsNullOrWhiteSpace(version) ? "https://docs.unity3d.com/Manual" : $"https://docs.unity3d.com/{version}/Documentation/Manual";

            string url = $"{baseUrl}/{slug}.html";
            try
            {
                using var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return ToolResponse.Text($"Manual page not found for slug '{slug}'. " + "Check the slug matches the Manual page URL path.");
                }

                var html = await response.Content.ReadAsStringAsync();
                var sb = new StringBuilder();
                var titleMatch = Regex.Match(html, @"<h1>(.*?)</h1>", RegexOptions.Singleline);
                string title = titleMatch.Success ? StripHtmlTags(titleMatch.Groups[1].Value).Trim() : slug;

                sb.AppendLine($"Unity Manual: {title}");
                sb.AppendLine($"URL: {url}");
                sb.AppendLine();


                var sectionMatches = Regex.Matches(html, @"<h[23][^>]*>(.*?)</h[23]>(.*?)(?=<h[23]|<footer|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match section in sectionMatches)
                {
                    var heading = StripHtmlTags(section.Groups[1].Value).Trim();
                    var content = section.Groups[2].Value;
                    var paragraphs = Regex.Matches(content, @"<p>(.*?)</p>", RegexOptions.Singleline);

                    if (paragraphs.Count > 0)
                    {
                        sb.AppendLine($"## {heading}");

                        foreach (Match p in paragraphs)
                        {
                            var text = StripHtmlTags(p.Groups[1].Value).Trim();

                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                sb.AppendLine($"  {text}");
                            }
                        }

                        sb.AppendLine();
                    }
                }

                var codeMatches = Regex.Matches(html, @"<pre[^>]*>(.*?)</pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                if (codeMatches.Count > 0)
                {
                    sb.AppendLine("Code Examples:");

                    foreach (Match code in codeMatches)
                    {
                        var codeText = StripHtmlTags(code.Groups[1].Value).Trim();

                        if (!string.IsNullOrWhiteSpace(codeText))
                        {
                            sb.AppendLine("```");
                            sb.AppendLine(codeText);
                            sb.AppendLine("```");
                            sb.AppendLine();
                        }
                    }
                }

                return ToolResponse.Text(sb.ToString());
            }
            catch (Exception ex)
            {
                return ToolResponse.Error($"Failed to fetch manual page: {ex.Message}");
            }
        }

        #endregion
    }
}