using System.Collections.Generic;
using HearApp.Core.HearingEngine;

namespace HearApp.Dev
{
    /// <summary>
    /// One fixed, deterministic sequence of classified outcomes covering all four
    /// <see cref="TrialOutcome"/> values and all three <see cref="EarChannel"/> values. Used both
    /// as a development convenience (drive a world without a real audio session) and as the
    /// integration proof's input (docs/12, acceptance criterion #2): the same sequence
    /// run through every world must produce the same stored <see cref="SessionResult"/>.
    /// </summary>
    public static class MockSequenceDriver
    {
        public static List<(TrialOutcome outcome, EarChannel channel)> GetDefaultSequence()
        {
            return new List<(TrialOutcome, EarChannel)>
            {
                (TrialOutcome.CorrectDetection, EarChannel.Left),
                (TrialOutcome.CorrectDetection, EarChannel.Right),
                (TrialOutcome.CorrectDetection, EarChannel.Combined),
                (TrialOutcome.Miss, EarChannel.Left),
                (TrialOutcome.Miss, EarChannel.Right),
                (TrialOutcome.FalsePositive, EarChannel.Combined),
                (TrialOutcome.CorrectRejection, EarChannel.Left),
                (TrialOutcome.CorrectRejection, EarChannel.Right),
                (TrialOutcome.CorrectDetection, EarChannel.Left),
                (TrialOutcome.CorrectDetection, EarChannel.Right),
            };
        }
    }
}
