using System;
using HearApp.Core.HearingEngine;

namespace HearApp.Core.Worlds
{
    /// <summary>
    /// Contract every World (Tide Troubles, The Paper Garden, River Journey, ...) must implement.
    /// Adapted to this codebase's coroutine/event idiom rather than the Task-based signature
    /// sketched in the handoff docs - the separation is what matters, not the exact shape.
    ///
    /// A World must NEVER need to know when a stimulus begins. It only ever receives classified
    /// outcomes after the response window has been resolved (see <see cref="PresentOutcome"/>),
    /// plus session-progress updates that are independent of hearing performance.
    /// </summary>
    public interface IWorldPresentation
    {
        /// <summary>Called once when the world becomes active. Build/reset presentation state here.</summary>
        void Initialize(WorldContext context);

        /// <summary>
        /// Called after a trial has been classified. The world may play a Success Event (or stay
        /// quiet on Miss/FalsePositive/CorrectRejection per its own design) but must not block -
        /// any disruptive/attention-grabbing presentation should run async and signal completion
        /// via <see cref="ListeningSafe"/> so the engine knows when it is safe to start the next
        /// quiet listening window. Ambient motion may continue uninterrupted.
        /// </summary>
        void PresentOutcome(OutcomePresentationContext outcome);

        /// <summary>
        /// Called whenever normalized session progress (0..1) advances, independent of whether the
        /// most recent trial was a hit or a miss. Drives scene/story progression (Session Progress),
        /// as distinct from the extra Performance Presentation triggered by PresentOutcome.
        /// </summary>
        void SetSessionProgress(float normalizedProgress);

        /// <summary>Called once when the session ends (progress has reached 1.0).</summary>
        void CompleteSession(SessionResult result);

        /// <summary>
        /// Raised by the world once it has returned to a listening-safe state (i.e. its most
        /// recent Success Event's disruptive/loud phase has finished) and the engine may open
        /// the next trial's response window. A world that never went "unsafe" may simply raise
        /// this immediately after PresentOutcome returns.
        /// </summary>
        event Action ListeningSafe;
    }
}
