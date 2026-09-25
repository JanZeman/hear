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
        private bool _visible = true;
        private EarChannel _selectedChannel = EarChannel.Combined;
        private int _selectedFrequencyIndex;
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
            if (!_visible) return;

            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 470), GUI.skin.box);
            GUILayout.Label("HEAR Dev Overlay (`)");

            GUILayout.Label("Channel:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_selectedChannel == EarChannel.Left, "Left")) _selectedChannel = EarChannel.Left;
            if (GUILayout.Toggle(_selectedChannel == EarChannel.Combined, "Both")) _selectedChannel = EarChannel.Combined;
            if (GUILayout.Toggle(_selectedChannel == EarChannel.Right, "Right")) _selectedChannel = EarChannel.Right;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"Frequency: {TrialPlan.ReferenceFrequenciesHz[_selectedFrequencyIndex]:0} Hz");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<")) _selectedFrequencyIndex = (_selectedFrequencyIndex - 1 + TrialPlan.ReferenceFrequenciesHz.Length) % TrialPlan.ReferenceFrequenciesHz.Length;
            if (GUILayout.Button(">")) _selectedFrequencyIndex = (_selectedFrequencyIndex + 1) % TrialPlan.ReferenceFrequenciesHz.Length;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Inject outcome (active session only):");
            var flow = GameFlowController.Instance;
            bool sessionRunning = flow != null && flow.Engine != null && flow.Engine.IsRunning;
            float freq = TrialPlan.ReferenceFrequenciesHz[_selectedFrequencyIndex];
            GUI.enabled = sessionRunning;
            if (GUILayout.Button("Correct Detection")) flow.InjectDevOutcome(TrialOutcome.CorrectDetection, _selectedChannel, freq);
            if (GUILayout.Button("Miss")) flow.InjectDevOutcome(TrialOutcome.Miss, _selectedChannel, freq);
            if (GUILayout.Button("False Positive")) flow.InjectDevOutcome(TrialOutcome.FalsePositive, _selectedChannel, freq);
            if (GUILayout.Button("Correct Rejection")) flow.InjectDevOutcome(TrialOutcome.CorrectRejection, _selectedChannel, freq);
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
