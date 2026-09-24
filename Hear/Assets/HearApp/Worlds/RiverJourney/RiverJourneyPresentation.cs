using System.Collections;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;

namespace HearApp.Worlds.RiverJourney
{
    /// <summary>
    /// World 3 - River Journey (3D). A primitive-geometry canoe follows a small hand-rolled
    /// waypoint path (no Splines package added - a single curve does not justify a new
    /// dependency here). The player never steers; <see cref="SetSessionProgress"/> alone drives
    /// continuous drift along the path so the destination is always reached regardless of hearing
    /// performance, while <see cref="PresentOutcome"/> only adds a paddle-stroke + short forward
    /// impulse on top. The settlement is a neutral placeholder (a couple of cabins + a campfire
    /// glow, no specific real-world culture) since the final settlement/cultural direction is an
    /// explicit open question (docs/09, docs/13) - the reference concept art's motifs are
    /// not reproduced here.
    /// </summary>
    public sealed class RiverJourneyPresentation : WorldPresentationBase
    {
        private static readonly Vector3[] Waypoints =
        {
            new(0f, 0f, 0f),
            new(1.5f, 0f, 20f),
            new(-1f, 0f, 40f),
            new(0.5f, 0f, 60f),
            new(0f, 0f, 80f),
        };

        private Camera _camera;
        private Transform _canoe;
        private Transform _canoeHull;
        private Quaternion _canoeHullBaseLocalRotation;
        private Vector3 _canoeHullBaseLocalPosition;
        private float _baseProgress; // from SetSessionProgress
        private float _impulseBoost; // temporary extra distance from correct detections, decays
        private float _traveledDistance;
        private float _totalPathLength;

        private void Awake()
        {
            var camObj = new GameObject("RiverJourneyCamera") { tag = "MainCamera" };
            _camera = camObj.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.55f, 0.72f, 0.85f);
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<CoreSafeSquareFit>();

            BuildRiverAndBanks();
            BuildSettlementPlaceholder();
            _canoe = BuildCanoe();

            _totalPathLength = ComputePathLength();
        }

        public override void Initialize(WorldContext context)
        {
            _baseProgress = 0f;
            _impulseBoost = 0f;
            _traveledDistance = 0f;
            PositionCanoeAndCamera();
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            if (outcome.Outcome == TrialOutcome.CorrectDetection)
                StartCoroutine(PaddleStroke(outcome.Channel));
            else
                RaiseListeningSafe();
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
            _baseProgress = normalizedProgress;
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[RiverJourney] Session complete: {result}");
        }

        private void Update()
        {
            // Continuous drift tied to session progress (Session Progress), plus a decaying
            // extra boost from recent correct detections (Performance Presentation) - these are
            // deliberately separate signals summed only for visual travel distance.
            _impulseBoost = Mathf.MoveTowards(_impulseBoost, 0f, Time.deltaTime * 2f);
            PositionCanoeAndCamera();
        }

        private void PositionCanoeAndCamera()
        {
            float distance = Mathf.Clamp01(_baseProgress) * _totalPathLength + _impulseBoost;
            distance = Mathf.Min(distance, _totalPathLength);
            _traveledDistance = distance;

            Vector3 pos = SampleAtDistance(distance, out Vector3 forward);
            _canoe.position = pos;
            if (forward.sqrMagnitude > 0.0001f)
                _canoe.rotation = Quaternion.LookRotation(forward, Vector3.up);

            _camera.transform.position = pos + new Vector3(0f, 2.2f, -5f);
            _camera.transform.LookAt(pos + Vector3.up * 0.5f);
        }

        private IEnumerator PaddleStroke(EarChannel channel)
        {
            // Post-classification lateralization only: side chosen from the classified ear when
            // in Headphones mode, otherwise alternates naturally.
            float sideSign = channel switch
            {
                EarChannel.Left => -1f,
                EarChannel.Right => 1f,
                _ => (Random.value < 0.5f ? -1f : 1f)
            };

            // Applied to the HULL (child of the path-following root), not the root itself - the
            // root's position/rotation are re-driven every Update() to track the path, so any
            // cosmetic offset must live one level down to avoid fighting that assignment.
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                float bump = Mathf.Sin(p * Mathf.PI);
                _canoeHull.localPosition = _canoeHullBaseLocalPosition + Vector3.up * (bump * 0.12f);
                _canoeHull.localRotation = _canoeHullBaseLocalRotation * Quaternion.Euler(0f, 0f, -sideSign * bump * 6f);
                yield return null;
            }
            _canoeHull.localPosition = _canoeHullBaseLocalPosition;
            _canoeHull.localRotation = _canoeHullBaseLocalRotation;

