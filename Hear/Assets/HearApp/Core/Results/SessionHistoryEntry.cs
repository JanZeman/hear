using System;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;

namespace HearApp.Core.Results
{
    /// <summary>One completed session's Results-screen-relevant summary, persisted by
    /// <see cref="SessionHistoryStore"/>. A snapshot, not a live reference - the engine's
    /// <see cref="SessionResult"/> is reused elsewhere and must not be mutated after the fact.</summary>
    [Serializable]
    public sealed class SessionHistoryEntry
    {
        public string WorldId;
        public long TimestampUnixSeconds;
        public float HearingAgeEstimate;
        public bool Reliable;
        public int CorrectDetections;
        public int Misses;
        public int FalsePositives;
        public int CorrectRejections;
        public List<FrequencyTrialRecord> FrequencyTrials = new();

        public DateTime TimestampUtc => DateTimeOffset.FromUnixTimeSeconds(TimestampUnixSeconds).UtcDateTime;

        /// <summary>Tone-trial detection rate (CorrectDetections / tone trials), used for the
        /// "Today's session" stats - the only per-session number the engine actually measures that
        /// a mechanic-specific stat (e.g. a world's own "fish caught") is not a substitute for.</summary>
        public float DetectionRate => (CorrectDetections + Misses) == 0 ? 0f : CorrectDetections / (float)(CorrectDetections + Misses);

        public static SessionHistoryEntry FromResult(string worldId, SessionResult result)
        {
            return new SessionHistoryEntry
            {
                WorldId = worldId,
                TimestampUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                HearingAgeEstimate = HearingAgeEstimator.EstimateHearingAge(result),
                Reliable = HearingAgeEstimator.IsReliable(result),
                CorrectDetections = result.CorrectDetections,
                Misses = result.Misses,
                FalsePositives = result.FalsePositives,
                CorrectRejections = result.CorrectRejections,
                FrequencyTrials = new List<FrequencyTrialRecord>(result.FrequencyTrials)
            };
        }
    }
}
