using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HearApp.Worlds.VikingBoat
{
    /// <summary>
    /// World 5 - Viking Boat. Another scratch space in the same spirit as
    /// <see cref="HearApp.Worlds.Experiment.ExperimentPresentation"/> (human request 2026-09-26:
    /// prepare another world named Viking Boat, with the real objects to follow shortly) -
    /// camera/lighting are ready, BuildEnvironment is an empty placeholder until a model/texture
    /// pack arrives.
    ///
    /// When adding the real asset, follow the pattern proven out on the Experiment world's Castle
    /// pass: copy the FBX into Resources/Worlds/VikingBoat/Models, copy any texture pack into
    /// Resources/Worlds/VikingBoat/Textures, measure the imported hierarchy once via a throwaway
    /// Editor diagnostic (mesh/camera/light counts, combined bounds, material names) rather than
    /// guessing, strip any embedded camera/light the FBX brings with it (a recurring gotcha - an
    /// embedded camera silently composited over ours with Unity's default sky both times so far),
    /// and wire textures onto fresh URP Lit materials by matching the FBX's material names to the
    /// texture pack's file names.
    /// </summary>
    public sealed class VikingBoatPresentation : WorldPresentationBase
    {
        private Camera _camera;
        private float _startTime;

        private void Awake()
        {
            _startTime = Time.time;
            BuildLighting();
            BuildEnvironment();
            BuildCamera();
            BuildShieldRewardAssets();
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.38f);
            RenderSettings.ambientIntensity = 1f;

            var lightObject = new GameObject("VikingBoatSun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("VikingBoatCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.55f, 0.68f, 0.78f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
            // CoreSafeSquareFit.Awake() runs synchronously inside this AddComponent call and
            // immediately overwrites fieldOfView from its own 50 deg base (portrait-widened per
            // aspect, landing around 87-90 deg on a typical phone) - a fieldOfView assignment made
            // before this line is silently dead code (found 2026-09-27 chasing why the "held
            // shield near bottom edge" math didn't add up: it assumes the actual runtime FOV, not
            // a value set here and then overridden). That wide FOV happens to be what produced the
            // close/dramatic framing already confirmed on-device, so left as the class default
            // rather than narrowed via ConfigurePerspectiveCoreVerticalFov.
            // Position/orientation are driven every frame by Update() (flythrough) instead of
            // set once here.

            // Second camera dedicated to the held/reserve shield (human report 2026-09-27: "Vidis,
            // ze se castecne potapeji do vody? To nesmi, musi byt vzdy v popredi pred vsim" - it must
            // always render in front of everything). The held shield is a real 3D object riding along
            // in front of the main camera, so ordinary depth testing lets nearer world geometry (the
            // river plane, once the flythrough dips low enough near the end) draw over it - a HUD
            // element has no business being depth-tested against the scene at all. Parented with a
            // zero local pose so it always exactly matches the main camera's position/rotation
            // without extra per-frame code; renders on its own layer, after the main camera, and
            // clears only the depth buffer first (not color) so it composites on top of whatever the
            // main camera already drew instead of erasing it.
            var overlayObject = new GameObject("VikingBoatHeldShieldCamera");
            overlayObject.transform.SetParent(_camera.transform, false);
            _heldShieldCamera = overlayObject.AddComponent<Camera>();
            _heldShieldCamera.clearFlags = CameraClearFlags.Depth;
            _heldShieldCamera.cullingMask = 1 << HeldShieldLayer;
            _heldShieldCamera.nearClipPlane = _camera.nearClipPlane;
            _heldShieldCamera.farClipPlane = _camera.farClipPlane;
            _heldShieldCamera.depth = _camera.depth + 1;
            _camera.cullingMask &= ~(1 << HeldShieldLayer);

            // Plain multi-Camera layering (what the block above would be on the built-in render
            // pipeline) doesn't work as-is on URP (found the hard way 2026-09-27: the whole screen
            // went solid dark blue the moment a second enabled Camera existed) - URP treats every
            // Camera as an independent "Base" pass unless explicitly told otherwise, so the second
            // camera's own base pass was replacing the first camera's output rather than compositing
            // over it. URP's actual mechanism for this is Camera Stacking: mark the held-shield
            // camera as an Overlay and add it to the main camera's stack so URP composites them in
            // one pass in the intended order.
            _heldShieldCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            _camera.GetUniversalAdditionalCameraData().cameraStack.Add(_heldShieldCamera);
        }

        // An otherwise-unused layer index (no other world/shell code in this project touches
        // Camera.cullingMask or GameObject.layer - confirmed by a project-wide search 2026-09-27),
        // picked from the high end of the user-defined range to minimize any chance of collision.
        private const int HeldShieldLayer = 30;
        private Camera _heldShieldCamera;

        // Flythrough: the camera glides along the hull's side at shield height so each shield on
        // the gunwale crosses the frame one at a time as we pass it. The ship is recentred in
        // BuildEnvironment so its combined bounds sit at local origin; measured combined size was
        // (1.65, 2.39, 4.58), so half-length along Z is ~2.3m - the span below adds a margin past
        // both ends so shields visibly enter/exit frame. Kept within the flat midship section (not
        // the curled bow/stern, which sit outside this span) so the rail stays level and the
        // shields pass by at a consistent height/spacing.
        //
        // One single one-way pass across the whole session instead of an 8s back-and-forth loop
        // (human request 2026-09-27: over the 30s session there should be exactly one flyover from
        // front to back, from the side) - FlythroughDuration below must track
        // GameFlowController.SessionDurationSeconds (private to that class, so not referenced
        // directly; both are 30f by design intent, not coincidence - keep them in sync by hand).
        //
        // A straight track parallel to the hull (constant X, varying only Z, always looking
        // perpendicular across) read on-device as a flat left-to-right pan, not a flight (human
        // report 2026-09-27: "ta let jde zleva doprava... melo by to jit sikmo" - the flight goes
        // left-to-right, it should go obliquely). Fixed by moving BOTH X and Z over the course of
        // the pass - a genuine diagonal approach that swoops in from wide-and-forward of the bow to
        // close-and-aft of the stern along the port side, plus a forward-biased look target so the
        // camera visibly banks into its own direction of travel instead of just tracking sideways.
        // Direction (bow at -Z, stern at +Z) is a best guess pending an on-device look at which end
        // is actually the bow; if it turns out backwards, swap Start/End below.
        private static readonly Vector3 FlythroughStart = new(3.0f, -0.2f, -2.6f);
        private static readonly Vector3 FlythroughEnd = new(0.9f, -0.7f, 2.6f);
        private const float FlythroughLookAheadZ = 1.1f;
        private const float FlythroughDuration = 30f;

        private void Update()
        {
            if (_camera == null) return;

            float elapsed = Time.time - _startTime;
            float normalized = Mathf.Clamp01(elapsed / FlythroughDuration);
            Vector3 pos = Vector3.Lerp(FlythroughStart, FlythroughEnd, normalized);

            _camera.transform.position = pos;
            _camera.transform.LookAt(new Vector3(-0.4f, pos.y, pos.z + FlythroughLookAheadZ), Vector3.up);

            // CoreSafeSquareFit can change the main camera's fieldOfView on an aspect/orientation
            // change - keep the held-shield overlay camera's projection identical so the held shield
            // never appears to shift or resize relative to the rest of the frame.
            if (_heldShieldCamera != null) _heldShieldCamera.fieldOfView = _camera.fieldOfView;
        }

        // Reward shields. Original idea (human, 2026-09-27): with each success, a shield should
        // either appear or "fly in" onto the side of the ship - nice and big so its texture is
        // visible. Refined immediately after seeing the first pass (human, same session): the
        // shields could instead be visible from the very start, held very close to the player's
        // eye, and on "success" snap onto the ship - so instead of spawning + cutting the camera
        // away to it, the next
        // shield to be earned is always held close in view (parented to the camera, so it rides
        // along through the ambient flythrough) and on a correct detection it flies from there
        // over to its slot on the hull and snaps into place; the following variant then appears
        // held, ready for the next success.
        //
        // The shield FBX's raw mesh data (measured via a throwaway InspectShieldModel Editor
        // diagnostic using MeshFilter.sharedMesh.bounds, i.e. true local/object space - NOT
        // Renderer.bounds, which is a world AABB and was misleading here) is a disc lying in the
        // local XY plane, thin along local Z: mesh.bounds size (2.15, 2.17, 0.26). The imported
        // asset's default transform.localRotation (270, 0, 0) is a separate FBX-import correction
        // baked onto the root - assigning our own localRotation below REPLACES that default
        // rather than composing with it, so the rotation here is derived straight from the raw
        // mesh axes, not from the default-oriented appearance. Euler(0, 90, 0) sends local Z (the
        // disc's thin/normal axis) to world X - a shield mounted on the hull's side, facing
        // outward - while local Y (wide) becomes world Y (vertical) and local X (wide) becomes
        // world Z (along the ship's length).
        private const float ShieldNativeDiameter = 2.15f;
        // Held/reserve size (human report 2026-09-27, this round: "stity v zasobniku... udelej je
        // i o neco mensi" - the reserve shields, make them a bit smaller too) - a modest trim from
        // the previous 0.42.
        private const float HeldShieldTargetDiameter = 0.34f;
        private const float HeldShieldScale = HeldShieldTargetDiameter / ShieldNativeDiameter;
        // Mounted-on-hull size - human report the SAME round, about the shields already on the ship:
        // "nehorazne vysoko a silene moc velke" (outrageously high AND insanely too big). Cut hard,
        // separately from the held size above - see AttachShieldRoutine, which now lerps localScale
        // from Held to Mounted during the fly-in instead of leaving the held scale untouched.
        private const float MountedShieldTargetDiameter = 0.22f;
        private const float MountedShieldScale = MountedShieldTargetDiameter / ShieldNativeDiameter;
        private static readonly Quaternion ShieldMountRotation = Quaternion.Euler(0f, 90f, 0f);
        private const int ShieldVariantCount = 6;

        // X re-derived 2026-09-27 (human report: "Stity se umistuji spatne... umisti je co nejblize
        // toho stitu, co uz tam defaultne je" - shields are placed wrong, put them as close as
        // possible to the shield that is already there by default). A throwaway
        // InspectVikingShieldCluster Editor diagnostic (clustering submesh 1's vertices by their UV -
        // map_ShipV_002's texture reuses the exact same red/blue quartered art as our reward shield
        // in one small corner) found the hull already carries two small baked-in shield decorations,
        // one per side, at RAW asset-space (+-0.52, 0.35, 0).
        //
        // Y was wrong, though (human report 2026-09-27, next round: mounted shields "nehorazne
        // vysoko" - outrageously high - matching an on-device screenshot with the shields sitting up
        // by the mast/rigging). Root cause: that 0.35 is the shield decal's RAW Y, straight from the
        // unmodified source asset - but BuildEnvironment recenters the actual ship instance by
        // -SourceBoundsCenter (Y=1.17), so every displayed/local-space Y used elsewhere in this class
        // (the flythrough path, the water line, the earlier rail-half-span measurement) is
        // (rawY - 1.17), NOT the raw value directly. 0.35 used as-is put the mount a full 1.17 units
        // too high. A follow-up InspectHullMidshipProfile diagnostic (this time correctly reading the
        // hull's OWN outer edge in the same recentred space everything else already uses) found the
        // hull's actual widest point at midship sits at displayed Y in [-0.87, -0.77], X~=0.54 - i.e.
        // low on the side, well below the mast, not up near the rigging. X=0.52 happens to already be
        // right (SourceBoundsCenter.x is 0, so X needed no correction) - only Y moves.
        private const float ShieldRailX = 0.52f;
        private const float ShieldRailY = -0.8f;
        // HalfSpan/MinSpacing re-derived the same round shields were found floating near the bow
        // (human report: shields hanging over open water, and separately clipping into the curled
        // prow). A throwaway InspectHullRailProfile Editor diagnostic (bucketing the hull's own
        // vertices by Z and reading the rail's outer X edge in each slice, in the same displayed-Y
        // band as the corrected mount above) measured the real profile: outer edge ~0.54 at Z=0
        // (matching the default shield, as expected) but only ~0.40 by Z=+-0.6 and collapsed to
        // ~0.10-0.25 by Z=+-1.8/2.2 as the hull narrows into the curled bow/stern. Kept the span
        // tight to where the profile stays close to X=0.52, and tightened spacing to match the
        // smaller MountedShieldTargetDiameter above so more shields still fit within that strip.
        private const float ShieldRailHalfSpan = 0.6f;
        private const float ShieldMinSpacing = 0.26f;
        // Raised from 6 (human report 2026-09-27: "kolik stitu mame? Zda se mi jich malo" - how many
        // do we have, seems like too few). The old cap of 6 was really a hidden side effect of
        // ShieldVariantCount (also 6) - SpawnHeldShield refused to hand out a 7th shield at all once
        // it ran out of distinct textures. There are still only 6 painted variants (no new art), so
        // beyond the 6th shield AttachShieldRoutine now cycles back through them via modulo instead
        // of hard-stopping - a repeated pattern reads far better than no reward shield appearing.
        private const int MaxShields = 10;
        private readonly List<float> _placedShieldZs = new();

        // Held close in front of the camera (child of the camera transform, so it rides along
        // through the flythrough) - originally centred near the player's eye per human direction,
        // moved down toward the bottom edge of the frame on further human direction 2026-09-27 (a
        // HUD-style "held item" look, like a carried weapon in an FPS view, rather than floating
        // centre-screen). NOTE the real effective vertical FOV here is NOT the 42 the first attempt
        // assumed - CoreSafeSquareFit overrides it at runtime to ~87-90 on a typical phone (see
        // BuildCamera's note), so the visible half-height at 1.0 distance is tan(~44) =~ 0.96, not
        // tan(21) =~ 0.38 - -0.7 (not the first attempt's -0.34, which only nudged it slightly
        // below centre) is what actually reads as sitting down at the bottom edge. Facing the
        // camera means the mesh's local-Z normal (see rotation note above) must point along local
        // -Z here, with local Y kept as up: Euler(0, 180, 0) does exactly that.
        private static readonly Vector3 HeldShieldLocalPosition = new(0f, -0.7f, 1.0f);
        private static readonly Quaternion HeldShieldLocalRotation = Quaternion.Euler(0f, 180f, 0f);

        private GameObject _shieldAsset;
        private Material[] _shieldMaterials;
        private Transform _heldShield;
        private int _nextVariantIndex;
        private int _shieldsPlaced;

        private void BuildShieldRewardAssets()
        {
            _shieldAsset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShield");
            if (_shieldAsset == null)
            {
                Debug.LogError("[VikingBoat] Could not load VikingShield model from Resources.");
                return;
            }

            _shieldMaterials = new Material[ShieldVariantCount];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            for (int i = 0; i < ShieldVariantCount; i++)
            {
                var material = new Material(shader);
                material.mainTexture = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/Shields/Shield{i + 1}_Albedo");
                var normalMap = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/Shields/Shield{i + 1}_Normal");
                if (normalMap != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_BumpMap", normalMap);
                }

                _shieldMaterials[i] = material;
            }

            SpawnHeldShield();
        }

        private void SpawnHeldShield()
        {
            // Was `_nextVariantIndex >= _shieldMaterials.Length` - an accidental second, lower cap
            // (6 painted variants) hiding behind MaxShields' own cap (also 6 at the time), so a 7th
            // shield could never be offered even once MaxShields was raised above 6 (see MaxShields'
            // comment above). Now cycles through the 6 variants via modulo below instead.
            if (_shieldAsset == null || _nextVariantIndex >= MaxShields) return;

            var instance = Instantiate(_shieldAsset, _camera.transform);
            instance.name = $"HeldShield_{_nextVariantIndex}";
            instance.transform.localPosition = HeldShieldLocalPosition;
            instance.transform.localRotation = HeldShieldLocalRotation;
            instance.transform.localScale = Vector3.one * HeldShieldScale;
            SetLayerRecursively(instance, HeldShieldLayer);

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _shieldMaterials[_nextVariantIndex % _shieldMaterials.Length];

            _heldShield = instance.transform;
            _nextVariantIndex++;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private IEnumerator AttachShieldRoutine()
        {
            if (_heldShield == null || _shieldsPlaced >= MaxShields)
            {
                RaiseListeningSafe();
                yield break;
            }

            Transform shield = _heldShield;
            _heldShield = null;
            _shieldsPlaced++;

            // Target Z is wherever the camera currently is (not a pre-baked slot) - with the
            // flythrough now a long diagonal sweep rather than a short straight track, a fixed slot
            // list authored for the old camera range meant an early success (camera still near the
            // start) could get assigned a slot far down the rail, so the shield had to cover a huge
            // apparent distance and read as flying away from the ship instead of snapping onto it
            // (human report 2026-09-27: shields landing far from the ship). Nudged forward in
            // MinSpacing steps past any already-placed shield so they never overlap.
            float desiredZ = Mathf.Clamp(_camera.transform.position.z, -ShieldRailHalfSpan, ShieldRailHalfSpan);
            for (int guard = 0; guard < MaxShields; guard++)
            {
                bool tooClose = false;
                foreach (float z in _placedShieldZs)
                {
                    if (Mathf.Abs(z - desiredZ) < ShieldMinSpacing) { tooClose = true; break; }
                }
                if (!tooClose) break;
                desiredZ = Mathf.Min(desiredZ + ShieldMinSpacing, ShieldRailHalfSpan);
            }
            _placedShieldZs.Add(desiredZ);
            Vector3 targetPosition = new(ShieldRailX, ShieldRailY, desiredZ);

            // Detach from the camera, keeping its current world pose as the flight's start point.
            // Reset off the held-shield overlay layer back to Default (0) at the same time - once
            // mounted this is a normal world object again and must be depth-tested like everything
            // else, not forced in front of the ship it is about to land on.
            shield.SetParent(transform, true);
            SetLayerRecursively(shield.gameObject, 0);
            Vector3 startPosition = shield.localPosition;
            Quaternion startRotation = shield.localRotation;
            Vector3 startScale = shield.localScale;
            Vector3 targetScale = Vector3.one * MountedShieldScale;

            const float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                shield.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
                shield.localRotation = Quaternion.Slerp(startRotation, ShieldMountRotation, eased);
                shield.localScale = Vector3.Lerp(startScale, targetScale, eased);
                yield return null;
            }

            shield.localPosition = targetPosition;
            shield.localRotation = ShieldMountRotation;
            shield.localScale = targetScale;

            SpawnHeldShield();
            RaiseListeningSafe();
        }

        private static readonly Vector3 SourceBoundsCenter = new(0f, 1.17f, -0.2f);

        private void BuildEnvironment()
        {
            var asset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShip");
            if (asset == null)
            {
                Debug.LogError("[VikingBoat] Could not load VikingShip model from Resources.");
                return;
            }

            var pivot = new GameObject("VikingShip").transform;
            pivot.SetParent(transform, false);

            var instance = Instantiate(asset);
            instance.transform.SetParent(pivot, false);
            instance.transform.localPosition = -SourceBoundsCenter;

            // No embedded camera/light this time (the plain .obj format has no concept of either,
            // unlike the Experiment world's FBX assets) - stripped defensively anyway in case a
            // future asset for this world does ship as FBX.
            foreach (var importedCamera in instance.GetComponentsInChildren<Camera>(true))
                Destroy(importedCamera.gameObject);
            foreach (var importedLight in instance.GetComponentsInChildren<Light>(true))
                Destroy(importedLight.gameObject);

            ApplyTextures(instance);
            FixSailOrientation(instance);
            BuildRiver();
        }

        // Mast + sail + rigging read as "sailing backward" on-device (human report 2026-09-27: "Lod
        // pluje naopak. Podivej se na plachtu. Otoc ji o 180 stupnu." - the ship sails the wrong way,
        // look at the sail, rotate it 180 degrees). The whole ship is a single imported mesh with no
        // separate sail transform to rotate (confirmed via a throwaway InspectVikingShip Editor
        // diagnostic - one child GameObject named "default", one Mesh, 3 submeshes split only by
        // material). A second throwaway diagnostic (InspectVikingSail) measured each submesh's own
        // bounds directly from the raw .obj's "o " groups (base1_Cube/base2_Cube.005/Plane, mapped
        // to map_ShipV_001/002/003 in that order) and found submesh 2 ("Plane" - despite the name,
        // it is the mast/sail/rigging assembly, not a simple ground plane) is symmetric about the
        // ship's local X=0/Z=0 centerline (bounds center (0, 1.24, 0)) - i.e. the mast stands right
        // on the rotation axis, so spinning just that submesh's vertices 180 degrees around Y through
        // the origin turns the sail/rigging in place without touching the hull (submeshes 0/1) or
        // needing to reposition anything.
        private const int SailSubmeshIndex = 2;

        private void FixSailOrientation(GameObject instance)
        {
            var meshFilter = instance.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null) return;

            var mesh = meshFilter.mesh; // .mesh (not .sharedMesh) forces a per-instance copy here,
                                         // so this never mutates the shared Resources-loaded asset.
            var indices = mesh.GetTriangles(SailSubmeshIndex);
            var affected = new HashSet<int>(indices);

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            foreach (var i in affected)
            {
                var v = vertices[i];
                vertices[i] = new Vector3(-v.x, v.y, -v.z);
                if (i < normals.Length)
                {
                    var n = normals[i];
                    normals[i] = new Vector3(-n.x, n.y, -n.z);
                }
            }

            mesh.vertices = vertices;
            if (normals.Length == vertices.Length) mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        // Water under the ship (human request 2026-09-27: get the ship onto water).
        //
        // First attempt imported the free CGTrader "river lake in middle of mountain" model the
        // human supplied (river.fbx - no texture pack; the download only ships mesh formats plus a
        // bare, image-less .mtl) directly, scaled up 10x so the camera's travel range would only
        // ever see a small fraction of its overall shape. That did not hold up on-device (human
        // report 2026-09-27): this is a natural, IRREGULAR lake blob with real height variation in
        // its own surface, not a flat plane, and scaling a bumpy, finite-footprint mesh 10x
        // amplifies both problems - the ship read as underwater in some spots and the water
        // vanished entirely in others, wherever the enlarged bumps or the lake's actual edge
        // happened to land relative to the camera. A flat plane primitive sidesteps both issues
        // outright: perfectly flat by construction, and sized here (20x20 units, comfortably past
        // the camera's max reach of X=3/Z=+-2.6) so it can never run out under any camera angle.
        // No metallic - a fully metallic surface (the first attempt's _Metallic=0.15) relies on
        // real environment reflections to look shiny, and this scene has no reflection probe/skybox
        // to reflect, so it just read as an unexpectedly dark patch dominating the frame (human
        // report: "everything got darker") instead of shining. Smoothness alone still gives a
        // plausible water sheen from the specular highlight off the directional light.
        //
        // Second attempt (-0.75) still read as "water inside the ship" (human report 2026-09-27:
        // "voda je v lodi"). Root cause: the hull is recentred so its combined bounds sit at local
        // origin (see SourceBoundsCenter/BuildEnvironment), and the measured combined size's Y
        // component (2.39, see the flythrough comment above) means the hull's own lowest point
        // (keel) sits at local Y = -1.195, well BELOW -0.75 - so an infinite flat plane at -0.75
        // sliced straight through the hollow inside of the hull rather than passing under the
        // keel. The hull has no deck/floor mesh capping that interior and its material is
        // double-sided (see the _Cull fix below), so the slice was visible poking up inside the
        // hull walls instead of being hidden behind solid geometry. Moved below the hull's actual
        // lowest point with a safety margin so the plane can never intersect the mesh at all.
        //
        // Third attempt (RiverScale=2) fixed the intersection but read as "water floating with the
        // ship" instead of a static sea the ship moves relative to (human report 2026-09-27: "voda
        // musi zustat staticka, ne 'plout' s lodi... trochu premyslej o fyzice"). Root cause, found
        // via a throwaway InspectPlaneBuiltin Editor diagnostic: Resources.GetBuiltinResource
        // <Mesh>("Plane.fbx") measures only 1x1 units (bounds extents 0.5/0.5), NOT the 10x10 of
        // Unity's CreatePrimitive(PrimitiveType.Plane) - so at scale 2 the "sea" was really just a
        // 2x2 patch, smaller than the hull's own footprint (1.65 x 4.58), i.e. a raft-sized platform
        // pinned directly under the ship rather than an environment independent of it. Scaled up to
        // 80x80 (comfortably larger than both the hull and the whole flythrough's X/Z travel range)
        // so it reads as a vast, fixed sea the camera/ship pass over, not an object riding along
        // with the ship.
        private const float RiverScale = 80f;
        private const float RiverWaterlineY = -1.3f;

        private void BuildRiver()
        {
            // Built by hand (MeshFilter + MeshRenderer only) instead of GameObject.CreatePrimitive,
            // which always also tries to attach a MeshCollider - this app's build strips the
            // Physics module (unused elsewhere), so that implicit AddComponent<MeshCollider>() call
            // failed at runtime with "Can't add component because class 'MeshCollider' doesn't
            // exist!" straight into the on-device dev console (harmless to the visual result, since
            // the mesh/renderer still got created regardless, but still a real error worth not
            // shipping). This water plane has no gameplay collision to begin with, so no collider
            // is needed anyway.
            var water = new GameObject("River");
            water.transform.SetParent(transform, false);
            water.transform.localPosition = new Vector3(0f, RiverWaterlineY, 0f);
            water.transform.localScale = Vector3.one * RiverScale;
            water.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx");

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var riverMaterial = new Material(shader) { color = new Color(0.16f, 0.40f, 0.55f) };
            riverMaterial.SetFloat("_Smoothness", 0.75f);
            water.AddComponent<MeshRenderer>().sharedMaterial = riverMaterial;
        }

        // The three materials (map_ShipV_001/002/003) each map onto their own BaseColor + Normal
        // texture pair, copied from the source ship_textures.zip into
        // Assets/HearApp/Resources/Worlds/VikingBoat/Textures/ with simplified names.
        private static readonly (string materialName, string textureBaseName)[] ShipMaterials =
        {
            ("map_ShipV_001", "Ship001"),
            ("map_ShipV_002", "Ship002"),
            ("map_ShipV_003", "Ship003"),
        };

        private readonly Dictionary<string, Material> _materialCache = new();

        private void ApplyTextures(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var original = renderer.sharedMaterials;
                var replaced = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                    replaced[i] = original[i] != null ? BuildMaterialFor(original[i].name) : null;
                renderer.sharedMaterials = replaced;
            }
        }

        private Material BuildMaterialFor(string materialName)
        {
            if (_materialCache.TryGetValue(materialName, out var cached)) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            // Root cause of the hull rendering as completely invisible (confirmed 2026-09-27 via a
            // throwaway diagnostic dump - mesh, camera, shader and render pipeline all checked out
            // fine individually, yet nothing drew): this raw .obj's triangle winding comes in
            // inverted, so every face was being backface-culled from any outside camera angle.
            // Double-siding the hull material is the pragmatic fix, rather than re-exporting or
            // flipping winding at import time.
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            var entry = System.Array.Find(ShipMaterials, e => e.materialName == materialName);
            if (entry.textureBaseName != null)
            {
                material.mainTexture = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/{entry.textureBaseName}_BaseColor");
                var normalMap = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/{entry.textureBaseName}_Normal");
                if (normalMap != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_BumpMap", normalMap);
                }
            }
            else
            {
                material.color = Color.gray;
            }

            _materialCache[materialName] = material;
            return material;
        }

        public override void Initialize(WorldContext context)
        {
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            if (outcome.Outcome != TrialOutcome.CorrectDetection)
            {
                RaiseListeningSafe();
                return;
            }

            StartCoroutine(AttachShieldRoutine());
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[VikingBoat] Session complete: {result}");
        }
    }
}
