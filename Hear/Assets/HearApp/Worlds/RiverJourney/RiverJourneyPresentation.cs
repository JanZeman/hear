using System;
using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.RiverJourney
{
    /// <summary>
    /// World 3, presented to players as River of Echoes. The complete journey is built as one
    /// lightweight procedural 3D scene; session time moves the canoe forward while classified
    /// successes add a small, monotonic paddle boost.
    /// </summary>
    public sealed class RiverJourneyPresentation : WorldPresentationBase
    {
        private const float JourneyDurationSeconds = 30f;
        private const float JourneyStartZ = 2f;
        private const float JourneyEndZ = 96f;

        private sealed class Villager
        {
            public Transform LeftArm;
            public Transform RightArm;
            public Quaternion LeftArmRest;
            public Quaternion RightArmRest;
            public float Phase;
            public bool IsChief;
        }

        private readonly Dictionary<Color32, Material> _materials = new();
        private readonly Dictionary<int, Mesh> _coneMeshes = new();
        private readonly List<Material> _ownedMaterials = new();
        private readonly List<Mesh> _generatedMeshes = new();
        private readonly List<Villager> _villagers = new();
        private readonly List<Light> _fireLights = new();

        private Camera _camera;
        private Light _sun;
        private Transform _canoe;
        private Transform _canoeHull;
        private Transform _paddle;
        private Transform _water;
        private Transform _villageDetails;
        private Transform _dock;
        private Transform _villagerGroup;
        private Transform _chiefGroup;
        private Transform _smokeGroup;
        private ParticleSystem _leftSplash;
        private ParticleSystem _rightSplash;

        private float _journeyProgress;
        private float _sessionProgress;
        private float _elapsedSeconds;
        private float _boostSeconds;
        private int _nextAlternateSide = -1;
        private bool _sessionActive;
        private bool _cameraInitialized;
        private Vector3 _paddleRestPosition;
        private Quaternion _paddleRestRotation;
        private Quaternion _hullRestRotation;

        public float JourneyProgress => _journeyProgress;

        private void Awake()
        {
            BuildLighting();
            BuildCamera();
            BuildBackdropAndRiver();
            BuildBanksAndForest();
            BuildVillage();
            BuildCanoe();
            UpdateJourneyScene();
        }

        private void OnDestroy()
        {
            foreach (var material in _ownedMaterials)
                if (material != null)
                    Destroy(material);
            foreach (var mesh in _generatedMeshes)
                if (mesh != null)
                    Destroy(mesh);
        }

        public override void Initialize(WorldContext context)
        {
            _journeyProgress = 0f;
            _sessionProgress = 0f;
            _elapsedSeconds = 0f;
            _boostSeconds = 0f;
            _nextAlternateSide = -1;
            _sessionActive = true;
            _cameraInitialized = false;
            UpdateJourneyScene();
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            if (outcome.Outcome != TrialOutcome.CorrectDetection)
            {
                RaiseListeningSafe();
                return;
            }

            StartCoroutine(PaddleStroke(ResolvePaddleSide(outcome.Channel)));
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
            _sessionProgress = Mathf.Max(_sessionProgress, Mathf.Clamp01(normalizedProgress));
        }

        public override void CompleteSession(SessionResult result)
        {
            _sessionActive = false;
            _sessionProgress = 1f;
            _journeyProgress = 1f;
            UpdateJourneyScene();
            Debug.Log($"[RiverOfEchoes] Session complete: {result}");
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (_sessionActive)
            {
                _elapsedSeconds += deltaTime;
                _journeyProgress = Mathf.Max(
                    _journeyProgress,
                    _sessionProgress,
                    Mathf.Clamp01(_elapsedSeconds / JourneyDurationSeconds));

                if (_boostSeconds > 0f)
                {
                    float boostDelta = Mathf.Min(deltaTime, _boostSeconds);
                    _journeyProgress = Mathf.Clamp01(
                        _journeyProgress + boostDelta / JourneyDurationSeconds * 2.5f);
                    _boostSeconds = Mathf.Max(0f, _boostSeconds - deltaTime);
                }
            }

            UpdateJourneyScene();
            UpdateCamera(deltaTime);
            UpdateVillagers();
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.43f, 0.39f, 0.42f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.55f, 0.40f, 0.43f);

            var lightObject = new GameObject("RiverOfEchoesSun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(30f, -32f, 0f);
            _sun = lightObject.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = new Color(1f, 0.73f, 0.55f);
            _sun.intensity = 1.05f;
            _sun.shadows = LightShadows.Soft;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("RiverOfEchoesCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.77f, 0.49f, 0.46f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 180f;
            _camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
        }

        private void BuildBackdropAndRiver()
        {
            for (int i = 0; i < 7; i++)
            {
                float x = -24f + i * 8f;
                float height = 19f + (i % 3) * 5f;
                CreateCone(
                    transform,
                    $"DistantMountain_{i}",
                    new Vector3(x, -1f, 142f + (i % 2) * 5f),
                    new Vector3(10f + (i % 2) * 3f, height, 12f),
                    i % 2 == 0 ? new Color(0.36f, 0.32f, 0.43f) : new Color(0.43f, 0.34f, 0.43f),
                    7);
            }

            _water = CreatePrimitive(
                PrimitiveType.Plane,
                transform,
                "RiverSurface",
                new Vector3(0f, -0.14f, 57f),
                new Vector3(1.8f, 1f, 15f),
                new Color(0.16f, 0.31f, 0.38f));
            var waterRenderer = _water.GetComponent<Renderer>();
            waterRenderer.sharedMaterial = CreateMaterial(new Color(0.16f, 0.31f, 0.38f), 0.78f, 0.22f);

            CreatePrimitive(
                PrimitiveType.Plane,
                transform,
                "LeftRiverbank",
                new Vector3(-12f, -0.42f, 57f),
                new Vector3(0.65f, 1f, 15f),
                new Color(0.18f, 0.28f, 0.22f));
            CreatePrimitive(
                PrimitiveType.Plane,
                transform,
                "RightRiverbank",
                new Vector3(12f, -0.42f, 57f),
                new Vector3(0.65f, 1f, 15f),
                new Color(0.21f, 0.29f, 0.22f));
        }

        private void BuildBanksAndForest()
        {
            for (int i = 0; i < 10; i++)
            {
                float z = 5f + i * 10f;
                float stagger = (i % 2) * 1.6f;
                CreateRockCluster(new Vector3(-9f - stagger, 0f, z), 0.85f + (i % 3) * 0.18f);

                if (z < 78f)
                {
                    CreateRockCluster(new Vector3(9f + stagger, 0f, z + 3f), 0.75f + (i % 2) * 0.2f);
                    CreatePineTree(new Vector3(-8.4f - stagger, 0f, z + 4f), 0.8f + (i % 3) * 0.12f);
                    CreatePineTree(new Vector3(8.7f + stagger, 0f, z + 7f), 0.75f + (i % 2) * 0.14f);
                }
                else
                {
                    CreatePineTree(new Vector3(-8.6f - stagger, 0f, z + 3f), 0.85f);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                float z = 18f + i * 15f;
                CreateCone(
                    transform, "LeftCanyonWall",
                    new Vector3(-15f, -1f, z),
                    new Vector3(11f, 17f + (i % 2) * 4f, 19f),
                    new Color(0.3f, 0.29f, 0.35f),
                    7);
                if (z < 80f)
                {
                    CreateCone(
                        transform, "RightCanyonWall",
                        new Vector3(15f, -1f, z + 6f),
                        new Vector3(10f, 15f + (i % 3) * 2f, 18f),
                        new Color(0.32f, 0.3f, 0.34f),
                        7);
                }
            }

            CreateWaterfall(new Vector3(7.4f, 0.6f, 48f));
            CreateWaterfall(new Vector3(-8.1f, 0.4f, 63f));
            for (int i = 0; i < 15; i++)
            {
                float x = Mathf.Sin(i * 1.7f) * (0.5f + i * 0.04f);
                float z = 10f + i * 5.2f;
                CreatePrimitive(
                    PrimitiveType.Cube, transform, "SunsetRiverGlint",
                    new Vector3(x, -0.132f, z),
                    new Vector3(0.18f + (i % 4) * 0.12f, 0.012f, 0.055f),
                    new Color(0.62f, 0.48f, 0.39f));
            }
        }

        private void BuildVillage()
        {
            var silhouette = new GameObject("VillageSilhouette").transform;
            silhouette.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                float z = 99f + i * 3f;
                CreateCone(
                    silhouette,
                    $"DistantTipi_{i}",
                    new Vector3(8.5f + (i % 2) * 2.4f, 0f, z),
                    new Vector3(2.2f, 4.2f + (i % 2) * 0.5f, 2.2f),
                    new Color(0.28f, 0.26f, 0.34f),
                    6);
            }

            _villageDetails = new GameObject("VillageDetails").transform;
            _villageDetails.SetParent(transform, false);
            CreateTipi(_villageDetails, new Vector3(8.8f, 0f, 103f), 3.3f, new Color(0.68f, 0.49f, 0.32f));
            CreateTipi(_villageDetails, new Vector3(11.4f, 0f, 106f), 3.8f, new Color(0.72f, 0.56f, 0.38f));
            CreateTipi(_villageDetails, new Vector3(9.4f, 0f, 110f), 2.9f, new Color(0.56f, 0.42f, 0.34f));
            CreateBanner(_villageDetails, new Vector3(7.9f, 0f, 105f), new Color(0.25f, 0.36f, 0.37f));
            CreateBanner(_villageDetails, new Vector3(12.7f, 0f, 101f), new Color(0.62f, 0.33f, 0.24f));
            CreateFire(_villageDetails, new Vector3(10f, 0f, 100f));
            CreateFire(_villageDetails, new Vector3(12f, 0f, 103f));
            _villageDetails.gameObject.SetActive(false);

            _dock = new GameObject("VillageDock").transform;
            _dock.SetParent(transform, false);
            for (int i = 0; i < 5; i++)
            {
                float z = 91f + i * 2.1f;
                CreatePrimitive(
                    PrimitiveType.Cube, _dock, $"DockPlank_{i}",
                    new Vector3(6.9f, 0.15f, z), new Vector3(4.2f, 0.2f, 1.7f),
                    new Color(0.43f, 0.29f, 0.21f));
                CreatePrimitive(
                    PrimitiveType.Cylinder, _dock, $"DockPost_{i}",
                    new Vector3(5.2f, -0.05f, z), new Vector3(0.24f, 0.8f, 0.24f),
                    new Color(0.31f, 0.24f, 0.21f));
                CreatePrimitive(
                    PrimitiveType.Cylinder, _dock, $"DockPostOuter_{i}",
                    new Vector3(8.6f, -0.05f, z), new Vector3(0.24f, 0.8f, 0.24f),
                    new Color(0.31f, 0.24f, 0.21f));
            }
            _dock.gameObject.SetActive(false);

            _smokeGroup = new GameObject("VillageSmoke").transform;
            _smokeGroup.SetParent(transform, false);
            CreateSmoke(_smokeGroup, new Vector3(8.9f, 2.7f, 103f));
            CreateSmoke(_smokeGroup, new Vector3(11.5f, 3.1f, 106f));
            _smokeGroup.gameObject.SetActive(false);

            _villagerGroup = new GameObject("Villagers").transform;
            _villagerGroup.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                float z = 91f + i * 3.2f;
                _villagers.Add(CreateVillager(
                    _villagerGroup,
                    $"Villager_{i}",
                    new Vector3(8.8f + (i % 2) * 0.5f, 0f, z),
                    1f,
                    false,
                    i * 0.9f));
            }
            _villagerGroup.gameObject.SetActive(false);

            _chiefGroup = new GameObject("ChiefWelcome").transform;
            _chiefGroup.SetParent(transform, false);
            _villagers.Add(CreateVillager(
                _chiefGroup,
                "Chief",
                new Vector3(6.8f, 0f, 97.5f),
                1.18f,
                true,
                0.3f));
            _chiefGroup.gameObject.SetActive(false);
        }

        private void BuildCanoe()
        {
            _canoe = new GameObject("CanoeRoot").transform;
            _canoe.SetParent(transform, false);

            _canoeHull = CreateMeshObject(
                _canoe,
                "CanoeHull",
                BuildCanoeHullMesh(),
                new Vector3(0f, 0f, 0f),
                Vector3.one,
                new Color(0.39f, 0.25f, 0.18f));
            _hullRestRotation = _canoeHull.localRotation;

            for (int side = -1; side <= 1; side += 2)
            {
                var rail = CreatePrimitive(
                    PrimitiveType.Cylinder, _canoe, $"CanoeGunwale_{side}",
                    new Vector3(side * 0.39f, 0.2f, 0f),
                    new Vector3(0.055f, 0.84f, 0.055f),
                    new Color(0.69f, 0.48f, 0.28f));
                rail.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            CreatePrimitive(
                PrimitiveType.Cube, _canoe, "CanoeSeat",
                new Vector3(0f, 0.32f, -0.15f), new Vector3(0.62f, 0.09f, 0.22f),
                new Color(0.55f, 0.37f, 0.24f));

            CreateCanoeist();
            _paddle = CreatePrimitive(
                PrimitiveType.Cylinder, _canoe, "PaddleShaft",
                new Vector3(0f, 1.05f, -0.12f), new Vector3(0.045f, 1.05f, 0.045f),
                new Color(0.65f, 0.43f, 0.26f));
            _paddle.localRotation = Quaternion.Euler(0f, 0f, 62f);
            _paddleRestPosition = _paddle.localPosition;
            _paddleRestRotation = _paddle.localRotation;
            CreatePrimitive(
                PrimitiveType.Cube, _paddle, "PaddleBlade",
                new Vector3(0f, -0.93f, 0f), new Vector3(0.16f, 0.42f, 0.07f),
                new Color(0.57f, 0.36f, 0.22f));

            _leftSplash = CreateSplashSystem(_canoe, "PaddleSplashLeft", new Vector3(-0.78f, -0.09f, 0.28f));
            _rightSplash = CreateSplashSystem(_canoe, "PaddleSplashRight", new Vector3(0.78f, -0.09f, 0.28f));
        }

        private void CreateCanoeist()
        {
            CreatePrimitive(
                PrimitiveType.Capsule, _canoe, "CanoeistBody",
                new Vector3(0f, 0.92f, -0.35f), new Vector3(0.34f, 0.56f, 0.28f),
                new Color(0.44f, 0.34f, 0.27f));
            CreatePrimitive(
                PrimitiveType.Sphere, _canoe, "CanoeistHead",
                new Vector3(0f, 1.72f, -0.28f), new Vector3(0.22f, 0.27f, 0.22f),
                new Color(0.48f, 0.34f, 0.25f));
            CreatePrimitive(
                PrimitiveType.Capsule, _canoe, "CanoeistHair",
                new Vector3(0f, 1.63f, -0.45f), new Vector3(0.2f, 0.25f, 0.16f),
                new Color(0.18f, 0.14f, 0.13f));
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = CreatePrimitive(
                    PrimitiveType.Capsule, _canoe, $"CanoeistArm_{side}",
                    new Vector3(side * 0.33f, 1.02f, -0.2f), new Vector3(0.12f, 0.4f, 0.12f),
                    new Color(0.48f, 0.34f, 0.25f));
                arm.localRotation = Quaternion.Euler(0f, 0f, side * 28f);
            }
        }

        private void CreateTipi(Transform parent, Vector3 position, float height, Color color)
        {
            CreateCone(
                parent, "TipiCanvas", position,
                new Vector3(1.55f, height, 1.55f), color, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                var pole = CreatePrimitive(
                    PrimitiveType.Cylinder, parent, "TipiPole",
                    position + new Vector3(side * 0.52f, height * 0.48f, 0f),
                    new Vector3(0.035f, height * 0.58f, 0.035f),
                    new Color(0.32f, 0.24f, 0.19f));
                pole.localRotation = Quaternion.Euler(0f, 0f, side * 9f);
            }
            CreatePrimitive(
                PrimitiveType.Cube, parent, "TipiEntrance",
                position + new Vector3(0f, 0.52f, -0.74f),
                new Vector3(0.36f, 0.82f, 0.06f),
                new Color(0.25f, 0.19f, 0.17f));
        }

        private void CreateBanner(Transform parent, Vector3 position, Color color)
        {
            CreatePrimitive(
                PrimitiveType.Cylinder, parent, "BannerPole",
                position + new Vector3(0f, 2.2f, 0f), new Vector3(0.07f, 2.4f, 0.07f),
                new Color(0.32f, 0.25f, 0.2f));
            CreatePrimitive(
                PrimitiveType.Cube, parent, "BannerCloth",
                position + new Vector3(0.62f, 2.8f, 0f), new Vector3(1.2f, 1.1f, 0.07f),
                color);
            CreatePrimitive(
                PrimitiveType.Sphere, parent, "BannerEmblem",
                position + new Vector3(0.62f, 2.8f, -0.05f), new Vector3(0.24f, 0.24f, 0.08f),
                new Color(0.82f, 0.64f, 0.38f));
        }

        private Villager CreateVillager(
            Transform parent,
            string name,
            Vector3 position,
            float scale,
            bool isChief,
            float phase)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, -90f, 0f);
            root.localScale = Vector3.one * scale;

            CreatePrimitive(
                PrimitiveType.Capsule, root, "Body",
                new Vector3(0f, 1f, 0f), new Vector3(0.36f, 0.56f, 0.27f),
                isChief ? new Color(0.56f, 0.37f, 0.25f) : new Color(0.34f, 0.4f, 0.34f));
            CreatePrimitive(
                PrimitiveType.Sphere, root, "Head",
                new Vector3(0f, 1.77f, 0f), new Vector3(0.2f, 0.24f, 0.2f),
                new Color(0.48f, 0.34f, 0.25f));
            CreatePrimitive(
                PrimitiveType.Capsule, root, "Hair",
                new Vector3(0f, 1.67f, 0.1f), new Vector3(0.19f, 0.22f, 0.14f),
                new Color(0.17f, 0.14f, 0.13f));

            var leftArm = CreatePrimitive(
                PrimitiveType.Capsule, root, "LeftWelcomingArm",
                new Vector3(-0.36f, 1.18f, 0f), new Vector3(0.11f, 0.39f, 0.11f),
                new Color(0.48f, 0.34f, 0.25f));
            var rightArm = CreatePrimitive(
                PrimitiveType.Capsule, root, "RightWelcomingArm",
                new Vector3(0.36f, 1.18f, 0f), new Vector3(0.11f, 0.39f, 0.11f),
                new Color(0.48f, 0.34f, 0.25f));
            leftArm.localRotation = Quaternion.Euler(0f, 0f, -24f);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, 24f);

            if (isChief)
            {
                CreatePrimitive(
                    PrimitiveType.Cylinder, root, "HeaddressBand",
                    new Vector3(0f, 1.96f, 0f), new Vector3(0.25f, 0.09f, 0.25f),
                    new Color(0.63f, 0.38f, 0.2f));
                for (int i = 0; i < 7; i++)
                {
                    float x = (i - 3) * 0.11f;
                    var feather = CreateCone(
                        root, $"HeaddressFeather_{i}",
                        new Vector3(x, 2.03f + (3 - Mathf.Abs(i - 3)) * 0.03f, 0.03f),
                        new Vector3(0.13f, 0.67f, 0.13f),
                        i % 2 == 0 ? new Color(0.72f, 0.45f, 0.25f) : new Color(0.29f, 0.38f, 0.36f),
                        5);
                    feather.localRotation = Quaternion.Euler(0f, 0f, (i - 3) * 8f);
                }
            }

            return new Villager
            {
                LeftArm = leftArm,
                RightArm = rightArm,
                LeftArmRest = leftArm.localRotation,
                RightArmRest = rightArm.localRotation,
                Phase = phase,
                IsChief = isChief
            };
        }

        private void CreateRockCluster(Vector3 position, float scale)
        {
            for (int i = 0; i < 3; i++)
            {
                var rock = CreatePrimitive(
                    PrimitiveType.Sphere, transform, $"BankRock_{i}",
                    position + new Vector3((i - 1) * 0.75f * scale, 0.1f * scale, i * 0.34f),
                    new Vector3(1.05f, 0.55f, 0.78f) * scale,
                    i == 1 ? new Color(0.34f, 0.34f, 0.36f) : new Color(0.28f, 0.3f, 0.32f));
                rock.localRotation = Quaternion.Euler(i * 13f, i * 22f, i * 17f);
            }
        }

        private void CreatePineTree(Vector3 position, float scale)
        {
            CreatePrimitive(
                PrimitiveType.Cylinder, transform, "PineTrunk",
                position + new Vector3(0f, 1.1f * scale, 0f),
                new Vector3(0.18f, 1.2f, 0.18f) * scale,
                new Color(0.3f, 0.24f, 0.19f));
            for (int tier = 0; tier < 3; tier++)
            {
                CreateCone(
                    transform, $"PineCrown_{tier}",
                    position + new Vector3(0f, (1.45f + tier * 0.82f) * scale, 0f),
                    new Vector3((1.75f - tier * 0.28f) * scale, (2.2f - tier * 0.15f) * scale, (1.75f - tier * 0.28f) * scale),
                    tier == 0 ? new Color(0.2f, 0.3f, 0.27f) : new Color(0.24f, 0.35f, 0.3f),
                    6);
            }
        }

        private void CreateWaterfall(Vector3 position)
        {
            CreatePrimitive(
                PrimitiveType.Cube, transform, "DistantWaterfall",
                position + new Vector3(0f, 1.7f, 0f),
                new Vector3(0.62f, 3.4f, 0.12f),
                new Color(0.34f, 0.52f, 0.57f));
            CreatePrimitive(
                PrimitiveType.Cube, transform, "WaterfallFoot",
                position + new Vector3(0f, 0.12f, 0.3f),
                new Vector3(1.1f, 0.2f, 0.65f),
                new Color(0.42f, 0.57f, 0.58f));
        }

        private void CreateSmoke(Transform parent, Vector3 position)
        {
            var smokeObject = new GameObject("TipiSmoke");
            smokeObject.transform.SetParent(parent, false);
            smokeObject.transform.localPosition = position;
            var particles = smokeObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 4.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = new Color(0.64f, 0.57f, 0.54f, 0.42f);
            main.maxParticles = 18;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 1.5f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = CreateParticleMaterial(new Color(0.75f, 0.68f, 0.64f, 0.42f));
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private ParticleSystem CreateSplashSystem(Transform parent, string name, Vector3 position)
        {
            var splashObject = new GameObject(name);
            splashObject.transform.SetParent(parent, false);
            splashObject.transform.localPosition = position;
            var particles = splashObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
            main.startColor = new Color(0.72f, 0.86f, 0.88f, 0.9f);
            main.maxParticles = 16;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.12f;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = CreateParticleMaterial(new Color(0.82f, 0.93f, 0.92f));
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }

        private void UpdateJourneyScene()
        {
            if (_canoe != null)
            {
                Vector3 pathPosition = PathPosition(_journeyProgress);
                float bob = Mathf.Sin(Time.time * 1.65f) * 0.045f;
                _canoe.position = pathPosition + Vector3.up * bob;
                float t = _journeyProgress;
                Vector3 forward = new Vector3(5.2f * 6f * t * (1f - t), 0f, JourneyEndZ - JourneyStartZ).normalized;
                _canoe.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            SetVisibleFromProgress(_villageDetails, 0.24f, _journeyProgress);
            SetVisibleFromProgress(_dock, 0.42f, _journeyProgress);
            SetVisibleFromProgress(_smokeGroup, 0.4f, _journeyProgress);
            SetVisibleFromProgress(_villagerGroup, 0.62f, _journeyProgress);
            SetVisibleFromProgress(_chiefGroup, 0.88f, _journeyProgress);

            if (_sun != null)
            {
                _sun.color = Color.Lerp(
                    new Color(1f, 0.73f, 0.55f),
                    new Color(0.9f, 0.58f, 0.57f),
                    _journeyProgress);
                _sun.intensity = Mathf.Lerp(1.05f, 0.86f, _journeyProgress);
            }
            for (int i = 0; i < _fireLights.Count; i++)
                _fireLights[i].intensity = 0.7f + Mathf.Sin(Time.time * 5f + i) * 0.12f;
        }

        private void UpdateCamera(float deltaTime)
        {
            if (_camera == null || _canoe == null) return;

            Vector3 desiredPosition = _canoe.position + new Vector3(-1.4f, 3.1f, -7.4f);
            Vector3 lookTarget = _canoe.position + new Vector3(0.3f, 1.65f, 14f);
            float blend = 1f - Mathf.Exp(-deltaTime * 3.5f);
            if (!_cameraInitialized)
            {
                _camera.transform.position = desiredPosition;
                _camera.transform.LookAt(lookTarget);
                _cameraInitialized = true;
                return;
            }

            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desiredPosition, blend);
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - _camera.transform.position, Vector3.up);
            _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, targetRotation, blend);
        }

        private void UpdateVillagers()
        {
            float notice = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.82f, _journeyProgress));
            float chiefWelcome = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.91f, 0.98f, _journeyProgress));
            foreach (var villager in _villagers)
            {
                float weight = villager.IsChief ? chiefWelcome : notice;
                float wave = Mathf.Sin(Time.time * 4.1f + villager.Phase) * 42f * weight;
                villager.LeftArm.localRotation = villager.LeftArmRest * Quaternion.Euler(0f, 0f, -wave);
                villager.RightArm.localRotation = villager.RightArmRest * Quaternion.Euler(0f, 0f, wave);
            }
        }

        private IEnumerator PaddleStroke(int side)
        {
            const float duration = 0.68f;
            float elapsed = 0f;
            bool splashEmitted = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float arc = Mathf.Sin(progress * Mathf.PI);
                _paddle.localPosition = _paddleRestPosition + new Vector3(side * arc * 0.42f, -arc * 0.18f, arc * 0.08f);
                _paddle.localRotation = _paddleRestRotation * Quaternion.Euler(12f * arc, side * 12f * arc, side * 25f * arc);
                _canoeHull.localRotation = _hullRestRotation * Quaternion.Euler(0f, 0f, -side * arc * 4f);

                if (!splashEmitted && progress >= 0.36f)
                {
                    (side < 0 ? _leftSplash : _rightSplash).Emit(10);
                    _boostSeconds = Mathf.Max(_boostSeconds, 0.82f);
                    splashEmitted = true;
                }

                yield return null;
            }

            _paddle.localPosition = _paddleRestPosition;
            _paddle.localRotation = _paddleRestRotation;
            _canoeHull.localRotation = _hullRestRotation;
            RaiseListeningSafe();
        }

        private int ResolvePaddleSide(EarChannel channel)
        {
            if (channel == EarChannel.Left) return -1;
            if (channel == EarChannel.Right) return 1;
            _nextAlternateSide *= -1;
            return _nextAlternateSide;
        }

        private static Vector3 PathPosition(float progress)
        {
            float x = Mathf.Lerp(-0.6f, 4.5f, Mathf.SmoothStep(0f, 1f, progress));
            float z = Mathf.Lerp(JourneyStartZ, JourneyEndZ, progress);
            return new Vector3(x, 0.06f, z);
        }

        private static void SetVisibleFromProgress(Transform target, float threshold, float progress)
        {
            if (target != null && !target.gameObject.activeSelf && Mathf.Clamp01(progress) >= threshold)
                target.gameObject.SetActive(true);
        }

        private void CreateFire(Transform parent, Vector3 position)
        {
            var flame = CreatePrimitive(
                PrimitiveType.Sphere, parent, "CampfireGlow",
                position + new Vector3(0f, 0.34f, 0f),
                new Vector3(0.34f, 0.68f, 0.34f),
                new Color(1f, 0.48f, 0.16f));
            var flameMaterial = CreateMaterial(new Color(1f, 0.48f, 0.16f), 0.25f, 0f);
            flameMaterial.EnableKeyword("_EMISSION");
            flameMaterial.SetColor("_EmissionColor", new Color(1f, 0.24f, 0.06f) * 1.4f);
            flame.GetComponent<Renderer>().sharedMaterial = flameMaterial;
            CreatePrimitive(
                PrimitiveType.Sphere, parent, "CampfireCore",
                position + new Vector3(0f, 0.26f, -0.03f),
                new Vector3(0.18f, 0.38f, 0.18f),
                new Color(1f, 0.78f, 0.36f));
            for (int i = 0; i < 3; i++)
            {
                var log = CreatePrimitive(
                    PrimitiveType.Cylinder, parent, "CampfireLog",
                    position + new Vector3((i - 1) * 0.12f, 0.06f, 0f),
                    new Vector3(0.08f, 0.4f, 0.08f),
                    new Color(0.25f, 0.18f, 0.15f));
                log.localRotation = Quaternion.Euler(0f, 0f, 90f + i * 15f);
            }

            var lightObject = new GameObject("CampfireLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position + new Vector3(0f, 1.1f, 0f);
            var fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.52f, 0.27f);
            fireLight.intensity = 0.7f;
            fireLight.range = 7f;
            fireLight.shadows = LightShadows.None;
            _fireLights.Add(fireLight);
        }

        private Transform CreatePrimitive(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            var renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gameObject.transform;
        }

        private Transform CreateCone(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color,
            int sides)
        {
            if (!_coneMeshes.TryGetValue(sides, out var mesh))
            {
                mesh = BuildConeMesh(sides);
                _coneMeshes.Add(sides, mesh);
            }
            return CreateMeshObject(parent, name, mesh, position, scale, color);
        }

        private Transform CreateMeshObject(
            Transform parent,
            string name,
            Mesh mesh,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gameObject.transform;
        }

        private Material GetMaterial(Color color)
        {
            Color32 key = color;
            if (_materials.TryGetValue(key, out var material)) return material;
            material = CreateMaterial(color, 0.34f, 0f);
            _materials[key] = material;
            return material;
        }

        private Material CreateMaterial(Color color, float smoothness, float metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("The URP Lit shader is unavailable.");

            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            _ownedMaterials.Add(material);
            return material;
        }

        private Material CreateParticleMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                throw new InvalidOperationException("The URP Particles Unlit shader is unavailable.");
            var material = new Material(shader) { color = color };
            _ownedMaterials.Add(material);
            return material;
        }

        private Mesh BuildConeMesh(int sides)
        {
            var vertices = new List<Vector3>(sides * 6);
            var triangles = new List<int>(sides * 6);
            Vector3 center = Vector3.zero;
            Vector3 tip = Vector3.up;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                int start = vertices.Count;
                vertices.Add(p0);
                vertices.Add(tip);
                vertices.Add(p1);
                vertices.Add(center);
                vertices.Add(p1);
                vertices.Add(p0);
                for (int j = 0; j < 6; j++) triangles.Add(start + j);
            }

            var mesh = new Mesh { name = "LowPolyCone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _generatedMeshes.Add(mesh);
            return mesh;
        }

        private Mesh BuildCanoeHullMesh()
        {
            float[] z = { -1.55f, -1.15f, 0f, 1.15f, 1.55f };
            float[] width = { 0.06f, 0.36f, 0.47f, 0.36f, 0.06f };
            float[] bottom = { 0.03f, -0.18f, -0.21f, -0.18f, 0.03f };
            float[] top = { 0.06f, 0.12f, 0.15f, 0.12f, 0.06f };
            var vertices = new List<Vector3>(z.Length * 4);
            for (int i = 0; i < z.Length; i++)
            {
                vertices.Add(new Vector3(-width[i], bottom[i], z[i]));
                vertices.Add(new Vector3(width[i], bottom[i], z[i]));
                vertices.Add(new Vector3(width[i], top[i], z[i]));
                vertices.Add(new Vector3(-width[i], top[i], z[i]));
            }

            var triangles = new List<int>();
            for (int i = 0; i < z.Length - 1; i++)
            {
                int a = i * 4;
                int b = a + 4;
                AddQuad(triangles, a, b, b + 1, a + 1);
                AddQuad(triangles, a + 1, b + 1, b + 2, a + 2);
                AddQuad(triangles, a + 2, b + 2, b + 3, a + 3);
                AddQuad(triangles, a + 3, b + 3, b, a);
            }
            triangles.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
            int end = (z.Length - 1) * 4;
            triangles.AddRange(new[] { end, end + 1, end + 2, end, end + 2, end + 3 });

            var mesh = new Mesh { name = "CanoeHull" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _generatedMeshes.Add(mesh);
            return mesh;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(a);
            triangles.Add(d);
            triangles.Add(c);
        }
    }
}
