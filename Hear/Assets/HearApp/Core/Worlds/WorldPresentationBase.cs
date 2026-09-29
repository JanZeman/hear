using System;
using HearApp.Core.HearingEngine;
using UnityEngine;

namespace HearApp.Core.Worlds
{
    /// <summary>
    /// Common MonoBehaviour base for world presentation scripts. Exists so
    /// <see cref="GameFlowController"/> can locate the active world's presentation via
    /// GetComponentInChildren&lt;WorldPresentationBase&gt;() (Unity's FindObjectOfType family
    /// cannot target a plain interface), and so the ListeningSafe event plumbing is not
    /// duplicated in every world.
    /// </summary>
    public abstract class WorldPresentationBase : MonoBehaviour, IWorldPresentation
    {
        public event Action ListeningSafe;

        /// <summary>Call once a world's disruptive Success Event presentation has finished.</summary>
        protected void RaiseListeningSafe() => ListeningSafe?.Invoke();

        public abstract void Initialize(WorldContext context);
        public abstract void PresentOutcome(OutcomePresentationContext outcome);
        public abstract void SetSessionProgress(float normalizedProgress);
        public abstract void CompleteSession(SessionResult result);

        /// <summary>Called every 2nd "wasted" tap (one that landed outside an open response
        /// window, or a redundant extra tap after one already claimed it) - see
        /// TrialEngine.RegisterWastedTap's own note. Deliberately NOT part of
        /// IWorldPresentation's "only vocabulary a World is allowed to react to" (this is a raw
        /// input signal, not a classified trial outcome - it must never affect SessionResult/
        /// hearing-measurement data) and deliberately NOT abstract: human request 2026-09-29 only
        /// asked for this in VikingBoat so far ("Pozdeji bychom museli vymyslet, jak to ilustrovat
        /// v ostatnich svetech" - later we'd need to work out how to illustrate this in the other
        /// worlds too), so every other world gets a silent no-op until it defines its own
        /// reaction. Same non-blocking rule as PresentOutcome.</summary>
        public virtual void PresentTapPenalty() { }
    }
}