            _impulseBoost += 1.5f; // short forward acceleration/momentum impulse
            RaiseListeningSafe();
        }

        private Vector3 SampleAtDistance(float distance, out Vector3 forward)
        {
            float accumulated = 0f;
            for (int i = 0; i < Waypoints.Length - 1; i++)
            {
                Vector3 a = Waypoints[i];
                Vector3 b = Waypoints[i + 1];
                float segLength = Vector3.Distance(a, b);
                if (distance <= accumulated + segLength || i == Waypoints.Length - 2)
                {
                    float t = segLength > 0f ? Mathf.Clamp01((distance - accumulated) / segLength) : 0f;
                    forward = (b - a).normalized;
                    return Vector3.Lerp(a, b, t);
                }
                accumulated += segLength;
            }
            forward = Vector3.forward;
            return Waypoints[^1];
        }

        private float ComputePathLength()
        {
            float total = 0f;
            for (int i = 0; i < Waypoints.Length - 1; i++)
                total += Vector3.Distance(Waypoints[i], Waypoints[i + 1]);
            return total;
        }

        private void BuildRiverAndBanks()
        {
            var river = GameObject.CreatePrimitive(PrimitiveType.Plane);
            river.name = "River";
            river.transform.position = new Vector3(0f, -0.05f, 40f);
            river.transform.localScale = new Vector3(1.2f, 1f, 9f);
            SetUrpColor(river, new Color(0.25f, 0.45f, 0.6f));

            var bankLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bankLeft.name = "BankLeft";
            bankLeft.transform.position = new Vector3(-6f, 0f, 40f);
            bankLeft.transform.localScale = new Vector3(4f, 0.5f, 90f);
            SetUrpColor(bankLeft, new Color(0.3f, 0.45f, 0.28f));

            var bankRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bankRight.name = "BankRight";
            bankRight.transform.position = new Vector3(6f, 0f, 40f);
            bankRight.transform.localScale = new Vector3(4f, 0.5f, 90f);
            SetUrpColor(bankRight, new Color(0.3f, 0.45f, 0.28f));
        }

        private void BuildSettlementPlaceholder()
        {
            // Neutral placeholder settlement near the end of the journey: simple cabin shapes and
            // a campfire glow, deliberately generic pending real cultural/production art direction.
            for (int i = 0; i < 3; i++)
            {
                var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cabin.name = $"Cabin_{i}";
                cabin.transform.position = new Vector3(5.5f, 0.5f, 72f + i * 3f);
                cabin.transform.localScale = new Vector3(1.5f, 1.2f, 1.5f);
                SetUrpColor(cabin, new Color(0.45f, 0.32f, 0.22f));
            }

            var fire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fire.name = "CampfireGlow";
            fire.transform.position = new Vector3(4.5f, 0.3f, 78f);
            fire.transform.localScale = Vector3.one * 0.4f;
            SetUrpColor(fire, new Color(1f, 0.5f, 0.15f));
        }

        private Transform BuildCanoe()
        {
            var root = new GameObject("CanoeRoot");

            var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hull.name = "Canoe";
            hull.transform.SetParent(root.transform, worldPositionStays: false);
            hull.transform.localScale = new Vector3(0.5f, 0.3f, 1.6f);
            hull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie the capsule flat
            SetUrpColor(hull, new Color(0.5f, 0.32f, 0.2f));

            _canoeHull = hull.transform;
            _canoeHullBaseLocalPosition = _canoeHull.localPosition;
            _canoeHullBaseLocalRotation = _canoeHull.localRotation;

            return root.transform;
        }

        private static void SetUrpColor(GameObject target, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("The URP Lit shader is unavailable.");

            var material = new Material(shader);
            material.color = color;
            target.GetComponent<Renderer>().material = material;
        }
    }
}
