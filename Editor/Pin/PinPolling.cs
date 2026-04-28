using UnityEditor;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Editor-only polling loop that periodically recomputes the toolbar pin's
    /// status. Registers a throttled callback on <see cref="EditorApplication.update"/>
    /// via <see cref="InitializeOnLoadMethodAttribute"/> and exposes the latest
    /// result through <see cref="CurrentStatus"/> for consumption by
    /// <see cref="PinToolbarElement"/>.
    /// </summary>
    public static class PinPolling
    {
        #region CONSTANTS

        private const double TICK_INTERVAL_SECONDS = 0.5;

        #endregion

        #region FIELDS

        private static double _nextTickAt;

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Most-recently computed pin status. Read by <see cref="PinToolbarElement"/> when
        /// constructing the toolbar icon.
        /// </summary>
        public static EPinStatus CurrentStatus { get; private set; } = EPinStatus.NOT_INSTALLED;

        /// <summary>
        /// Number of throttled ticks that have fired since the last assembly reload.
        /// Useful for confirming the polling loop is alive during validation.
        /// </summary>
        public static long TickCount { get; private set; }

        #endregion

        #region INITIALIZATION METHODS

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= HandleTick;
            EditorApplication.update += HandleTick;
            _nextTickAt = EditorApplication.timeSinceStartup + TICK_INTERVAL_SECONDS;
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Throttled editor-update callback that recomputes <see cref="CurrentStatus"/>
        /// and increments <see cref="TickCount"/> at most once per
        /// <see cref="TICK_INTERVAL_SECONDS"/>. Early-exits when the next tick deadline
        /// has not yet elapsed to keep the editor update loop cheap.
        /// </summary>
        private static void HandleTick()
        {
            if (EditorApplication.timeSinceStartup < _nextTickAt)
            {
                return;
            }
            _nextTickAt = EditorApplication.timeSinceStartup + TICK_INTERVAL_SECONDS;

            CurrentStatus = EPinStatus.NOT_INSTALLED;
            TickCount++;
        }

        #endregion
    }
}