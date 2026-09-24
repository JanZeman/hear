namespace HearApp.Core.HearingEngine
{
    /// <summary>
    /// Classified result of a single trial. This is the ONLY vocabulary a World is allowed
    /// to react to. A World must never be told when a stimulus starts - only what happened,
    /// after the response window has already closed.
    /// </summary>
    public enum TrialOutcome
    {
        /// <summary>A tone played and the player responded inside the active window.</summary>
        CorrectDetection,

        /// <summary>A tone played and the player did not respond in time.</summary>
        Miss,

        /// <summary>A silent catch trial played (no tone) and the player responded anyway.</summary>
        FalsePositive,

        /// <summary>A silent catch trial played (no tone) and the player correctly did not respond.</summary>
        CorrectRejection
    }

    /// <summary>
    /// Which audio channel(s) a trial was presented on. Combined is used for speaker/free-field
    /// playback where left/right cannot be measured separately. Left/Right are only meaningful
    /// in headphone mode.
    /// </summary>
    public enum EarChannel
    {
        Combined,
        Left,
        Right
    }

    /// <summary>
    /// User/shell-selected output mode. Deliberately NOT auto-detected: reliably detecting
    /// headphone presence across mobile/desktop platforms is an open question (see
    /// docs/13-open-questions.md), so the shell asks the player explicitly instead.
    /// </summary>
    public enum AudioOutputMode
    {
        Speaker,
        Headphones
    }
}
