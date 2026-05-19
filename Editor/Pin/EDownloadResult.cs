namespace GameDeck.Editor.Pin
{
    #region ENUM

    /// <summary>
    /// Outcome of <see cref="PinBinaryManager.DownloadAsync"/>.
    /// </summary>
    /// <remarks>
    /// Cancellation is surfaced via <see cref="System.OperationCanceledException"/>
    /// rather than a dedicated enum value — callers handle cancellation through the
    /// standard cooperative-cancellation pattern.
    /// </remarks>
    public enum EDownloadResult
    {
        SUCCESS,
        NETWORK_ERROR,
        HASH_MISMATCH,
    }

    #endregion
}
