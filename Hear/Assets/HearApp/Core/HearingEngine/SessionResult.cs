using System;
using System.Collections.Generic;

namespace HearApp.Core.HearingEngine
{
    /// <summary>One tone trial's frequency/channel/outcome, kept alongside the aggregate counts
    /// below so a per-frequency hearing profile (Results screen audiogram) can be built from what
    /// was actually measured, instead of being invented at the UI layer. Catch trials (no tone)
    /// are not recorded here - see <see cref="SessionResult.Record"/>.</summary>
    [Serializable]
    public struct FrequencyTrialRecord
    {
        public float FrequencyHz;
        public EarChannel Channel;
        public bool Detected;

        public FrequencyTrialRecord(float frequencyHz, EarChannel channel, bool detected)
        {
            FrequencyHz = frequencyHz;
            Channel = channel;
            Detected = detected;
        }
    }

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

        /// <summary>One entry per tone trial (CorrectDetection or Miss only - catch trials have
        /// no frequency). Source data for the Results screen's hearing profile chart.</summary>
        public List<FrequencyTrialRecord> FrequencyTrials = new();

        public int TotalTrials => CorrectDetections + Misses + FalsePositives + CorrectRejections;

        public void Record(TrialOutcome outcome, EarChannel channel, float frequencyHz = 0f)
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
                    FrequencyTrials.Add(new FrequencyTrialRecord(frequencyHz, channel, detected: true));
                    break;
                case TrialOutcome.Miss:
                    Misses++;
                    FrequencyTrials.Add(new FrequencyTrialRecord(frequencyHz, channel, detected: false));
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
