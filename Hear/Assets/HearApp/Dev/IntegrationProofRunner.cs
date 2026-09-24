using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HearApp.Dev
{
    /// <summary>
    /// First milestone's key deliverable (docs/11 Phase D, docs/12 acceptance criterion
    /// #2): loads each world's scene in isolation, runs the exact same
    /// <see cref="MockSequenceDriver"/> sequence through a fresh <see cref="TrialEngine"/> against
    /// it, and verifies the resulting <see cref="SessionResult"/> is identical every time. Uses
    /// its own scratch TrialEngine instance so it never interferes with a live shell session.
    /// </summary>
    public sealed class IntegrationProofRunner : MonoBehaviour
    {
        public IEnumerator RunProof()
        {
            var results = new List<(string worldId, SessionResult result)>();

            foreach (var entry in WorldRegistry.Worlds)
            {
                yield return SceneManager.LoadSceneAsync(entry.SceneName, LoadSceneMode.Additive);
                var scene = SceneManager.GetSceneByName(entry.SceneName);

                WorldPresentationBase world = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    world = root.GetComponentInChildren<WorldPresentationBase>(true);
                    if (world != null) break;
                }

                if (world == null)
                {
                    Debug.LogError($"[IntegrationProof] No WorldPresentationBase found in scene '{entry.SceneName}'.");
                    yield return SceneManager.UnloadSceneAsync(scene);
                    continue;
                }

                var engine = gameObject.AddComponent<TrialEngine>();
                var sequence = MockSequenceDriver.GetDefaultSequence();
                engine.BeginSession(world, new WorldContext(AudioOutputMode.Headphones, 12345), sequence.Count);

                foreach (var (outcome, channel) in sequence)
                    yield return StartCoroutine(engine.ProcessTrial(outcome, channel));

                engine.CompleteSession();
                results.Add((entry.Id, engine.CurrentResult));
                Destroy(engine);

                yield return SceneManager.UnloadSceneAsync(scene);
            }

            bool allMatch = true;
            for (int i = 0; i < results.Count; i++)
                Debug.Log($"[IntegrationProof] {results[i].worldId}: {results[i].result}");

            for (int i = 1; i < results.Count; i++)
            {
                if (!results[i].result.HasSameCountsAs(results[0].result))
                {
                    allMatch = false;
                    Debug.LogError($"[IntegrationProof] MISMATCH: {results[0].worldId} vs {results[i].worldId}");
                }
            }

            Debug.Log(allMatch
                ? "[IntegrationProof] PASS - identical SessionResult across all worlds."
                : "[IntegrationProof] FAIL - see mismatches logged above.");
        }
    }
}
