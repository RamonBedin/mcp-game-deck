#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    /// <summary>
    /// Renders chat messages into the ScrollView messages container.
    /// Creates UI elements for user messages, assistant messages, tool executions,
    /// code blocks, and thinking blocks. Supports basic markdown rendering and streaming.
    /// </summary>
    public class MessageRenderer
    {
        #region CONSTRUCTOR

        public MessageRenderer(VisualElement container, ScrollView scrollView)
        {
            _container = container;
            _scrollView = scrollView;
        }

        #endregion

        #region FIELDS

        private readonly VisualElement _container;
        private readonly ScrollView _scrollView;
        private Label? _streamingLabel;
        private VisualElement? _streamingBubble;
        private VisualElement? _typingIndicator;
        private IVisualElementScheduledItem? _typingAnimation;
        private int _typingFrame;
        private VisualElement? _currentToolRow;
        private Label? _currentToolName;
        private Label? _currentToolStatus;
        private int _toolCallCount;
        private VisualElement[]? _typingDots;
        private VisualElement? _permissionSummaryRow;
        private Label? _permissionSummaryLabel;
        private VisualElement? _activePermissionBlock;
        private int _permissionAllowedCount;
        private int _permissionDeniedCount;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Scrolls the chat <see cref="_scrollView"/> to the bottom.
        /// Fires twice — once on the next frame and again after 50ms — to handle
        /// content that takes more than one frame to finish layout.
        /// </summary>
        private void ScrollToBottom()
        {
            _scrollView.schedule.Execute(() =>
            {
                _scrollView.scrollOffset = new Vector2(0, float.MaxValue);
            });

            _scrollView.schedule.Execute(() =>
            {
                _scrollView.scrollOffset = new Vector2(0, float.MaxValue);
            }).ExecuteLater(ChatConstants.SCROLL_DELAY_MS);
        }

        /// <summary>
        /// Updates or creates the compact permission summary row that replaces
        /// resolved permission blocks.
        /// </summary>
        private void UpdatePermissionSummary()
        {
            var parts = new System.Text.StringBuilder();

            if (_permissionAllowedCount > 0)
            {
                parts.Append($"\u2705 {_permissionAllowedCount} allowed");
            }

            if (_permissionDeniedCount > 0)
            {
                if (parts.Length > 0) parts.Append("  ");
                parts.Append($"\u274C {_permissionDeniedCount} denied");
            }

            if (_permissionSummaryRow != null && _permissionSummaryLabel != null)
            {
                _permissionSummaryLabel.text = parts.ToString();
                return;
            }

            var row = new VisualElement();
            row.AddToClassList("tool-execution");

            var icon = new Label("\u26A0");
            icon.AddToClassList("tool-icon");
            row.Add(icon);

            var label = new Label(parts.ToString());
            label.AddToClassList("tool-name");
            row.Add(label);

            _permissionSummaryRow = row;
            _permissionSummaryLabel = label;
            _container.Add(row);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Appends a user message bubble to the chat container.
        /// </summary>
        /// <param name="text">The user's message text.</param>
        public void AddUserMessage(string text)
        {
            FinalizeStreaming();

            var bubble = new VisualElement();
            bubble.AddToClassList("message");
            bubble.AddToClassList("message-user");

            var label = new Label(text);
            label.AddToClassList("message-text");
            label.selection.isSelectable = true;
            bubble.Add(label);

            _container.Add(bubble);
            ScrollToBottom();
        }

        /// <summary>
        /// Appends or updates an assistant message bubble.
        /// In streaming mode, text is appended to the current active label.
        /// When streaming completes, markdown is rendered.
        /// </summary>
        /// <param name="text">The assistant message text or delta to append.</param>
        /// <param name="streaming">When true, text is appended to the current streaming label rather than creating a new bubble.</param>
        public void AddAssistantMessage(string text, bool streaming)
        {
            HideTypingIndicator();

            if (streaming && _streamingLabel != null)
            {
                _streamingLabel.text += text;
                ScrollToBottom();
                return;
            }

            FinalizeStreaming();

            var bubble = new VisualElement();
            bubble.AddToClassList("message");
            bubble.AddToClassList("message-assistant");

            if (streaming)
            {
                var label = new Label(text);
                label.AddToClassList("message-text");
                label.selection.isSelectable = true;
                bubble.Add(label);
                _streamingLabel = label;
                _streamingBubble = bubble;
            }
            else
            {
                RenderMarkdownInto(bubble, text);
            }

            _container.Add(bubble);
            ScrollToBottom();
        }

        /// <summary>
        /// Shows a tool execution in a single updating row. If a tool row already exists,
        /// updates it with the new tool name and increments the counter. This prevents
        /// stacking dozens of tool chips — like Claude Code's single-line tool display.
        /// </summary>
        /// <param name="toolName">The name of the tool being executed.</param>
        /// <param name="status">An optional status string shown alongside the tool name.</param>
        public void AddToolExecution(string toolName, string? status = null)
        {
            _toolCallCount++;

            if (_currentToolRow != null && _currentToolName != null)
            {
                _currentToolName.text = _toolCallCount > 1 ? $"Running {toolName}... ({_toolCallCount} tools)" : $"Running {toolName}...";

                if (_currentToolStatus != null)
                {
                    _currentToolStatus.text = status ?? "";
                }

                ScrollToBottom();
                return;
            }

            var row = new VisualElement();
            row.AddToClassList("tool-execution");

            var icon = new Label("*");
            icon.AddToClassList("tool-icon");
            row.Add(icon);

            var nameLabel = new Label($"Running {toolName}...");
            nameLabel.AddToClassList("tool-name");
            row.Add(nameLabel);

            var statusLabel = new Label(status ?? "");
            statusLabel.AddToClassList("tool-status");
            row.Add(statusLabel);

            _container.Add(row);
            _currentToolRow = row;
            _currentToolName = nameLabel;
            _currentToolStatus = statusLabel;
            ScrollToBottom();
        }

        /// <summary>
        /// Updates the current tool execution row with a success/failure indicator.
        /// </summary>
        /// <param name="toolName">The tool name (for logging, not displayed).</param>
        /// <param name="success">Whether the tool call succeeded.</param>
        /// <param name="output">The output string from the tool.</param>
#pragma warning disable IDE0060 // Remover o parâmetro não utilizado
        public void UpdateToolResult(string toolName, bool success, string output)
#pragma warning restore IDE0060 // Remover o parâmetro não utilizado
        {
            if (_currentToolStatus != null)
            {
                string indicator = success ? "ok" : "error";
                _currentToolStatus.text = indicator;

                if (!success)
                {
                    _currentToolStatus.AddToClassList("tool-status--error");
                }
            }

            ScrollToBottom();
        }

        /// <summary>
        /// Resets the tool execution display so the next tool call creates a fresh row.
        /// Called from <see cref="ChatWindow"/> when the final "result" message arrives,
        /// or when the user sends a new message.
        /// </summary>
        public void FinalizeToolExecution()
        {
            _currentToolRow = null;
            _currentToolName = null;
            _currentToolStatus = null;
            _toolCallCount = 0;
            _permissionSummaryRow = null;
            _permissionSummaryLabel = null;
            _activePermissionBlock = null;
            _permissionAllowedCount = 0;
            _permissionDeniedCount = 0;
        }

        /// <summary>
        /// Appends a collapsible thinking block to the chat container.
        /// </summary>
        /// <param name="content">The thinking content text to display when expanded.</param>
        public void AddThinkingBlock(string content)
        {
            var block = new VisualElement();
            block.AddToClassList("thinking-block");

            var header = new VisualElement();
            header.AddToClassList("thinking-header");

            var headerLabel = new Label("\u25B6 Thinking...");
            headerLabel.AddToClassList("thinking-label");
            header.Add(headerLabel);
            block.Add(header);

            var contentLabel = new Label(content);
            contentLabel.AddToClassList("thinking-content");
            contentLabel.selection.isSelectable = true;
            contentLabel.style.display = DisplayStyle.None;
            block.Add(contentLabel);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool hidden = contentLabel.style.display == DisplayStyle.None;
                contentLabel.style.display = hidden ? DisplayStyle.Flex : DisplayStyle.None;
                headerLabel.text = hidden ? "\u25BC Thinking" : "\u25B6 Thinking...";
            });

            _container.Add(block);
            ScrollToBottom();
        }

        /// <summary>
        /// Renders a permission request block with tool name, input summary,
        /// and Accept/Reject buttons. Resolved permissions collapse into a compact
        /// summary row; only the active request shows a full block.
        /// </summary>
        /// <param name="toolName">The MCP tool requesting permission.</param>
        /// <param name="inputSummary">Formatted summary of the tool input.</param>
        /// <param name="reason">Optional reason the permission was triggered.</param>
        /// <param name="onAccept">Callback invoked when the user clicks Accept.</param>
        /// <param name="onReject">Callback invoked when the user clicks Reject.</param>
        public void AddPermissionRequest(string toolName, string inputSummary, string? reason, System.Action onAccept, System.Action onReject)
        {
            var block = new VisualElement();
            block.AddToClassList("message");
            block.AddToClassList("message-assistant");
            block.style.borderLeftColor = new Color(1f, 0.6f, 0f);
            block.style.borderLeftWidth = 3f;

            var header = new Label($"\u26A0 Permission Request: {toolName}");
            header.AddToClassList("message-text");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            block.Add(header);

            if (!string.IsNullOrEmpty(reason))
            {
                var reasonLabel = new Label(reason);
                reasonLabel.AddToClassList("message-text");
                reasonLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                block.Add(reasonLabel);
            }

            if (!string.IsNullOrEmpty(inputSummary))
            {
                var inputLabel = new Label(inputSummary);
                inputLabel.AddToClassList("message-text");
                inputLabel.selection.isSelectable = true;
                inputLabel.style.whiteSpace = WhiteSpace.PreWrap;
                inputLabel.style.fontSize = 11;
                block.Add(inputLabel);
            }

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 6;

            var acceptBtn = new Button(() =>
            {
                onAccept();
                _permissionAllowedCount++;
                _container.Remove(block);
                UpdatePermissionSummary();
                ScrollToBottom();
            }) { text = "Accept" };

            acceptBtn.style.marginRight = 8;
            acceptBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            acceptBtn.style.color = Color.white;
            buttonRow.Add(acceptBtn);

            var rejectBtn = new Button(() =>
            {
                onReject();
                _permissionDeniedCount++;
                _container.Remove(block);
                UpdatePermissionSummary();
                ScrollToBottom();
            }) { text = "Reject" };
            
            rejectBtn.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f);
            rejectBtn.style.color = Color.white;
            buttonRow.Add(rejectBtn);

            block.Add(buttonRow);
            _activePermissionBlock = block;
            _container.Add(block);
            ScrollToBottom();
        }

        /// <summary>
        /// Clears the active streaming label reference, finalizing the current stream.
        /// When finalized, the raw text is re-rendered with markdown formatting.
        /// </summary>
        public void FinalizeStreaming()
        {
            if (_streamingLabel != null && _streamingBubble != null)
            {
                var rawText = _streamingLabel.text;
                _streamingBubble.Clear();
                RenderMarkdownInto(_streamingBubble, rawText);
            }

            _streamingLabel = null;
            _streamingBubble = null;
            ScrollToBottom();
        }

        /// <summary>
        /// Removes all message elements from the container and resets streaming state.
        /// </summary>
        public void Clear()
        {
            HideTypingIndicator();
            FinalizeToolExecution();
            _container.Clear();
            _streamingLabel = null;
            _streamingBubble = null;
        }

        /// <summary>
        /// Shows a typing indicator (3 animated dots) at the bottom of the chat.
        /// Called when the user sends a message. Hidden when the first response arrives.
        /// </summary>
        public void ShowTypingIndicator()
        {
            if (_typingIndicator != null)
            {
                return;
            }

            _typingIndicator = new VisualElement();
            _typingIndicator.AddToClassList("typing-indicator");
            _typingDots = new VisualElement[3];

            for (int i = 0; i < 3; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("typing-dot");
                _typingIndicator.Add(dot);
                _typingDots[i] = dot;
            }

            _container.Add(_typingIndicator);
            ScrollToBottom();

            _typingFrame = 0;
            _typingAnimation = _typingIndicator.schedule.Execute(() =>
            {
                if (_typingDots == null)
                {
                    return;
                }

                int activeDot = (_typingFrame / ChatConstants.TYPING_FRAME_DIVISOR) % 3;

                for (int i = 0; i < 3; i++)
                {
                    float targetOpacity = (i == activeDot) ? ChatConstants.TYPING_ACTIVE_OPACITY : ChatConstants.TYPING_INACTIVE_OPACITY;
                    float targetScale = (i == activeDot) ? ChatConstants.TYPING_ACTIVE_SCALE : ChatConstants.TYPING_INACTIVE_SCALE;
                    _typingDots[i].style.opacity = targetOpacity;
                    _typingDots[i].style.scale = new Scale(new Vector3(targetScale, targetScale, 1));
                }

                _typingFrame++;

            }).Every(ChatConstants.TYPING_ANIMATION_INTERVAL_MS);
        }

        /// <summary>
        /// Removes the typing indicator from the chat container.
        /// </summary>
        public void HideTypingIndicator()
        {
            if (_typingIndicator == null)
            {
                return;
            }

            _typingAnimation?.Pause();
            _typingAnimation = null;
            _typingDots = null;
            _typingIndicator.RemoveFromHierarchy();
            _typingIndicator = null;
        }

        #endregion

        #region MARKDOWN RENDERING

        /// <summary>
        /// Parses basic markdown and renders it as UI Toolkit elements into the target container.
        /// Supports: code blocks, bold, inline code, and unordered/ordered lists.
        /// Consecutive text lines (paragraphs, lists) are merged into a single selectable
        /// <see cref="Label"/> so the user can select text across paragraphs.
        /// </summary>
        /// <param name="target">The container to add rendered elements to.</param>
        /// <param name="text">The raw markdown text to render.</param>
        private void RenderMarkdownInto(VisualElement target, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] lines = text.Split('\n');
            bool inCodeBlock = false;
            var codeBuffer = new System.Text.StringBuilder();
            var textBuffer = new System.Text.StringBuilder();
            string codeLang = "";

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.TrimStart().StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        FlushTextBuffer(target, textBuffer);
                        inCodeBlock = true;
                        codeLang = line.TrimStart().Length > 3 ? line.TrimStart()[3..].Trim() : "";
                        codeBuffer.Clear();
                    }
                    else
                    {
                        AddCodeBlock(target, codeBuffer.ToString(), codeLang);
                        inCodeBlock = false;
                        codeLang = "";
                    }

                    continue;
                }

                if (inCodeBlock)
                {
                    if (codeBuffer.Length > 0) codeBuffer.Append('\n');
                    codeBuffer.Append(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    textBuffer.Append("\n\n");
                    continue;
                }

                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    if (textBuffer.Length > 0 && !EndsWith(textBuffer, '\n'))
                    {
                        textBuffer.Append('\n');
                    }

                    textBuffer.Append("\u2022 ");
                    textBuffer.Append(RenderInlineMarkdown(trimmed[2..]));
                    continue;
                }

                int dotIdx = trimmed.IndexOf(". ");

                if (dotIdx > 0 && dotIdx <= 3)
                {
                    bool isNumber = true;

                    for (int j = 0; j < dotIdx; j++)
                    {
                        if (trimmed[j] < '0' || trimmed[j] > '9')
                        {
                            isNumber = false;
                            break;
                        }
                    }
                    if (isNumber)
                    {
                        if (textBuffer.Length > 0 && !EndsWith(textBuffer, '\n'))
                        {
                            textBuffer.Append('\n');
                        }

                        textBuffer.Append(trimmed[..(dotIdx + 1)]);
                        textBuffer.Append(' ');
                        textBuffer.Append(RenderInlineMarkdown(trimmed[(dotIdx + 2)..]));
                        continue;
                    }
                }

                if (textBuffer.Length > 0 && !EndsWith(textBuffer, '\n'))
                {
                    textBuffer.Append('\n');
                }

                textBuffer.Append(RenderInlineMarkdown(line));
            }

            FlushTextBuffer(target, textBuffer);

            if (inCodeBlock && codeBuffer.Length > 0)
            {
                AddCodeBlock(target, codeBuffer.ToString(), codeLang);
            }
        }

        /// <summary>
        /// Creates a single selectable <see cref="Label"/> from the accumulated text buffer
        /// and appends it to <paramref name="target"/>. Clears the buffer afterwards.
        /// </summary>
        /// <param name="target">The container to append the label to.</param>
        /// <param name="buffer">The text buffer to flush.</param>
        private static void FlushTextBuffer(VisualElement target, System.Text.StringBuilder buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            string content = buffer.ToString().Trim();
            buffer.Clear();

            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            var label = new Label(content);
            label.AddToClassList("message-text");
            label.selection.isSelectable = true;
            label.style.whiteSpace = WhiteSpace.Normal;
            target.Add(label);
        }

        /// <summary>
        /// Checks if the last character in a <see cref="System.Text.StringBuilder"/> matches the given char.
        /// </summary>
        /// <param name="sb">The StringBuilder to check.</param>
        /// <param name="c">The character to compare against.</param>
        /// <returns>True if the StringBuilder is non-empty and its last character equals <paramref name="c"/>.</returns>
        private static bool EndsWith(System.Text.StringBuilder sb, char c)
        {
            return sb.Length > 0 && sb[^1] == c;
        }

        /// <summary>
        /// Strips inline markdown (bold, inline code) for plain-text Label rendering.
        /// UI Toolkit Labels don't support rich text with mixed styles, so this
        /// removes markdown syntax while keeping the text readable.
        /// </summary>
        /// <param name="text">The line of text with potential inline markdown.</param>
        /// <returns>Clean text with markdown syntax removed.</returns>
        private static string RenderInlineMarkdown(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            int i = 0;

            while (i < text.Length)
            {
                if (i + 1 < text.Length && ((text[i] == '*' && text[i + 1] == '*') || (text[i] == '_' && text[i + 1] == '_')))
                {
                    char marker = text[i];
                    int end = text.IndexOf(new string(marker, 2), i + 2);

                    if (end > 0)
                    {
                        sb.Append(text, i + 2, end - i - 2);
                        i = end + 2;
                        continue;
                    }
                }

                if (text[i] == '`')
                {
                    int end = text.IndexOf('`', i + 1);

                    if (end > 0)
                    {
                        sb.Append(text, i + 1, end - i - 1);
                        i = end + 1;
                        continue;
                    }
                }

                sb.Append(text[i]);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Creates and appends a styled code block with optional language label and copy button.
        /// </summary>
        /// <param name="target">The container to append the code block to.</param>
        /// <param name="code">The code content.</param>
        /// <param name="language">Optional language identifier (e.g., "csharp").</param>
        private static void AddCodeBlock(VisualElement target, string code, string language)
        {
            var block = new VisualElement();
            block.AddToClassList("code-block");

            var header = new VisualElement();
            header.AddToClassList("code-header");

            if (!string.IsNullOrEmpty(language))
            {
                var langLabel = new Label(language.ToUpperInvariant());
                langLabel.AddToClassList("code-lang");
                header.Add(langLabel);
            }
            else
            {
                header.Add(new VisualElement());
            }

            string codeCopy = code;
            var copyBtn = new Button(() => GUIUtility.systemCopyBuffer = codeCopy) { text = "Copy" };
            copyBtn.AddToClassList("code-copy-button");
            header.Add(copyBtn);

            block.Add(header);

            var codeLabel = new Label(code);
            codeLabel.AddToClassList("code-text");
            codeLabel.selection.isSelectable = true;
            block.Add(codeLabel);

            target.Add(block);
        }

        #endregion
    }
}
