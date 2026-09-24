namespace HearApp.Core.HearingEngine
{
    /// <summary>
    /// One-time initialization data handed to a World when it is entered. Contains only what a
    /// World legitimately needs to present itself - never trial scheduling/timing information.
    /// </summary>
    public readonly struct WorldContext
    {
        public readonly AudioOutputMode OutputMode;
        public readonly int RandomSeed;

        public WorldContext(AudioOutputMode outputMode, int randomSeed)
        {
            OutputMode = outputMode;
            RandomSeed = randomSeed;
        }
    }

    /// <summary>
    /// Immutable snapshot sent to the active World only AFTER a trial has been classified.
    /// This is intentionally the sole channel through which a World learns anything about a
    /// trial - there is no companion "stimulus started" event anywhere in the engine.
    /// </summary>
    public readonly struct OutcomePresentationContext
    {
        /// <summary>The classified result. Worlds decide their own presentation per outcome.</summary>
        public readonly TrialOutcome Outcome;

        /// <summary>
        /// Which ear this trial targeted. Only meaningful in Headphones mode. A World MAY use
        /// this to lateralize its Success Event (e.g. paddle on the corresponding side), but
        /// only now, after the response has already been classified - never before, and never
        /// to visually hint which ear is about to be tested.
        /// </summary>
        public readonly EarChannel Channel;

        /// <summary>Normalized session progress (0..1) AFTER this trial. Always reaches 1.0 eventually,
        /// regardless of how many trials were missed - this drives world/story progression, which
        /// must stay independent of hearing performance.</summary>
        public readonly float SessionProgress;

        public OutcomePresentationContext(TrialOutcome outcome, EarChannel channel, float sessionProgress)
        {
            Outcome = outcome;
            Channel = channel;
            SessionProgress = sessionProgress;
        }
    }
}
