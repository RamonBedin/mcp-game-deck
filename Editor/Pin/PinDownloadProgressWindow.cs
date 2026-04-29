#nullable enable

using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Small utility <see cref="EditorWindow"/> rendering a UIToolkit
    /// <see cref="ProgressBar"/> for the binary download flow. Lifecycle is
    /// caller-managed: open via <see cref="PinDialogs.ShowProgress"/> and close
    /// via <see cref="EditorWindow.Close"/> when the download finishes (success
    /// or failure).
    /// </summary>
    /// <remarks>
    /// Two ways to drive the bar:
    /// <list type="bullet">
    /// <item><description>Set <see cref="Progress"/> directly (synchronous,
    /// main-thread).</description></item>
    /// <item><description>Pass <see cref="AsProgress"/> to
    /// <see cref="PinBinaryManager.DownloadAsync"/> — internally a
    /// <see cref="System.Progress{T}"/> captures the editor main-thread
    /// <see cref="SynchronizationContext"/> so background-thread reports are
    /// marshaled before the UI is touched.</description></item>
    /// </list>
    /// </remarks>
    public sealed class PinDownloadProgressWindow : EditorWindow
    {
        #region CONSTANTS

        private const string WINDOW_TITLE = "Downloading MCP Game Deck app";
        private const float WINDOW_WIDTH = 320f;
        private const float WINDOW_HEIGHT = 100f;
        private const float PROGRESS_BAR_HEIGHT = 24f;
        private const float LABEL_TOP_MARGIN = 6f;
        private const int ROOT_PADDING = 12;
        private const string PROGRESS_LABEL_FORMAT = "{0:P0}";
        private const string INITIAL_PROGRESS_LABEL = "0%";

        #endregion

        #region FIELDS

        private ProgressBar? _progressBar;
        private Label? _progressLabel;
        private float _progress;
        private IProgress<float>? _progressSink;

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Current progress value in <c>[0, 1]</c>. Setting from the editor main
        /// thread updates the bar + label and triggers a <see cref="Repaint"/>.
        /// Safe to set before <see cref="CreateGUI"/> has built the UI — the value
        /// is cached and applied when the bar comes online.
        /// </summary>
        public float Progress
        {
            get => _progress;
            set
            {
                _progress = value;

                if (_progressBar != null)
                {
                    _progressBar.value = value;
                }

                if (_progressLabel != null)
                {
                    _progressLabel.text = string.Format(PROGRESS_LABEL_FORMAT, value);
                }

                Repaint();
            }
        }

        #endregion

        #region UNITY CALLBACKS

        private void CreateGUI()
        {
            rootVisualElement.style.paddingTop = ROOT_PADDING;
            rootVisualElement.style.paddingBottom = ROOT_PADDING;
            rootVisualElement.style.paddingLeft = ROOT_PADDING;
            rootVisualElement.style.paddingRight = ROOT_PADDING;

            _progressBar = new ProgressBar
            {
                lowValue = 0f,
                highValue = 1f,
                value = _progress,
            };

            _progressBar.style.height = PROGRESS_BAR_HEIGHT;
            rootVisualElement.Add(_progressBar);

            _progressLabel = new Label(string.Format(PROGRESS_LABEL_FORMAT, _progress));
            _progressLabel.style.marginTop = LABEL_TOP_MARGIN;
            rootVisualElement.Add(_progressLabel);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns an <see cref="IProgress{T}"/> view over this window suitable
        /// for passing to <see cref="PinBinaryManager.DownloadAsync"/>. The
        /// underlying <see cref="System.Progress{T}"/> captures the editor main
        /// thread's <see cref="SynchronizationContext"/> at first call so
        /// background-thread reports marshal back before touching the UI.
        /// </summary>
        /// <returns>An <see cref="IProgress{T}"/> wired to <see cref="Progress"/>.</returns>
        public IProgress<float> AsProgress()
        {
            _progressSink ??= new Progress<float>(v => Progress = v);

            return _progressSink;
        }

        #endregion

        #region INTERNAL METHODS

        /// <summary>
        /// Factory used by <see cref="PinDialogs.ShowProgress"/>: creates and
        /// shows a fresh utility window pinned to a fixed
        /// <see cref="WINDOW_WIDTH"/> × <see cref="WINDOW_HEIGHT"/>.
        /// </summary>
        /// <returns>The newly-shown progress window.</returns>
        /// <remarks>
        /// Named <c>Open</c> to avoid shadowing the inherited
        /// <see cref="EditorWindow.Show()"/> instance method.
        /// </remarks>
        internal static PinDownloadProgressWindow Open()
        {
            var window = CreateInstance<PinDownloadProgressWindow>();
            window.titleContent = new GUIContent(WINDOW_TITLE);
            var size = new Vector2(WINDOW_WIDTH, WINDOW_HEIGHT);
            window.minSize = size;
            window.maxSize = size;
            window.ShowUtility();
            return window;
        }

        #endregion
    }
}