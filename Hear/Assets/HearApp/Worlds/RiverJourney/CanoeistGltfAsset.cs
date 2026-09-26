using System;
using GLTFast;
using UnityEngine;

namespace HearApp.Worlds.RiverJourney
{
    /// <summary>
    /// Loads the canoeist glTF model (a free CC0 Quaternius character, see
    /// Assets/StreamingAssets/Models/canoeist.glb) and plays its "Idle" clip by name instead of
    /// glTFast's default auto-play-first-clip behavior - this model's clips are alphabetically
    /// sorted and the first one is "CharacterArmature|Death", not something to default to for a
    /// seated paddler.
    /// </summary>
    public sealed class CanoeistGltfAsset : GltfAsset
    {
        public event Action Loaded;

        protected override void PostInstantiation(IInstantiator instantiator, bool success)
        {
            // The manual bent-knee pose (guessed hip/knee rotation axes) came out completely wrong
            // - legs splayed out sideways like insect limbs, not a seated human (human feedback
            // 2026-09-26: "spis nejaky hmyz nez cloveka"). Reverted rather than keep guessing bone
            // axes blindly; standing tall with legs mostly hidden inside the canoe hull (see the
            // root position offset in RiverJourneyPresentation.CreateCanoeistModel) reads far better
            // than a broken pose.
            base.PostInstantiation(instantiator, success);
            var anim = SceneInstance?.LegacyAnimation;
            if (anim != null)
            {
                string neutral = null;
                string idle = null;
                foreach (AnimationState state in anim)
                {
                    if (state.name.EndsWith("Idle_Neutral", StringComparison.Ordinal)) neutral = state.name;
                    else if (state.name.EndsWith("|Idle", StringComparison.Ordinal)) idle = state.name;
                }
                if (neutral != null) anim.Play(neutral);
                else if (idle != null) anim.Play(idle);
            }
            Loaded?.Invoke();
        }
    }
}
