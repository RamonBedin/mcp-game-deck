#nullable enable
using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    public partial class ChatWindow
    {
        #region MESSAGE HANDLERS

        /// <summary>
        /// Routes an incoming JSON message from the Agent SDK Server to the appropriate UI handler.
        /// Handles assistant text (streaming and final), tool use/result, thinking blocks,
        /// session results with cost tracking, errors, pong, agent/session list responses,
        /// and session history restoration.
        /// </summary>
        /// <param name="json">Raw JSON string received over WebSocket.</param>
        private void HandleServerMessage(string json)
        {
            try
            {
                var type = ExtractJsonString(json, "type");

                switch (type)
                {
                    case "assistant":
                        var content = ExtractJsonString(json, "content") ?? "";
                        var streaming = json.Contains("\"streaming\":true");
                        _renderer?.AddAssistantMessage(content, streaming);
                        break;

                    case "tool_use":
                        var toolName = ExtractJsonString(json, "name") ?? "unknown";
                        if (toolName.StartsWith(ChatConstants.MCP_TOOL_PREFIX))
                        {
                            toolName = toolName[ChatConstants.MCP_TOOL_PREFIX_LENGTH..];
                        }
                        _renderer?.AddToolExecution(toolName, "...");
                        break;

                    case "tool_result":
                        var resultName = ExtractJsonString(json, "name") ?? "unknown";
                        var success = json.Contains("\"success\":true");
                        var output = ExtractJsonString(json, "output") ?? "";
                        _renderer?.UpdateToolResult(resultName, success, output);
                        break;

                    case "thinking":
                        var thinking = ExtractJsonString(json, "content") ?? "";
                        _renderer?.AddThinkingBlock(thinking);
                        break;

                    case "result":
                        _renderer?.FinalizeToolExecution();
                        _renderer?.FinalizeStreaming();
                        _currentSessionId = ExtractJsonString(json, "sessionId");
                        if (!string.IsNullOrEmpty(_currentSessionId))
                        {
                            EditorPrefs.SetString(SESSION_PREF_KEY, _currentSessionId!);
                        }
                        var costStr = ExtractJsonValue(json, "costUsd");
                        if (float.TryParse(costStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float cost))
                        {
                            _sessionCost += cost;
                            if (_costLabel != null)
                            {
                                _costLabel.text = string.Format(CultureInfo.InvariantCulture, "Session Cost: ${0:F3}", _sessionCost);
                            }
                        }
                        SetGenerating(false);
                        RefreshSessionList();
                        break;

                    case "error":
                        var errorMsg = ExtractJsonString(json, "message") ?? "Unknown error";
                        _renderer?.FinalizeStreaming();
                        _renderer?.AddAssistantMessage($"Error: {errorMsg}", false);
                        SetGenerating(false);
                        break;

                    case "pong":
                        var mcpConnected = json.Contains("\"mcpConnected\":true");
                        if (!mcpConnected && _statusLabel != null)
                        {
                            _statusLabel.text = "MCP Offline";
                        }
                        break;

                    case "agents":
                        PopulateAgentDropdown(json);
                        break;

                    case "commands":
                        OnCommandsReceived(json);
                        break;

                    case "sessions":
                        PopulateSessionList(json);
                        break;

                    case "session-history":
                        RenderSessionHistory(json);
                        break;

                    case "permission_request":
                        HandlePermissionRequest(json);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Game Deck Chat] Error handling message: {ex}");
            }
        }

        /// <summary>
        /// Parses a permission_request message and renders an Accept/Reject UI block.
        /// Sends a permission_response back to the server when the user clicks a button.
        /// </summary>
        /// <param name="json">Raw JSON string of the permission_request message.</param>
        private void HandlePermissionRequest(string json)
        {
            var toolName = ExtractJsonString(json, "toolName") ?? "unknown";
            var toolUseId = ExtractJsonString(json, "toolUseId") ?? "";
            var reason = ExtractJsonString(json, "reason");

            if (toolName.StartsWith(ChatConstants.MCP_TOOL_PREFIX))
            {
                toolName = toolName[ChatConstants.MCP_TOOL_PREFIX_LENGTH..];
            }

            var inputStart = json.IndexOf("\"toolInput\":", StringComparison.Ordinal);
            string inputSummary = "";

            if (inputStart >= 0)
            {
                var objStart = json.IndexOf('{', inputStart);

                if (objStart >= 0)
                {
                    int depth = 0;

                    for (int i = objStart; i < json.Length; i++)
                    {
                        if (json[i] == '{')
                        {
                            depth++;
                        }
                        else if (json[i] == '}')
                        {
                            depth--;

                            if (depth == 0)
                            {
                                inputSummary = json.Substring(objStart, i - objStart + 1);
                                break;
                            }
                        }
                    }
                }
            }

            _renderer?.AddPermissionRequest(
                toolName,
                inputSummary,
                reason,
                onAccept: async () =>
                {
                    if (_wsClient != null)
                    {
                        await _wsClient.SendAsync(
                            $"{{\"action\":\"permission_response\",\"toolUseId\":{EscapeJson(toolUseId)},\"allow\":true}}");
                    }
                },
                onReject: async () =>
                {
                    if (_wsClient != null)
                    {
                        await _wsClient.SendAsync(
                            $"{{\"action\":\"permission_response\",\"toolUseId\":{EscapeJson(toolUseId)},\"allow\":false,\"message\":\"User denied permission.\"}}");
                    }
                }
            );
        }

        /// <summary>
        /// Renders historical messages from a session-history server response.
        /// </summary>
        /// <param name="json">Raw JSON containing messages array.</param>
        private void RenderSessionHistory(string json)
        {
            if (_renderer == null)
            {
                return;
            }

            int idx = 0;

            while (true)
            {
                var role = ExtractJsonStringFromArray(json, "messages", idx, "role");

                if (role == null)
                {
                    break;
                }

                var content = ExtractJsonStringFromArray(json, "messages", idx, "content") ?? "";

                if (role == "user")
                {
                    _renderer.AddUserMessage(content);
                }
                else if (role == "assistant")
                {
                    _renderer.AddAssistantMessage(content, false);
                }

                idx++;
            }

            if (idx == 0)
            {
                _currentSessionId = null;
                _hasMessages = false;

                if (_welcomeScreen != null)
                {
                    _welcomeScreen.style.display = DisplayStyle.Flex;
                }
            }
        }

        /// <summary>
        /// Populates the agent dropdown with names extracted from the server's
        /// <c>agents</c> JSON response. Restores the previously selected agent
        /// if it still exists in the list, otherwise defaults to <see cref="ChatConstants.AGENT_DEFAULT_LABEL"/>.
        /// </summary>
        /// <param name="json">Raw JSON string containing the agents array from the server.</param>
        private void PopulateAgentDropdown(string json)
        {
            if (_agentDropdown == null)
            {
                return;
            }

            var choices = new System.Collections.Generic.List<string> { ChatConstants.AGENT_DEFAULT_LABEL };
            int idx = 0;

            while (true)
            {
                var name = ExtractJsonStringFromArray(json, "agents", idx, "name");

                if (name == null)
                {
                    break;
                }

                choices.Add(name);
                idx++;
            }

            _agentDropdown.choices = choices;

            var restore = !string.IsNullOrEmpty(_selectedAgent) && choices.Contains(_selectedAgent!) ? _selectedAgent! : ChatConstants.AGENT_DEFAULT_LABEL;
            _agentDropdown.SetValueWithoutNotify(restore);
        }

        /// <summary>
        /// Populates the sidebar session list from the server's JSON response.
        /// Each session item is clickable to resume, with a delete button.
        /// </summary>
        /// <param name="json">Raw JSON containing the sessions array.</param>
        private void PopulateSessionList(string json)
        {
            if (_sessionList == null)
            {
                return;
            }

            _sessionList.Clear();

            int idx = 0;

            while (true)
            {
                var sessionId = ExtractJsonStringFromArray(json, "sessions", idx, "id");

                if (sessionId == null)
                {
                    break;
                }

                var lastPrompt = ExtractJsonStringFromArray(json, "sessions", idx, "lastPrompt") ?? "New conversation";
                var title = lastPrompt.Length > ChatConstants.SESSION_TITLE_MAX_LENGTH ? lastPrompt[..ChatConstants.SESSION_TITLE_MAX_LENGTH] + "..." : lastPrompt;

                var item = new VisualElement();
                item.AddToClassList("session-item");

                if (sessionId == _currentSessionId)
                {
                    item.AddToClassList("session-item--active");
                }

                var titleLabel = new Label(title);
                titleLabel.AddToClassList("session-title");
                item.Add(titleLabel);

                var meta = new VisualElement();
                meta.AddToClassList("session-meta");

                var dateLabel = new Label($"#{idx + 1}");
                dateLabel.AddToClassList("session-date");
                meta.Add(dateLabel);

                string capturedId = sessionId;
                var deleteBtn = new Button(() => DeleteSessionWithConfirm(capturedId)) { text = "x" };
                deleteBtn.AddToClassList("session-delete");
                meta.Add(deleteBtn);

                item.Add(meta);

                item.RegisterCallback<ClickEvent>(_ => ResumeSession(capturedId));

                _sessionList.Add(item);
                idx++;
            }
        }

        #endregion
    }
}