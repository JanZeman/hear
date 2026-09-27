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
            // First seated-pose attempt bent hip/knee bones using a guessed LOCAL rotation axis
            // (Quaternion.Euler on localRotation) and came out completely wrong - legs splayed out
            // sideways like insect limbs (human feedback 2026-09-26: "spis nejaky hmyz nez
            // cloveka"). This retry rotates around a WORLD-space axis instead (this GameObject's own
            // right vector, i.e. "sideways" for the character, since it isn't independently rotated
            // from the canoe) via Transform.Rotate(axis, angle, Space.World) - that sidesteps ever
            // needing to know this rig's internal local bone-axis convention, unlike the local-Euler
            // approach that failed.
            base.PostInstantiation(instantiator, success);
            SceneInstance?.LegacyAnimation?.Stop();
            PoseSeated();
            Loaded?.Invoke();
        }

        private void PoseSeated()
        {
            Vector3 hingeAxis = transform.right;
            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "UpperLeg.L":
                    case "UpperLeg.R":
                        bone.Rotate(hingeAxis, -85f, Space.World);
                        break;
                    case "LowerLeg.L":
                    case "LowerLeg.R":
                        bone.Rotate(hingeAxis, 95f, Space.World);
                        break;
                }
            }
        }
    }
}
