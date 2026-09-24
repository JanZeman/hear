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
    }
}
