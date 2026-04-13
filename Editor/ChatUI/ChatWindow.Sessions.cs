#nullable enable
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    public partial class ChatWindow
    {
        #region SESSION MANAGEMENT

        /// <summary>
        /// Starts a new chat session by clearing messages and resetting state.
        /// </summary>
        private void StartNewChat()
        {
            _renderer?.Clear();
            _currentSessionId = null;
            _sessionCost = 0;
            _hasMessages = false;

            if (_costLabel != null)
            {
                _costLabel.text = "Session Cost: $0.000";
            }

            if (_welcomeScreen != null)
            {
                _welcomeScreen.style.display = DisplayStyle.Flex;
            }

            RefreshSessionList();
        }

        /// <summary>
        /// Resumes a previous conversation session by setting the session ID,
        /// clearing the chat, and requesting message history from the server.
        /// </summary>
        /// <param name="sessionId">The session ID to resume.</param>
        private void ResumeSession(string sessionId) => SafeAsync(ResumeSessionAsync(sessionId), "ResumeSession");

        /// <summary>
        /// Resumes a previous conversation session by setting the session ID,
        /// clearing the chat, and requesting message history from the server.
        /// </summary>
        /// <param name="sessionId">The session ID to resume.</param>
        /// <returns>A Task that completes when the session history has been requested.</returns>
        private async Task ResumeSessionAsync(string sessionId)
        {
            _currentSessionId = sessionId;
            _renderer?.Clear();
            _sessionCost = 0;
            _hasMessages = true;

            if (_costLabel != null)
            {
                _costLabel.text = "Session Cost: $0.000";
            }

            if (_welcomeScreen != null)
            {
                _welcomeScreen.style.display = DisplayStyle.None;
            }

            if (_wsClient != null)
            {
                await _wsClient.SendAsync($"{{\"action\":\"get-session\",\"sessionId\":{EscapeJson(sessionId)}}}");
            }

            RefreshSessionList();
        }

        /// <summary>
        /// Sends a delete-session request to the server after user confirmation.
        /// </summary>
        /// <param name="sessionId">The session ID to delete.</param>
        private void DeleteSessionWithConfirm(string sessionId) => SafeAsync(DeleteSessionWithConfirmAsync(sessionId), "DeleteSession");

        /// <summary>
        /// Sends a delete-session request to the server after user confirmation.
        /// </summary>
        /// <param name="sessionId">The session ID to delete.</param>
        /// <returns>A Task that completes when the deletion request has been sent.</returns>
        private async Task DeleteSessionWithConfirmAsync(string sessionId)
        {
            if (!EditorUtility.DisplayDialog("Delete Conversation", "Are you sure you want to delete this conversation?", "Delete", "Cancel"))
            {
                return;
            }

            if (_wsClient == null)
            {
                return;
            }

            await _wsClient.SendAsync($"{{\"action\":\"delete-session\",\"sessionId\":{EscapeJson(sessionId)}}}");

            if (_currentSessionId == sessionId)
            {
                StartNewChat();
            }
        }

        /// <summary>
        /// Requests a fresh session list from the server.
        /// </summary>
        private void RefreshSessionList() => SafeAsync(RefreshSessionListAsync(), "RefreshSessionList");

        /// <summary>
        /// Requests a fresh session list from the server.
        /// </summary>
        /// <returns>A Task that completes when the list request has been sent.</returns>
        private async Task RefreshSessionListAsync()
        {
            if (_wsClient == null)
            {
                return;
            }

            await _wsClient.SendAsync("{\"action\":\"list-sessions\"}");
        }

        #endregion
    }
}