using System;

namespace HearApp.Core.HearingEngine
{
    /// <summary>
    /// Aggregate measurement outcome for a completed (or in-progress) session. This is the
    /// engine's stored truth. Worlds never write to this - they only ever receive an immutable
    /// snapshot via <see cref="OutcomePresentationContext"/> / <see cref="Complete"/>.
    ///
    /// Session completion (reaching 100% SessionProgress) is intentionally independent of the
    /// counts below: a player who misses every tone still finishes the session.
    /// </summary>
    [Serializable]
    public sealed class SessionResult
    {
        public int CorrectDetections;
        public int Misses;
        public int FalsePositives;
        public int CorrectRejections;

        public int CorrectDetectionsLeft;
        public int CorrectDetectionsRight;
        public int CorrectDetectionsCombined;

        public int TotalTrials => CorrectDetections + Misses + FalsePositives + CorrectRejections;

        public void Record(TrialOutcome outcome, EarChannel channel)
        {
            switch (outcome)
            {
                case TrialOutcome.CorrectDetection:
                    CorrectDetections++;
                    switch (channel)
                    {
                        case EarChannel.Left: CorrectDetectionsLeft++; break;
                        case EarChannel.Right: CorrectDetectionsRight++; break;
                        default: CorrectDetectionsCombined++; break;
                    }
                    break;
                case TrialOutcome.Miss:
                    Misses++;
                    break;
                case TrialOutcome.FalsePositive:
                    FalsePositives++;
                    break;
                case TrialOutcome.CorrectRejection:
                    CorrectRejections++;
                    break;
            }
        }

        public override string ToString()
        {
            return $"CorrectDetection={CorrectDetections} (L={CorrectDetectionsLeft},R={CorrectDetectionsRight},C={CorrectDetectionsCombined}) " +
                   $"Miss={Misses} FalsePositive={FalsePositives} CorrectRejection={CorrectRejections} Total={TotalTrials}";
        }

        /// <summary>Structural equality helper used by the integration proof to compare results across worlds.</summary>
        public bool HasSameCountsAs(SessionResult other)
        {
            if (other == null) return false;
            return CorrectDetections == other.CorrectDetections
                && Misses == other.Misses
                && FalsePositives == other.FalsePositives
                && CorrectRejections == other.CorrectRejections
                && CorrectDetectionsLeft == other.CorrectDetectionsLeft
                && CorrectDetectionsRight == other.CorrectDetectionsRight
                && CorrectDetectionsCombined == other.CorrectDetectionsCombined;
        }
    }
}
