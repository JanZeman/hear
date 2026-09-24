using System.Collections.Generic;
using UnityEngine;

namespace HearApp.Core.HearingEngine
{
    /// <summary>One planned trial: either a real tone at a frequency, or a silent catch trial.</summary>
    public readonly struct TrialSpec
    {
        public readonly bool IsCatchTrial;
        public readonly EarChannel Channel;
        public readonly float FrequencyHz;

        public TrialSpec(bool isCatchTrial, EarChannel channel, float frequencyHz)
        {
            IsCatchTrial = isCatchTrial;
            Channel = channel;
            FrequencyHz = frequencyHz;
        }
    }

    /// <summary>
    /// Builds a simple placeholder trial plan for this architecture slice: one active trial per
    /// reference frequency plus ~15-20% interleaved silent catch trials, alternating ears when in
    /// Headphones mode. The full descending-staircase audiometric protocol is intentionally out of
    /// scope here (see docs/13-open-questions.md); this only needs to exercise all four
    /// TrialOutcome cases and both EarChannel paths for the architecture proof.
    /// </summary>
    public static class TrialPlan
    {
        private static readonly float[] ReferenceFrequenciesHz = { 1000f, 2000f, 4000f, 8000f, 12000f, 16000f };

        public static List<TrialSpec> BuildDefault(AudioOutputMode mode, System.Random rng)
        {
            var plan = new List<TrialSpec>();
            for (int i = 0; i < ReferenceFrequenciesHz.Length; i++)
            {
                EarChannel channel = mode == AudioOutputMode.Headphones
                    ? (i % 2 == 0 ? EarChannel.Left : EarChannel.Right)
                    : EarChannel.Combined;
                plan.Add(new TrialSpec(false, channel, ReferenceFrequenciesHz[i]));
            }

            int catchCount = Mathf.Max(1, Mathf.RoundToInt(plan.Count * 0.2f));
            for (int i = 0; i < catchCount; i++)
            {
                EarChannel channel = mode == AudioOutputMode.Headphones
                    ? (rng.Next(2) == 0 ? EarChannel.Left : EarChannel.Right)
                    : EarChannel.Combined;
                plan.Add(new TrialSpec(true, channel, 0f));
            }

            // Shuffle (Fisher-Yates) so catch trials are interleaved unpredictably rather than
            // trailing at the end - ambient ordering must not become a learnable pattern.
            for (int i = plan.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (plan[i], plan[j]) = (plan[j], plan[i]);
            }

            return plan;
        }
    }
}
