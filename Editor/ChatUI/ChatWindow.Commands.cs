#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    public partial class ChatWindow
    {
        #region COMMAND TYPES

        /// <summary>
        /// In-memory record of a slash command exposed by the Agent SDK Server.
        /// The <see cref="Name"/> always includes the leading "/" prefix
        /// (e.g. "/code-review") to match the protocol expected by the
        /// <c>command</c> action handler.
        /// </summary>
        private struct CommandInfo
        {
            public string Name;
            public string Description;
        }

        #endregion

        #region FIELDS

        private List<CommandInfo>? _commands;
        private readonly List<CommandInfo> _filteredCommands = new();
        private readonly List<VisualElement> _commandItemElements = new();
        private VisualElement? _commandPopup;
        private ScrollView? _commandList;
        private int _selectedCommandIndex = -1;
        private bool _commandPopupVisible;
        private EventCallback<MouseDownEvent>? _commandPopupOutsideClickHandler;

        #endregion

        #region INITIALIZATION METHODS

        private void InitializeCommandPopup()
        {
            var inputArea = rootVisualElement.Q<VisualElement>("InputArea");

            if (inputArea == null)
            {
                return;
            }

            _commandPopup = new VisualElement();
            _commandPopup.AddToClassList("command-popup");
            _commandPopup.style.display = DisplayStyle.None;
            _commandPopup.pickingMode = PickingMode.Position;

            _commandList = new ScrollView(ScrollViewMode.Vertical);
            _commandList.AddToClassList("command-popup__list");
            _commandPopup.Add(_commandList);

            inputArea.Add(_commandPopup);

            _commandPopupOutsideClickHandler = evt =>
            {
                if (!_commandPopupVisible || _commandPopup == null)
                {
                    return;
                }

                if (_commandPopup.worldBound.Contains(evt.mousePosition))
                {
                    return;
                }

                if (_promptInput != null && _promptInput.worldBound.Contains(evt.mousePosition))
                {
                    return;
                }

                HideCommandPopup();
            };

            rootVisualElement.RegisterCallback(_commandPopupOutsideClickHandler, TrickleDown.TrickleDown);
        }

        #endregion

        #region COMMAND DATA

        /// <summary>
        /// Parses a <c>{"type":"commands","commands":[...]}</c> response from the server
        /// and caches the entries. Mirrors <see cref="PopulateAgentDropdown"/> in shape.
        /// </summary>
        /// <param name="json">Raw JSON message body received over WebSocket.</param>
        private void OnCommandsReceived(string json)
        {
            var list = new List<CommandInfo>();
            int idx = 0;

            while (true)
            {
                var name = ExtractJsonStringFromArray(json, "commands", idx, "name");

                if (name == null)
                {
                    break;
                }

                var description = ExtractJsonStringFromArray(json, "commands", idx, "description") ?? "";
                list.Add(new CommandInfo { Name = name, Description = description });
                idx++;
            }

            _commands = list;
        }

        #endregion

        #region COMMAND INPUT HANDLING

        /// <summary>
        /// Reacts to live changes in the prompt text field. Shows the popup when
        /// the user types a "/"-prefixed token (no whitespace yet) and hides it
        /// otherwise. No-op when the command list has not yet been received.
        /// </summary>
        /// <param name="value">Current text-field value.</param>
        private void OnPromptInputChanged(string value)
        {
            if (_commands == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(value) || value[0] != '/')
            {
                HideCommandPopup();
                return;
            }

            if (value.Contains(' ') || value.Contains('\n'))
            {
                HideCommandPopup();
                return;
            }

            FilterAndShowCommands(value);
        }

        /// <summary>
        /// Lets the popup intercept arrow keys, Tab, Enter, and Escape while it
        /// is visible. Returns <c>true</c> if the event was consumed, in which
        /// case the caller MUST call <c>StopImmediatePropagation()</c> to keep
        /// the underlying TextField from also processing the key.
        /// </summary>
        /// <param name="evt">The raw KeyDownEvent from the TextField.</param>
        /// <returns><c>true</c> if the popup handled the key; <c>false</c> otherwise.</returns>
        private bool TryHandleCommandPopupKey(KeyDownEvent evt)
        {
            if (!_commandPopupVisible)
            {
                return false;
            }

            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    SelectCommand(Math.Max(0, _selectedCommandIndex - 1));
                    return true;

                case KeyCode.DownArrow:
                    SelectCommand(Math.Min(_filteredCommands.Count - 1, _selectedCommandIndex + 1));
                    return true;

                case KeyCode.Tab:
                    AutocompleteSelectedCommand();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExecuteSelectedCommand();
                    return true;

                case KeyCode.Escape:
                    HideCommandPopup();
                    return true;
            }

            return false;
        }

        #endregion

        #region COMMAND POPUP RENDERING

        /// <summary>
        /// Filters <see cref="_commands"/> by the typed prefix, rebuilds the visible
        /// rows in <see cref="_commandList"/>, and shows the popup. Hides the popup
        /// when there are no matches.
        /// </summary>
        /// <param name="prefix">The current input value, including the leading "/".</param>
        private void FilterAndShowCommands(string prefix)
        {
            if (_commands == null || _commandList == null || _commandPopup == null)
            {
                return;
            }

            _filteredCommands.Clear();
            _commandItemElements.Clear();
            _commandList.Clear();

            for (int i = 0; i < _commands.Count; i++)
            {
                var cmd = _commands[i];

                if (cmd.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    _filteredCommands.Add(cmd);
                }
            }

            if (_filteredCommands.Count == 0)
            {
                HideCommandPopup();
                return;
            }

            for (int i = 0; i < _filteredCommands.Count; i++)
            {
                int capturedIndex = i;
                var cmd = _filteredCommands[i];

                var item = new VisualElement();
                item.AddToClassList("command-item");

                var nameLabel = new Label(cmd.Name);
                nameLabel.AddToClassList("command-item__name");
                item.Add(nameLabel);

                if (!string.IsNullOrEmpty(cmd.Description))
                {
                    var descLabel = new Label(cmd.Description);
                    descLabel.AddToClassList("command-item__desc");
                    item.Add(descLabel);
                }

                item.RegisterCallback<MouseDownEvent>(evt =>
                {
                    SelectCommand(capturedIndex);
                    EditorApplication.delayCall += ExecuteSelectedCommand;
                    evt.StopPropagation();
                });

                _commandList.Add(item);
                _commandItemElements.Add(item);
            }

            _commandPopup.style.display = DisplayStyle.Flex;
            _commandPopupVisible = true;
            SelectCommand(0);
        }

        /// <summary>
        /// Highlights the row at <paramref name="index"/> in the popup, removing
        /// the highlight from the previously selected row. Scrolls the row into
        /// view if necessary.
        /// </summary>
        /// <param name="index">Zero-based index into <see cref="_filteredCommands"/>.</param>
        private void SelectCommand(int index)
        {
            if (index < 0 || index >= _commandItemElements.Count)
            {
                return;
            }

            if (_selectedCommandIndex >= 0 && _selectedCommandIndex < _commandItemElements.Count)
            {
                _commandItemElements[_selectedCommandIndex].RemoveFromClassList("command-item--selected");
            }

            _selectedCommandIndex = index;
            _commandItemElements[index].AddToClassList("command-item--selected");
            _commandList?.ScrollTo(_commandItemElements[index]);
        }

        /// <summary>
        /// Stages the selected command name into the input field and delegates
        /// to <see cref="HandleSendPrompt"/>, which detects the leading "/" and
        /// dispatches via the <c>command</c> action. Routing through the shared
        /// send path keeps SetGenerating ordering and message echo consistent
        /// with normal prompts.
        /// </summary>
        private void ExecuteSelectedCommand()
        {
            if (_promptInput == null || _selectedCommandIndex < 0
                || _selectedCommandIndex >= _filteredCommands.Count)
            {
                return;
            }

            var commandName = _filteredCommands[_selectedCommandIndex].Name;
            HideCommandPopup();
            _promptInput.value = commandName;
            HandleSendPrompt();
        }

        /// <summary>
        /// Replaces the input value with the selected command name followed by a
        /// space (so the user can append arguments) and hides the popup. Does
        /// NOT send the command — Tab is intentionally non-destructive.
        /// </summary>
        private void AutocompleteSelectedCommand()
        {
            if (_promptInput == null)
            {
                return;
            }

            if (_selectedCommandIndex < 0 || _selectedCommandIndex >= _filteredCommands.Count)
            {
                return;
            }

            var commandName = _filteredCommands[_selectedCommandIndex].Name;
            var newValue = commandName + " ";

            _promptInput.value = newValue;

            int caret = newValue.Length;
            _promptInput.schedule.Execute(() =>
            {
                _promptInput.Focus();
                _promptInput.SelectRange(caret, caret);
            });

            HideCommandPopup();
        }

        /// <summary>
        /// Hides the popup, resets selection state, and detaches all rendered rows.
        /// Safe to call when the popup is already hidden.
        /// </summary>
        private void HideCommandPopup()
        {
            if (_commandPopup != null)
            {
                _commandPopup.style.display = DisplayStyle.None;
            }

            _commandList?.Clear();
            _commandItemElements.Clear();
            _filteredCommands.Clear();
            _selectedCommandIndex = -1;
            _commandPopupVisible = false;
        }

        #endregion
    }
}