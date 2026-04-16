#nullable enable
using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    /// <summary>
    /// Main EditorWindow for the Game Deck Chat.
    /// Provides a chat interface that communicates with the Agent SDK Server via WebSocket.
    /// </summary>
    public partial class ChatWindow : EditorWindow
    {
        #region CONSTANTS

        private const string SESSION_PREF_KEY = "GameDeck_SessionId";

        #endregion

        #region  SERIALIZED FIELDS

        [SerializeField] private bool _hasMessages;
        [SerializeField] private bool _sidebarVisible = true;
        [SerializeField] private string? _currentSessionId;
        [SerializeField] private float _sessionCost;
        [SerializeField] private string? _selectedAgent;

        #endregion

        #region FIELDS

        private WebSocketClient? _wsClient;
        private MessageRenderer? _renderer;
        private ServerProcessManager? _serverManager;

        private TextField? _promptInput;
        private Button? _sendButton;
        private Button? _stopButton;
        private Button? _newChatButton;
        private Label? _statusLabel;
        private Label? _costLabel;
        private VisualElement? _statusIndicator;
        private VisualElement? _welcomeScreen;
        private VisualElement? _sidebar;
        private Button? _sidebarToggle;
        private Button? _sidebarOpen;
        private Button? _newChatSidebar;
        private ScrollView? _sessionList;
        private DropdownField? _agentDropdown;
        private DropdownField? _modelDropdown;
        private DropdownField? _permissionModeDropdown;
        private ScrollView? _chatScrollView;

        private bool _isGenerating;
        private bool _reloadLocked;

        #endregion

        #region EVENTS

        /// <summary>Named handler for scroll value changes, allowing unsubscribe in OnDestroy.</summary>
        private Action<float>? _scrollValueChangedHandler;

        /// <summary>Named handler for geometry changes, allowing unsubscribe in OnDestroy.</summary>
        private EventCallback<GeometryChangedEvent>? _geometryChangedHandler;

        /// <summary>Named handler for WebSocket connection state changes.</summary>
        private Action<WebSocketClient.EConnectionState>? _wsStateHandler;

        /// <summary>Named handler for incoming WebSocket messages.</summary>
        private Action<string>? _wsMessageHandler;

        /// <summary>Named handler for WebSocket errors.</summary>
        private Action<string>? _wsErrorHandler;

        #endregion

        #region UNITY CALLBACKS

        protected void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_currentSessionId))
            {
                EditorPrefs.SetString(SESSION_PREF_KEY, _currentSessionId);
            }

            if (_reloadLocked)
            {
                _reloadLocked = false;
                EditorApplication.UnlockReloadAssemblies();
            }

            if (_chatScrollView != null)
            {
                if (_scrollValueChangedHandler != null)
                {
                    _chatScrollView.verticalScroller.valueChanged -= _scrollValueChangedHandler;
                }

                if (_geometryChangedHandler != null)
                {
                    _chatScrollView.UnregisterCallback(_geometryChangedHandler);
                }
            }

            if (_wsClient != null)
            {
                if (_wsStateHandler != null)
                {
                    _wsClient.OnStateChanged -= _wsStateHandler;
                }

                if (_wsMessageHandler != null)
                {
                    _wsClient.OnMessageReceived -= _wsMessageHandler;
                }

                if (_wsErrorHandler != null)
                {
                    _wsClient.OnError -= _wsErrorHandler;
                }

                _wsClient.Dispose();
            }

            EditorApplication.quitting -= HandleOnEditorQuitting;
            _serverManager?.Dispose();
        }

        [MenuItem(ChatConstants.MENU_PATH)]
        public static void ShowWindow()
        {
            var window = GetWindow<ChatWindow>();
            window.titleContent = new GUIContent("Game Deck Chat");
            window.minSize = new Vector2(ChatConstants.MIN_WINDOW_WIDTH, ChatConstants.MIN_WINDOW_HEIGHT);
        }

        #endregion

        #region INITIALIZATION METHODS

        protected void CreateGUI()
        {
            var packagePath = ResolvePackageAssetPath();
            var uxmlPath = $"{packagePath}/Editor/ChatUI/ChatWindow.uxml";
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

            if (uxml != null)
            {
                uxml.CloneTree(rootVisualElement);
            }
            else
            {
                rootVisualElement.Add(new Label("Error: ChatWindow.uxml not found at " + uxmlPath));
                return;
            }

            var ussPath = $"{packagePath}/Editor/ChatUI/ChatWindow.uss";
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);

            if (uss != null)
            {
                rootVisualElement.styleSheets.Add(uss);
            }

            _promptInput = rootVisualElement.Q<TextField>("PromptInput");
            _sendButton = rootVisualElement.Q<Button>("SendButton");
            _stopButton = rootVisualElement.Q<Button>("StopButton");
            _newChatButton = rootVisualElement.Q<Button>("NewChatButton");
            _statusLabel = rootVisualElement.Q<Label>("StatusLabel");
            _costLabel = rootVisualElement.Q<Label>("CostLabel");
            _statusIndicator = rootVisualElement.Q<VisualElement>("StatusIndicator");
            _welcomeScreen = rootVisualElement.Q<VisualElement>("WelcomeScreen");
            _sidebar = rootVisualElement.Q<VisualElement>("Sidebar");
            _sidebarToggle = rootVisualElement.Q<Button>("SidebarToggle");
            _sidebarOpen = rootVisualElement.Q<Button>("SidebarOpen");
            _newChatSidebar = rootVisualElement.Q<Button>("NewChatSidebar");
            _sessionList = rootVisualElement.Q<ScrollView>("SessionList");
            _agentDropdown = rootVisualElement.Q<DropdownField>("AgentDropdown");
            _modelDropdown = rootVisualElement.Q<DropdownField>("ModelDropdown");
            _permissionModeDropdown = rootVisualElement.Q<DropdownField>("PermissionModeDropdown");
            _chatScrollView = rootVisualElement.Q<ScrollView>("ChatScrollView");
            _attachPreviewArea = rootVisualElement.Q<VisualElement>("AttachPreviewArea");

            var messagesContainer = rootVisualElement.Q<VisualElement>("MessagesContainer");

            if (_chatScrollView != null && messagesContainer != null)
            {
                _renderer = new MessageRenderer(messagesContainer, _chatScrollView);
            }

            var chatArea = rootVisualElement.Q<VisualElement>("ChatArea");

            if (_chatScrollView != null && chatArea != null)
            {
                var scrollBtn = new Button(() =>
                {
                    if (_chatScrollView != null)
                    {
                        _chatScrollView.scrollOffset = new Vector2(0, float.MaxValue);
                    }

                })
                { text = "\u2193" };
                scrollBtn.AddToClassList("scroll-to-bottom-btn");
                scrollBtn.style.display = DisplayStyle.None;
                chatArea.Add(scrollBtn);

                void UpdateScrollButton()
                {
                    if (_chatScrollView == null)
                    {
                        return;
                    }

                    float contentH = _chatScrollView.contentContainer.layout.height;
                    float viewH = _chatScrollView.layout.height;
                    float scrollY = _chatScrollView.scrollOffset.y;
                    bool atBottom = contentH <= viewH || scrollY >= contentH - viewH - ChatConstants.SCROLL_BOTTOM_THRESHOLD;

                    scrollBtn.style.display = atBottom ? DisplayStyle.None : DisplayStyle.Flex;
                }

                _scrollValueChangedHandler = _ => UpdateScrollButton();
                _geometryChangedHandler = _ => UpdateScrollButton();

                _chatScrollView.verticalScroller.valueChanged += _scrollValueChangedHandler;
                _chatScrollView.RegisterCallback(_geometryChangedHandler);
            }

            _sendButton?.RegisterCallback<ClickEvent>(_ => HandleSendPrompt());
            _stopButton?.RegisterCallback<ClickEvent>(_ => CancelGeneration());
            _newChatButton?.RegisterCallback<ClickEvent>(_ => StartNewChat());
            _newChatSidebar?.RegisterCallback<ClickEvent>(_ => StartNewChat());
            _sidebarToggle?.RegisterCallback<ClickEvent>(_ => ToggleSidebar());
            _sidebarOpen?.RegisterCallback<ClickEvent>(_ => ToggleSidebar());

            var attachBtn = rootVisualElement.Q<Button>("AttachButton");
            attachBtn?.RegisterCallback<ClickEvent>(_ => OpenAttachFilePicker());

            WireSuggestionChips();
            InitializeModelDropdown();
            InitializePermissionModeDropdown();
            InitializeCommandPopup();

            _agentDropdown?.RegisterValueChangedCallback(evt => _selectedAgent = evt.newValue);
            _promptInput?.RegisterValueChangedCallback(evt => OnPromptInputChanged(evt.newValue));

            _promptInput?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (TryHandleCommandPopupKey(evt))
                {
                    evt.StopImmediatePropagation();
                    return;
                }

                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    evt.StopImmediatePropagation();

                    if (evt.shiftKey)
                    {
                        var idx = _promptInput.cursorIndex;
                        _promptInput.value = _promptInput.value.Insert(idx, "\n");

                        var next = idx + 1;
                        _promptInput.schedule.Execute(() =>
                        {
                            _promptInput.Focus();
                            _promptInput.SelectRange(next, next);
                        });
                    }
                    else
                    {
                        EditorApplication.delayCall += HandleSendPrompt;
                    }
                }

            }, TrickleDown.TrickleDown);

            RestoreStateAfterReload();
            InitializeSetupScreen();
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Restores visual state from serialized fields after a domain reload.
        /// Called at the end of <see cref="CreateGUI"/> so the UI matches the
        /// pre-reload state (hidden welcome screen, sidebar, cost label, etc.).
        /// </summary>
        private void RestoreStateAfterReload()
        {
            if (_hasMessages && _welcomeScreen != null)
            {
                _welcomeScreen.style.display = DisplayStyle.None;
            }


            if (!_sidebarVisible)
            {
                _sidebar?.AddToClassList("sidebar--collapsed");
                _sidebarOpen?.AddToClassList("sidebar-open-btn--visible");
            }

            if (_sessionCost > 0 && _costLabel != null)
            {
                _costLabel.text = string.Format(CultureInfo.InvariantCulture, "Session Cost: ${0:F3}", _sessionCost);
            }
        }

        /// <summary>
        /// Toggles the generating state: swaps send/stop button visibility and manages
        /// assembly reload locking via <see cref="EditorApplication.LockReloadAssemblies"/>
        /// to prevent Unity from recompiling mid-generation.
        /// </summary>
        /// <param name="generating">True to enter generating state, false to exit.</param>
        private void SetGenerating(bool generating)
        {
            _isGenerating = generating;

            if (_sendButton != null)
            {
                _sendButton.style.display = generating ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_stopButton != null)
            {
                _stopButton.style.display = generating ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (generating && !_reloadLocked)
            {
                EditorApplication.LockReloadAssemblies();
                _reloadLocked = true;
            }
            else if (!generating && _reloadLocked)
            {
                _reloadLocked = false;
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        /// <summary>
        /// Hides the welcome screen after the first message is sent.
        /// </summary>
        private void HideWelcomeScreen()
        {
            if (!_hasMessages && _welcomeScreen != null)
            {
                _welcomeScreen.style.display = DisplayStyle.None;
                _hasMessages = true;
            }
        }

        /// <summary>
        /// Initializes the model dropdown with available Claude models and restores
        /// the user's last selection from EditorPrefs.
        /// </summary>
        private void InitializeModelDropdown()
        {
            if (_modelDropdown == null)
            {
                return;
            }

            var choices = new System.Collections.Generic.List<string>
            {
                ChatConstants.MODEL_SONNET_LABEL,
                ChatConstants.MODEL_OPUS_LABEL,
                ChatConstants.MODEL_HAIKU_LABEL
            };

            _modelDropdown.choices = choices;
            var saved = EditorPrefs.GetString(ChatConstants.MODEL_PREF_KEY, ChatConstants.MODEL_DEFAULT_LABEL);

            if (!choices.Contains(saved))
            {
                saved = ChatConstants.MODEL_DEFAULT_LABEL;
            }

            _modelDropdown.SetValueWithoutNotify(saved);

            _modelDropdown.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetString(ChatConstants.MODEL_PREF_KEY, evt.newValue);
            });
        }

        /// <summary>
        /// Initializes the permission mode dropdown with available modes and restores
        /// the user's last selection from EditorPrefs.
        /// </summary>
        private void InitializePermissionModeDropdown()
        {
            if (_permissionModeDropdown == null)
            {
                return;
            }

            var choices = new System.Collections.Generic.List<string>
            {
                ChatConstants.PERM_ASK_LABEL,
                ChatConstants.PERM_AUTO_LABEL,
                ChatConstants.PERM_PLAN_LABEL
            };

            _permissionModeDropdown.choices = choices;
            var saved = EditorPrefs.GetString(ChatConstants.PERM_PREF_KEY, ChatConstants.PERM_DEFAULT_LABEL);

            if (!choices.Contains(saved))
            {
                saved = ChatConstants.PERM_DEFAULT_LABEL;
            }

            _permissionModeDropdown.SetValueWithoutNotify(saved);

            _permissionModeDropdown.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetString(ChatConstants.PERM_PREF_KEY, evt.newValue);
            });
        }

        /// <summary>
        /// Returns the Agent SDK permission mode ID corresponding to the selected dropdown value.
        /// </summary>
        /// <returns>Permission mode ID string ("default", "acceptEdits", or "plan").</returns>
        private string GetSelectedPermissionMode()
        {
            var selected = _permissionModeDropdown?.value ?? ChatConstants.PERM_DEFAULT_LABEL;

            return selected switch
            {
                ChatConstants.PERM_AUTO_LABEL => ChatConstants.PERM_AUTO_ID,
                ChatConstants.PERM_PLAN_LABEL => ChatConstants.PERM_PLAN_ID,
                _ => ChatConstants.PERM_ASK_ID,
            };
        }

        /// <summary>
        /// Returns the Claude model ID corresponding to the selected dropdown value.
        /// </summary>
        /// <returns>Claude model ID string, or null if default.</returns>
        private string? GetSelectedModelId()
        {
            var selected = _modelDropdown?.value ?? ChatConstants.MODEL_DEFAULT_LABEL;

            return selected switch
            {
                ChatConstants.MODEL_OPUS_LABEL => ChatConstants.MODEL_OPUS_ID,
                ChatConstants.MODEL_HAIKU_LABEL => ChatConstants.MODEL_HAIKU_ID,
                ChatConstants.MODEL_SONNET_LABEL => ChatConstants.MODEL_SONNET_ID,
                _ => ChatConstants.MODEL_SONNET_ID,
            };
        }

        /// <summary>
        /// Wires click events on suggestion chips to auto-fill and send the prompt.
        /// </summary>
        private void WireSuggestionChips()
        {
            var chips = rootVisualElement.Q<VisualElement>("SuggestionChips");

            if (chips == null)
            {
                return;
            }

            var buttons = chips.Query<Button>().ToList();

            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i];

                btn.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_promptInput != null)
                    {
                        _promptInput.value = btn.text;
                        HandleSendPrompt();
                    }
                });
            }
        }

        /// <summary>
        /// Toggles the sidebar visibility and updates the open button state.
        /// </summary>
        private void ToggleSidebar()
        {
            _sidebarVisible = !_sidebarVisible;

            if (_sidebar != null)
            {
                if (_sidebarVisible)
                {
                    _sidebar.RemoveFromClassList("sidebar--collapsed");
                }
                else
                {
                    _sidebar.AddToClassList("sidebar--collapsed");
                }
            }
            if (_sidebarOpen != null)
            {
                if (_sidebarVisible)
                {
                    _sidebarOpen.RemoveFromClassList("sidebar-open-btn--visible");
                }
                else
                {
                    _sidebarOpen.AddToClassList("sidebar-open-btn--visible");
                }
            }
        }

        #endregion

        #region EVENT HANDLERS

        /// <summary>
        /// Sends the current prompt input to the Agent SDK Server over WebSocket.
        /// Clears the input field, adds the user message to the chat, and enters
        /// the generating state. No-ops if the input is empty or already generating.
        /// </summary>
        private void HandleSendPrompt() => SafeAsync(HandleSendPromptAsync(), "SendPrompt");

        /// <summary>
        /// Sends the current prompt input to the Agent SDK Server over WebSocket.
        /// </summary>
        /// <returns>A Task that completes when the prompt has been sent.</returns>
        private async Task HandleSendPromptAsync()
        {
            if (_promptInput == null || _wsClient == null)
            {
                return;
            }

            var prompt = _promptInput.value?.Trim() ?? "";

            if (string.IsNullOrEmpty(prompt))
            {
                return;
            }

            if (_isGenerating)
            {
                return;
            }

            var firstWord = prompt.Split(' ', 2)[0];
            var isCommand = firstWord.StartsWith("/") && firstWord.Length > 1;
            var action = isCommand ? "command" : "prompt";

            HideWelcomeScreen();
            _renderer?.FinalizeToolExecution();

            if (_pendingAttachments.Count > 0)
            {
                var attachNames = new System.Text.StringBuilder();

                for (int i = 0; i < _pendingAttachments.Count; i++)
                {
                    attachNames.Append($"[{_pendingAttachments[i].FileName}] ");
                }

                attachNames.AppendLine();
                attachNames.Append(prompt);
                _renderer?.AddUserMessage(attachNames.ToString());
            }
            else
            {
                _renderer?.AddUserMessage(prompt);
            }
            _renderer?.ShowTypingIndicator();
            _promptInput.value = "";

            var agent = _agentDropdown?.value;

            if (agent == ChatConstants.AGENT_DEFAULT_LABEL || string.IsNullOrWhiteSpace(agent))
            {
                agent = null;
            }


            var model = GetSelectedModelId();
            var permMode = GetSelectedPermissionMode();
            var attachmentsJson = ConsumeAttachmentsJson();
            var sessionPart = _currentSessionId != null ? $",\"sessionId\":{EscapeJson(_currentSessionId)}" : "";
            var agentPart = agent != null ? $",\"agent\":{EscapeJson(agent)}" : "";
            var modelPart = model != null ? $",\"model\":{EscapeJson(model)}" : "";
            var permPart = $",\"permissionMode\":{EscapeJson(permMode)}";
            var attachPart = !string.IsNullOrEmpty(attachmentsJson) ? $",\"attachments\":{attachmentsJson}" : "";
            var json = isCommand
                ? $"{{\"action\":\"command\",\"command\":{EscapeJson(firstWord)},\"prompt\":{EscapeJson(prompt)}{sessionPart}{agentPart}{modelPart}{permPart}{attachPart}}}"
                : $"{{\"action\":\"prompt\",\"prompt\":{EscapeJson(prompt)}{sessionPart}{agentPart}{modelPart}{permPart}{attachPart}}}";

            SetGenerating(true);
            await _wsClient.SendAsync(json);
        }

        /// <summary>
        /// Sends a cancel request to the Agent SDK Server and exits the generating state.
        /// No-ops if the WebSocket client is not connected.
        /// </summary>
        private void CancelGeneration() => SafeAsync(CancelGenerationAsync(), "CancelGeneration");

        /// <summary>
        /// Sends a cancel request to the Agent SDK Server and exits the generating state.
        /// </summary>
        /// <returns>A Task that completes when the cancel request has been sent.</returns>
        private async Task CancelGenerationAsync()
        {
            if (_wsClient == null)
            {
                return;
            }

            await _wsClient.SendAsync("{\"action\":\"cancel\"}");
            SetGenerating(false);
        }

        #endregion
    }
}