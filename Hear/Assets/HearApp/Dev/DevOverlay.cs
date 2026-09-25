#if UNITY_EDITOR || DEVELOPMENT_BUILD
using HearApp.Core.HearingEngine;
using HearApp.Core.Shell;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HearApp.Dev
{
    /// <summary>
    /// Development-only overlay (compiled out of non-development player builds) providing an easy
    /// way to simulate trial outcomes and jump straight into any world without a complete real
    /// hearing session, per the handoff's explicit request for such tooling. Also exposes a button
    /// to run the <see cref="IntegrationProofRunner"/> across all three worlds.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        // Defaults hidden: the only toggle used to be the backquote key, which no touchscreen
        // (iOS/Android) has - the panel was permanently stuck open on-device with no way to
        // dismiss it (human report 2026-09-26; the "three-finger double-tap" they'd heard about
        // is Unity's unrelated Rendering Debugger gesture, not this overlay).
        private bool _visible;
        private EarChannel _selectedChannel = EarChannel.Combined;
        private IntegrationProofRunner _proofRunner;

        private void Awake()
        {
            _proofRunner = gameObject.AddComponent<IntegrationProofRunner>();
        }

        private void Update()
        {
            if (Keyboard.current?.backquoteKey.wasPressedThisFrame == true)
                _visible = !_visible;
        }

        private void OnGUI()
        {
            // Small always-visible tap target, touch-friendly - the only way to reach this
            // overlay at all on a device with no keyboard.
            if (GUI.Button(new Rect(Screen.width - 70, 10, 60, 32), _visible ? "DEV ▾" : "DEV ▸"))
                _visible = !_visible;

            if (!_visible) return;

            GUILayout.BeginArea(new Rect(Screen.width - 260, 50, 250, 420), GUI.skin.box);
            GUILayout.Label("HEAR Dev Overlay (`)");

            GUILayout.Label("Channel:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_selectedChannel == EarChannel.Left, "Left")) _selectedChannel = EarChannel.Left;
            if (GUILayout.Toggle(_selectedChannel == EarChannel.Combined, "Both")) _selectedChannel = EarChannel.Combined;
            if (GUILayout.Toggle(_selectedChannel == EarChannel.Right, "Right")) _selectedChannel = EarChannel.Right;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Inject outcome (active session only):");
            var flow = GameFlowController.Instance;
            bool sessionRunning = flow != null && flow.Engine != null && flow.Engine.IsRunning;
            GUI.enabled = sessionRunning;
            if (GUILayout.Button("Correct Detection")) flow.InjectDevOutcome(TrialOutcome.CorrectDetection, _selectedChannel);
            if (GUILayout.Button("Miss")) flow.InjectDevOutcome(TrialOutcome.Miss, _selectedChannel);
            if (GUILayout.Button("False Positive")) flow.InjectDevOutcome(TrialOutcome.FalsePositive, _selectedChannel);
            if (GUILayout.Button("Correct Rejection")) flow.InjectDevOutcome(TrialOutcome.CorrectRejection, _selectedChannel);
            GUI.enabled = true;

            GUILayout.Space(10);
            GUILayout.Label("Quick-start world (skips shell flow):");
            for (int i = 0; i < WorldRegistry.Worlds.Count; i++)
            {
                var entry = WorldRegistry.Worlds[i];
                int index = i;
                if (GUILayout.Button(entry.DisplayName))
                    flow.StartDevSession(index, _selectedChannel == EarChannel.Combined ? AudioOutputMode.Speaker : AudioOutputMode.Headphones);
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Run Integration Proof (see Console)"))
                StartCoroutine(_proofRunner.RunProof());

            GUILayout.EndArea();
        }
    }
}
#endif
