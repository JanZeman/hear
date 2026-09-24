using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;

namespace HearApp.Worlds.PaperGarden
{
    /// <summary>
    /// World 2 - The Paper Garden (2.5D). "2.5D" here means flat art layers placed at different
    /// Z depths inside a real 3D/perspective scene so camera movement produces genuine parallax,
    /// per docs/08. This slice uses flat placeholder-colored quads standing in for the
    /// eventual layered paper-theatre art - no invented pseudo-Japanese production art yet, per
    /// the handoff's explicit instruction to do cultural research before production art.
    ///
    /// A small ordered list of stage actions advances by one step per CorrectDetection, rather
    /// than repeating a single animation, matching the "puppet/story action" success-event brief.
    /// </summary>
    public sealed class PaperGardenPresentation : WorldPresentationBase
    {
        private static readonly string[] StageActionNames =
        {
            "Gardener rake stroke",
            "Paper crane flies",
            "Puppet crosses bridge",
            "Cat changes position",
            "Sliding doors open"
        };

        private Camera _camera;
        private Transform _puppet;
        private readonly List<Transform> _parallaxLayers = new(); // ordered background -> foreground
        private int _stageActionIndex;
        private float _sessionProgress;

        private void Awake()
        {
            var camObj = new GameObject("PaperGardenCamera") { tag = "MainCamera" };
            _camera = camObj.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.86f, 0.78f, 0.62f);
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<CoreSafeSquareFit>();
            camObj.transform.position = new Vector3(0f, 0f, -12f);

            BuildLayeredStage();
        }

        public override void Initialize(WorldContext context)
        {
            _stageActionIndex = 0;
            _sessionProgress = 0f;
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            if (outcome.Outcome == TrialOutcome.CorrectDetection)
                StartCoroutine(AdvanceStageAction(outcome.Channel));
            else
                RaiseListeningSafe();
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
            _sessionProgress = normalizedProgress;
            // Subtle camera drift/reveal tied to session progress independent of correctness could
            // go here (e.g. slow dolly through the garden); left as a placeholder hook.
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[PaperGarden] Session complete: {result}");
        }

        private IEnumerator AdvanceStageAction(EarChannel channel)
        {
            string action = StageActionNames[_stageActionIndex % StageActionNames.Length];
            _stageActionIndex++;

            // Post-classification lateralization only: nudge the puppet toward the classified
            // side without ever hinting at it beforehand.
            float targetX = channel switch
            {
                EarChannel.Left => -2.5f,
                EarChannel.Right => 2.5f,
                _ => _puppet.position.x
            };

            Debug.Log($"[PaperGarden] Stage action: {action}");
            float startX = _puppet.position.x;
            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                _puppet.position = new Vector3(Mathf.Lerp(startX, targetX, p), _puppet.position.y, _puppet.position.z);
                yield return null;
            }

            RaiseListeningSafe();
        }

        private void BuildLayeredStage()
        {
            // Ordered back-to-front: paper sky, mountains, architecture, bridge/water, zen garden,
            // puppet, cherry branch, foreground curtains - per docs/08 layer concept.
            AddLayer("Sky", new Color(0.93f, 0.86f, 0.7f), z: 10f, width: 26f, height: 14f);
            AddLayer("Mountains", new Color(0.55f, 0.42f, 0.5f), z: 7f, width: 20f, height: 6f, yOffset: 1.5f);
            AddLayer("Architecture", new Color(0.35f, 0.25f, 0.3f), z: 5f, width: 14f, height: 5f, yOffset: 0.5f);
            AddLayer("BridgeWater", new Color(0.4f, 0.55f, 0.6f), z: 3f, width: 16f, height: 3f, yOffset: -2f);
            AddLayer("ZenGarden", new Color(0.8f, 0.75f, 0.6f), z: 1f, width: 18f, height: 4f, yOffset: -2.5f);

            var puppetObj = new GameObject("Puppet");
            var sr = puppetObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateFlatSprite(new Color(0.15f, 0.1f, 0.12f));
            puppetObj.transform.position = new Vector3(0f, -1f, 0f);
            puppetObj.transform.localScale = new Vector3(0.8f, 1.6f, 1f);
            _puppet = puppetObj.transform;

            AddLayer("CherryBranch", new Color(0.95f, 0.75f, 0.8f), z: -2f, width: 6f, height: 3f, yOffset: 3f);
            AddLayer("ForegroundCurtains", new Color(0.25f, 0.08f, 0.08f), z: -5f, width: 22f, height: 12f, yOffset: 0f);
        }

        private void AddLayer(string name, Color color, float z, float width, float height, float yOffset = 0f)
        {
            var obj = new GameObject(name);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateFlatSprite(color);
            obj.transform.position = new Vector3(0f, yOffset, z);
            obj.transform.localScale = new Vector3(width, height, 1f);
            _parallaxLayers.Add(obj.transform);
        }

        private static Sprite CreateFlatSprite(Color color)
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }
    }
}
