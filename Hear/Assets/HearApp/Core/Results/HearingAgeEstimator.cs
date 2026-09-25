using System;
using System.Collections.Generic;
using System.Linq;
using HearApp.Core.HearingEngine;
using UnityEngine;

namespace HearApp.Core.Results
{
    /// <summary>
    /// Turns raw trial counts into the Results screen's headline "hearing age" number and a
    /// measurement-quality verdict. Deliberately simple and transparent - not a clinical
    /// audiometric norm curve (see docs/13-open-questions.md) - kept honest by only ever being
    /// described as an estimate in the UI, never a diagnosis.
    ///
    /// Core logic works off plain counts/frequency trials rather than <see cref="SessionResult"/>
    /// directly, with thin overloads for both that live engine type and the persisted
    /// <see cref="SessionHistoryEntry"/> snapshot - the Results screen needs to describe both a
    /// just-finished live session and an old session loaded back from storage.
    /// </summary>
    public static class HearingAgeEstimator
    {
        private const float BaselineAge = 30f;
        private const float MinAge = 18f;
        private const float MaxAge = 90f;

        /// <summary>Frequencies at/above this are weighted more heavily: age-related hearing loss
        /// typically shows up in higher frequencies first.</summary>
        private const float HighFrequencyHz = 4000f;

        public static float EstimateHearingAge(SessionResult result) =>
            EstimateHearingAge(result?.FrequencyTrials);

        public static float EstimateHearingAge(SessionHistoryEntry entry) =>
            EstimateHearingAge(entry?.FrequencyTrials);

        public static float EstimateHearingAge(IReadOnlyList<FrequencyTrialRecord> trials)
        {
            if (trials == null || trials.Count == 0) return BaselineAge;

            bool hasLow = trials.Any(t => t.FrequencyHz < HighFrequencyHz);
            bool hasHigh = trials.Any(t => t.FrequencyHz >= HighFrequencyHz);
            float lowRate = DetectionRate(trials, f => f < HighFrequencyHz);
            float highRate = DetectionRate(trials, f => f >= HighFrequencyHz);
            // A band with no trials this session falls back to the other band's rate, rather than
            // letting a phantom 0% pull the estimate off a cliff.
            if (!hasLow) lowRate = highRate;
            if (!hasHigh) highRate = lowRate;

            float age = BaselineAge + (1f - lowRate) * 25f + (1f - highRate) * 35f;
            return Mathf.Clamp(age, MinAge, MaxAge);
        }

        private static float DetectionRate(IReadOnlyList<FrequencyTrialRecord> trials, Func<float, bool> band)
        {
            int total = 0, detected = 0;
            foreach (var t in trials)
            {
                if (!band(t.FrequencyHz)) continue;
                total++;
                if (t.Detected) detected++;
            }
            return total == 0 ? 1f : detected / (float)total;
        }

        /// <summary>A session counts toward the baseline when the player was clearly paying
        /// attention (few false alarms on silent catch trials) and produced enough tone trials to
        /// mean anything.</summary>
        public static bool IsReliable(SessionResult result) =>
            IsReliable(result?.FrequencyTrials?.Count ?? 0, result?.FalsePositives ?? 0, result?.CorrectRejections ?? 0);

        public static bool IsReliable(int toneTrialCount, int falsePositives, int correctRejections)
        {
            if (toneTrialCount < 4) return false;
            int catchTotal = falsePositives + correctRejections;
            if (catchTotal == 0) return true; // no catch trials at all (e.g. a dev-injected session) - don't penalize
            float falseAlarmRate = falsePositives / (float)catchTotal;
            return falseAlarmRate <= 0.3f;
        }

        public enum QualityLevel { Reliable, NeedsAttention, TooFewTrials }

        public readonly struct QualitySummary
        {
            public readonly QualityLevel Level;
            public readonly string Headline;
            public readonly string Detail;

            public QualitySummary(QualityLevel level, string headline, string detail)
            {
                Level = level;
                Headline = headline;
                Detail = detail;
            }
        }

        public static QualitySummary Describe(SessionResult result) =>
            Describe(result?.FrequencyTrials?.Count ?? 0, result?.FalsePositives ?? 0, result?.CorrectRejections ?? 0);

        public static QualitySummary Describe(SessionHistoryEntry entry) =>
            Describe(entry?.FrequencyTrials?.Count ?? 0, entry?.FalsePositives ?? 0, entry?.CorrectRejections ?? 0);

        public static QualitySummary Describe(int toneTrialCount, int falsePositives, int correctRejections)
        {
            if (toneTrialCount < 4)
                return new QualitySummary(QualityLevel.TooFewTrials, "Too short to measure",
                    "Play a full session for a reliable reading.");

            int catchTotal = falsePositives + correctRejections;
            float falseAlarmRate = catchTotal == 0 ? 0f : falsePositives / (float)catchTotal;
            if (falseAlarmRate > 0.3f)
                return new QualitySummary(QualityLevel.NeedsAttention, "Needs a quieter moment",
                    "Several taps landed on silent trials - try again somewhere quiet.");

            return new QualitySummary(QualityLevel.Reliable, "Reliable session", "Clear and consistent responses.");
        }
    }
}
