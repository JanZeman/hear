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
        // The ship's own root transform (see BuildEnvironment) - Update() moves this to give the
        // ship a real forward velocity instead of just sweeping the camera's gaze across a static
        // hull. Reward shields are parented to this too (see AttachShieldRoutine) so they travel
        // with the ship instead of being left behind in world space.
        private Transform _shipPivot;

        private void Awake()
        {
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

        // Flythrough. The ship is recentred in BuildEnvironment so its combined bounds sit at local
        // origin; measured combined size was (1.65, 2.39, 4.58), so half-length along Z is ~2.3m.
        //
        // One single one-way pass across the whole session instead of an 8s back-and-forth loop
        // (human request 2026-09-27: over the 30s session there should be exactly one flyover from
        // front to back, from the side). Slowed down repeatedly the same class of feedback kept
        // landing on 2026-09-28 ("Musis prulet trochu zpomalit", then "Ta lod nam pluje moc rychle.
        // Trosku ji zpomal", then "...jeste trochu pomalejsi") while this was still one constant-rate
        // pass with a single duration constant (30, matching GameFlowController.SessionDurationSeconds
        // by design -> 45 -> 55 -> 65) - since replaced by the two-phase, distance-triggered speed
        // model below (see FlythroughFastSpeed/FlythroughSlowSpeed), which has no single duration
        // constant left to tune the same way.
        //
        // Direction (bow at -Z, stern at +Z) was a best guess when first chosen (human report
        // 2026-09-27 about a flat left-to-right pan not reading as a flight) - confirmed since by
        // every later piece of shield-row work (ShieldRowStartZ etc.) working out consistently with
        // it, so no longer pending.
        //
        // Re-modelled twice on 2026-09-28. First pass ("sikmeji" - more oblique) had the CAMERA
        // itself translate along the hull's length with a look target barely lagging behind its own
        // position - implementing "more oblique" as a wider gap between the camera's start/end X,
        // which actually made the gaze MORE perpendicular to the hull, the opposite of the request.
        // Human clarification: "lod me jakoby obeplouva. Predstav si, ze sedim na kanoe, co je pred
        // lodi na jejim levoboku. Lod me predjede a ja se na ni divam" - the ship circles around me,
        // as if; imagine I'm sitting in a canoe ahead of the ship on its port side, the ship
        // overtakes me and I watch it - a near-fixed viewpoint. Second pass kept the camera fixed but
        // swept its LOOK TARGET across the still-static hull to fake the sweep - which read as
        // "neprirozene" (unnatural, human report the same day): nothing in the scene ever actually
        // moved relative to the water, so there was no parallax at all, just a camera swivelling on
        // the spot. Final human steer: "ta lod vubec neplave svym vlastnim smerem! Pouzij trochu
        // matematiky a fyziky. Ta lod musi plout 'dopredu' podle toho oc je dopredu pro tu lod!" -
        // the ship isn't sailing in its own direction at all - use some real math/physics, it has to
        // sail forward according to what "forward" means for the ship (its own -Z, bow-first - see
        // the direction note above).
        //
        // So now the SHIP has a real forward velocity along its own -Z axis (_shipPivot's position,
        // not the camera's), while the camera is genuinely fixed - both position AND rotation, like
        // someone sitting still in a canoe watching a real ship go by. All the "arriving, alongside,
        // departing" motion the earlier two attempts tried to fake with camera animation now comes
        // from actual relative motion between the hull and the (stationary) water/background.
        //
        // FlythroughCanoePosition.x=1.2 clears the row's own widest mount point (near the original
        // decal, X~0.54, see HullSideProfile) with margin; .z=-2.4 sits just ahead of the bow's own
        // measured rest-position tip (~-2.0/-2.29), matching "ahead of the ship, port side".
        //
        // Looking due -X (straight across the beam, perpendicular to the ship's own direction of
        // travel) made the ship's real -Z velocity project onto the screen as pure lateral motion -
        // human report the same day: "lod pluje zprava doleva a ne proti me jako pozorovateli!" (the
        // ship sails right to left, not toward me as an observer). Motion toward/away from an
        // observer needs a velocity component ALONG the observer's own line of sight, not just
        // across it - a target perpendicular to travel can only ever show lateral motion, no matter
        // how fast the ship moves. Looking instead toward the ship's own resting centre (world
        // origin, roughly its midship at t=0, before Update() starts moving it) puts a good deal of
        // the gaze direction along the ship's own -Z travel axis - the ship genuinely approaches out
        // of the distance along that sightline before crossing and receding, rather than just
        // sliding past sideways.
        private static readonly Vector3 FlythroughCanoePosition = new(1.2f, -0.7f, -2.4f);
        private static readonly Vector3 FlythroughLookTarget = new(0f, FlythroughCanoePosition.y, 0f);

        // Core distance the ship travels (along -Z) once under way - enough for the stern (rest
        // position ~+2.3, see the class-level hull-size note above) to also end up past the canoe's
        // fixed Z once translated: 2.3 - (-2.4) = 4.7, plus a little margin so the whole hull is
        // convincingly gone by the end rather than just barely past.
        private const float FlythroughCoreTravelDistance = 5.0f;

        // Extra distance the ship starts out beyond its own rest position (along +Z, i.e. further
        // from the canoe before the pass even begins) - human report 2026-09-28, right after the
        // oblique-gaze fix above finally read as "toward me" correctly: "Ted to zacina byt opravdu
        // krasne! Lod ale musi prijet z mnohem vetsi dalky" (now it's starting to look really
        // beautiful, but the ship has to arrive from a much greater distance). Added on top of the
        // ship's rest position rather than folded into FlythroughCoreTravelDistance, so the ENDING
        // position (how far past the canoe the stern ends up) stays exactly what it was - only the
        // starting point moves further out, giving a longer approach.
        private const float FlythroughInitialDistance = 5.0f;

        // Two-phase speed, not one constant rate across the whole pass (human report 2026-09-28:
        // "prvni pripichnute stity nejsou dlouho videt... na zacatku bude propluti lodi rychlejsi,
        // aby doplula do pozice, kdy brzy bude pripichnuty prvni stit a bude videt" - the first
        // attached shields aren't visible for long; let the initial approach be faster, so it
        // reaches the position where the first shield will soon be attached, and it stays visible).
        // Phase one covers the initial approach (including FlythroughInitialDistance) quickly; phase
        // two covers the rest at a much slower, more deliberate pace, so shields have time to arrive
        // on camera and linger once attached.
        //
        // DISTANCE-triggered, not time-triggered - human correction 2026-09-28, after a first
        // attempt switched phases at a fixed elapsed-time constant: "Ty jsi nastavil cas, ale co ja
        // myslel bylo, aby ta lod zabrzdila jakmile prijede blizko ke kamere" (you set a time, but
        // what I meant was for the ship to brake as soon as it arrives close to the camera). A fixed
        // time can't guarantee that - the fast phase needs to cover a fixed DISTANCE at a fixed
        // SPEED, and whatever real time that takes is however long it takes; the brake point is then
        // genuinely "close to the camera" by construction, not by a guessed duration. Speeds below
        // are simply the ones the previous fixed-duration attempt (5s to cover fastPhaseTravel,
        // ~60s for the rest) worked out to, kept as the same-feeling default now that the mechanism
        // is correct.
        private const float FlythroughFastSpeed = 1.3f;
        private const float FlythroughSlowSpeed = 0.06f;

        // See the fastPhaseTravel computation in Update() for the human report this responds to -
        // roughly half the hull's own measured length (combined bounds size.z was 4.58, see the
        // class-level note, half of that is ~2.3).
        private const float FlythroughBrakeLeadDistance = 1.3f;

        // The fast/slow speeds above used to switch instantly at the brake point - a genuine
        // velocity discontinuity, which read as a hard cut rather than a ship actually slowing down
        // (human report 2026-09-28: "Muzes naanimovat to zbrzdeni jako zbrzdeni? Tj. trochu
        // pozvolnejsi?" - can you animate the braking AS braking? i.e. a bit more gradual?). Over
        // this many units of distance BEFORE the brake point, speed now eases from
        // FlythroughFastSpeed down to FlythroughSlowSpeed via smoothstep instead of jumping - see
        // the accumulator below.
        private const float FlythroughBrakeZoneDistance = 1.0f;

        // Distance actually covered so far - an explicit per-frame accumulator (added this update)
        // rather than a closed-form function of elapsed time, because a smooth speed-vs-distance
        // curve (see FlythroughBrakeZoneDistance) has no simple closed form to invert back into
        // distance-as-a-function-of-time; integrating speed*deltaTime every frame sidesteps that
        // entirely and is the standard way to drive this kind of eased motion.
        private float _shipTravelled;

        private void Update()
        {
            if (_camera == null || _shipPivot == null) return;

            // How far the ship needs to travel for the BOW (not the first shield - see below) to
            // reach the canoe's fixed line of sight - computed here (inside a method, not another
            // field's own initializer) rather than as its own static field, since HullSideProfile is
            // declared later in the file and C# only guarantees static field initializers run in
            // textual declaration order.
            //
            // Originally targeted ShieldRowStartZ (the row's first shield), which sits well aft of
            // the bow - human report 2026-09-28: "Ta lod brzi, kdyz uz jsme kamerou v jeji polovine.
            // Rikam, ze musi brzdit DRIVE. Zhruba, kdyz prid prijela do poloviny obrazovky" (the ship
            // brakes when the camera's already halfway alongside it; it has to brake EARLIER -
            // roughly when the bow arrives at the middle of the screen). By the time the first
            // shield reached the canoe's sightline under the old target, the BOW itself (further out
            // along -Z, so it always reaches any given world Z first) had already sailed well past
            // the camera. HullSideProfile[0].z (-2.0) is the bow-most Z the hull-profile diagnostic
            // actually measured a side wall at (see that table's own note) - using it here instead
            // means braking now happens right as the bow itself reaches the camera's sightline, with
            // the first shield (and everything after it) still approaching during the slow phase.
            //
            // Even that undershot it (human follow-up, same day: "Skvela zmena! Ted uz jen doladit a
            // nechat lod brzdit jeste drive - tak o pulku lodi" - great change! now just fine-tune
            // and have the ship brake even earlier - by about half a ship's length) - pulled forward
            // by FlythroughBrakeLeadDistance, ~half the hull's own measured length (see the
            // class-level note on the combined bounds), so braking now starts while the bow is still
            // that much further out rather than exactly at the canoe's own sightline.
            float fastPhaseTravel = FlythroughInitialDistance + (HullSideProfile[0].z - FlythroughCanoePosition.z) - FlythroughBrakeLeadDistance;
            float totalTravel = FlythroughInitialDistance + FlythroughCoreTravelDistance;

            // Ease speed down from FlythroughFastSpeed to FlythroughSlowSpeed over the last
            // FlythroughBrakeZoneDistance units before fastPhaseTravel, instead of cutting instantly
            // at it - see FlythroughBrakeZoneDistance's note.
            float distanceToBrakePoint = fastPhaseTravel - _shipTravelled;
            float speed;
            if (distanceToBrakePoint <= 0f)
                speed = FlythroughSlowSpeed;
            else if (distanceToBrakePoint >= FlythroughBrakeZoneDistance)
                speed = FlythroughFastSpeed;
            else
                speed = Mathf.Lerp(FlythroughSlowSpeed, FlythroughFastSpeed,
                    Mathf.SmoothStep(0f, 1f, distanceToBrakePoint / FlythroughBrakeZoneDistance));

            _shipTravelled = Mathf.Min(_shipTravelled + speed * Time.deltaTime, totalTravel);
            float travelled = _shipTravelled;

            // Bow-first: forward for the ship is -Z (see the direction note above). Starts
            // FlythroughInitialDistance out and advancing "travelled" units brings it back down
            // toward and past its own rest position - see FlythroughInitialDistance's note. Reward
            // shields, parented to this same pivot (see AttachShieldRoutine), travel with it
            // automatically.
            _shipPivot.localPosition = new Vector3(0f, 0f, FlythroughInitialDistance - travelled);

            _camera.transform.position = FlythroughCanoePosition;
            _camera.transform.LookAt(FlythroughLookTarget, Vector3.up);

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
        // Mounted-on-hull size - human report the SAME round, about the shields already on the ship:
        // "nehorazne vysoko a silene moc velke" (outrageously high AND insanely too big). Cut hard,
        // separately from the held size above - see AttachShieldRoutine, which now lerps localScale
        // from Held to Mounted during the fly-in instead of leaving the held scale untouched.
        // Shrunk again 2026-09-28 (human report, with a close-up screenshot: the mounted shield reads
        // clearly bigger than the hull's own decal right next to/behind it) - down toward the
        // original decal's own measured size (its vertex cluster's extents were only ~0.07 in Y/Z, so
        // roughly a 0.14-0.18 diameter).
        private const float MountedShieldTargetDiameter = 0.16f;
        // Both target diameters above are shared across every variant, held or mounted family alike -
        // only the NATIVE diameter (what the raw asset measures at its own baked scale, i.e. the
        // divisor in target/native) differs per family, so held/mounted scale is now computed on the
        // fly per shield (see NativeDiameterFor) instead of being one fixed constant.
        private static readonly Quaternion ShieldMountRotation = Quaternion.Euler(0f, 90f, 0f);
        // 7th variant added 2026-09-28 (human: a "Viking_Shield_1" asset placed under
        // sources/3D/VikingShields1, own full PBR set at 1k - only BaseColor -> Shield7_Albedo and
        // NormalOpenGL -> Shield7_Normal are wired in, matching what SpawnHeldShield's material setup
        // actually uses; the accompanying Roughness/Metallic/AO/Height maps aren't consumed anywhere
        // in this class). Its BaseColor is a multi-part UV atlas (front face, rim, boss, strap) laid
        // out similarly to the existing Shield1-6 textures, not a simple 0-1 front-face image, so it
        // was added straight into the existing per-variant loop below on the assumption the shared
        // shield mesh's UV template is close enough to read correctly - unconfirmed on-device as of
        // this writing.
        private const int OldStyleVariantCount = 7;

        // A second, structurally different pack added the same day (human: "pod 2 jsem vlozil jinou
        // sadu, zkus naimportovat i tuto" - I put another set under [VikingShields]2, try to import
        // this one too). Unlike the OldStyle family above (one shared mesh, swap the material's
        // texture per variant), this FBX ("VikingShieldPack2") ships 8 already-complete, separately
        // named child meshes ("Shield 1".."Shield 8"), each with its own combination of shared
        // trim/rivet/wood submeshes plus one submesh using a material name not shared with any other
        // shield - that odd-one-out submesh is the one with the actual painted pattern (confirmed via
        // a throwaway InspectShieldPack2Detail diagnostic: Shield 1 and Shield 2 both use
        // Material.002/.001/.004 for their common submeshes, but their pattern submesh's material
        // name differs, .005 vs .006). "Shield 1" alone has a 5th submesh (Material.003) with no
        // counterpart on any other shield - reads as its own bonus decorative bit, not worth special
        // casing given it just gets tinted with that shield's own pattern texture like its main one.
        //
        // Same diagnostic measured Shield 1's raw mesh: bounds center (0, 0, 0.00327), size (0.04748,
        // 0.04748, 0.00953) - thin along local Z exactly like the OldStyle disc (see ShieldNativeDiameter's
        // note above), so the same ShieldMountRotation/HeldShieldLocalRotation both apply unchanged.
        // The FBX bakes a x100 scale onto each "Shield N" child's own transform, but since we always
        // REPLACE localScale wholesale (never compose with the source's), that baked 100 is irrelevant
        // here - Pack2ShieldNativeDiameter below is the raw mesh bounds size directly, same convention
        // as ShieldNativeDiameter for the OldStyle family.
        private const int Pack2VariantCount = 8;
        private const float Pack2ShieldNativeDiameter = 0.0475f;
        private const int ShieldVariantCount = OldStyleVariantCount + Pack2VariantCount;

        // OldStyle occupies indices 0..OldStyleVariantCount-1, Pack2 the rest - see
        // CreateShieldVisual's dispatch. (A temporary swap put Pack2 first for easier dev-injected
        // testing while confirming its on-device look - reverted 2026-09-28 once that was confirmed
        // and variant selection became random anyway, see SpawnHeldShield.)
        private static float NativeDiameterFor(int variantIndex) =>
            variantIndex < OldStyleVariantCount ? ShieldNativeDiameter : Pack2ShieldNativeDiameter;

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
        // X nudged out from the decal's own 0.52 (human report 2026-09-28, with a close-up
        // screenshot: "ta puvodni 'prosvita' temi, co jsou mezi okem pozorovatele a tou puvodni" -
        // the original 'shows through' the ones that are between the viewer's eye and it - and "musis
        // to na z-ose priblizit trochu vic k mym ocim" - move it closer to my eyes on that axis).
        // Mounting the reward shield at the exact same X as the decal it sits on put both surfaces
        // exactly coplanar from the camera's point of view - classic z-fighting, two overlapping faces
        // flickering between each other rather than one cleanly occluding the other.
        //
        // First fix pushed X out to 0.60 (+0.08) - reasonable relative to the shield's size at the
        // time (0.22 diameter, 0.11 radius), but MountedShieldTargetDiameter got cut hard in that same
        // round (down to 0.16, radius 0.08) without revisiting this offset - so +0.08 had gone from
        // "most of a radius" to "a full radius", enough to visibly float the reward shield off the
        // hull as its own separate thing (human report 2026-09-28: "uplne ses na tu puvodni netrefil"
        // - you completely missed hitting the original). Second fix (0.55, i.e. +0.03) was closer but
        // still visibly off (human report the same round, another close-up: "porad to tam neni
        // presne" - still not exactly there).
        //
        // Rather than guess a third offset, this finally measured the decal's own geometry directly:
        // an InspectShieldDecalCluster diagnostic clustered the SAME per-triangle colour-matched
        // vertices already proven correct (they are what RemoveDefaultShieldDecoration used to
        // successfully strip the decal's colour, before that whole approach got reverted - see
        // BuildEnvironment's history) by proximity, rather than averaging all of them together (that
        // average was meaningless - the colour match also catches scattered rivets/hardware
        // elsewhere on the hull, see the numbers this same diagnostic reported at other Z values).
        // The two tight, matching clusters (one per side, ~288 vertices each, a plausible full disc)
        // sit at recentred (+-0.52, -0.82, 0.20) with extents (0.02, 0.08, 0.08) - i.e. the decal is
        // almost perfectly flush against the hull (only 0.04 total depth) and about 0.16 in diameter,
        // which is exactly MountedShieldTargetDiameter already. Y was off by 0.02 (-0.8 vs the real
        // -0.82) and, now that the decal's own tiny 0.02 half-thickness is known precisely, the X
        // offset needed to clear it is far smaller than either previous guess - just past the decal's
        // own outer surface (0.52 + 0.02) plus a hair of margin. This fixed X only holds AT the
        // decal's own Z, though - see HullSideProfile below for why every other position in the row
        // now needs its own X instead of reusing this constant.
        private const float ShieldRailY = -0.81f;
        // Everything above this line about spreading shields along a rail (a half-span, a minimum
        // spacing, a slot grid, a centre-exclusion radius to stay clear of the original) chased a
        // moving target across several rounds of human feedback and never actually landed - most
        // recently, 2026-09-28: "porad blbe! A puvodni stit jsi vubec mazat nemel! Zkusme to jinak.
        // Vrat ten puvodni stit a pak vsechny nahradni stity umistuj presne na nej" (still bad! And
        // you should never have deleted the original shield at all! Let's try it differently: bring
        // the original shield back, then mount every reward shield exactly on it). So:
        // RemoveDefaultShieldDecoration is gone (see BuildEnvironment - the hull's own decal is left
        // completely untouched again) and every reward shield now targets the SAME single spot, right
        // on top of that decal, rather than a spread of distinct positions - see AttachShieldRoutine's
        // replace-in-place logic below.
        //
        // 0.20 exactly matched the decal's own measured Z (see ShieldRailX's note), but still read as
        // shifted from the human's viewpoint (2026-09-28, screenshot: "musis to dat vice doleva! (z
        // meho pohledu)" - you need to put it more to the left, from my point of view). The flythrough
        // camera's screen-right axis (worldUp x forward, with the camera looking mostly along -X with
        // a +Z lean toward the direction of travel - see the flythrough comment above) points mostly
        // along +Z with a smaller +X component, so the ShieldRailX outward push needed for z-fighting
        // (see above) also drags the shield toward screen-right as a side effect of moving toward the
        // camera along a not-quite-perpendicular sightline. Nudged Z down to compensate and pull it
        // back toward screen-left.
        //
        // 0.15 overshot the other way (human report the same round: "nasazeny stit je moc vlevo od
        // spravneho cile" - the mounted shield is now too far left of the correct target) - confirms
        // the direction of the fix was right, just too large a step. Split the difference between the
        // decal's own measured 0.20 (read as too far right) and 0.15 (too far left). Human then
        // fine-tuned this value directly (own build, see the note above AttachShieldRoutine) rather
        // than going another round through me - left as whatever is currently checked in.
        private const float ShieldRailZ = 0.185f;

        // Row layout pivoted several times across 2026-09-28. First: "Ted chci, abys stity
        // nepokladal na ten stary stit, ale kousek nalevo a kousek napravo..." (don't mount on the
        // original, put shields a bit left and a bit right) - piled up in two spots, then flattened
        // into "ted zrusime kupicky... rada stitu" (cancel the piles, alternating left/right growing
        // row instead). THAT in turn got replaced - see AttachShieldRoutine's note - by a single
        // bow-to-stern row per the next human request, once the two-directional row turned out to be
        // hiding a real bug (mounted shields drifting off the hull's actual surface away from
        // midship, not just running out of room).
        //
        // Gap sized off the two things that have to clear each other: the decal's own ~0.16 diameter
        // and this shield's matching 0.16 (MountedShieldTargetDiameter) - tightened from 0.20 to 0.17
        // per human request 2026-09-28 ("pro jistotu trochu zmensi gap mezi prilepenymi stity" - just
        // to be safe, shrink the gap between the mounted shields a bit). Edges of two adjacent discs
        // still clear each other (~0.01 apart), just tighter than the original 0.04.
        private const float ShieldSlotGap = 0.17f;

        // The hull's side wall is NOT a flat plane at a constant X - it's widest at midship (where
        // the original decal sits, ShieldRailZ) and tapers down toward both the bow and stern before
        // curling up into the ornamental prow at each end. An InspectHullSideProfile Editor
        // diagnostic (deleted after use) sampled the hull's outer-most vertex X at 0.1-unit Z slices,
        // restricted to a +-0.15 band around ShieldRailY (mount height) - i.e. "how far out does the
        // hull wall reach at shield height, at this point along its length". Table below is that
        // measurement verbatim, with the Z=0.1/0.2 entries dropped (they sampled the decal's own
        // raised geometry, not the hull wall itself, and would otherwise poke a false spike into the
        // interpolation right where the row's skipped slot sits anyway). Root cause found this way
        // for human report 2026-09-28 ("Ta lod nam ujizdi" - the ship is sailing away from us): every
        // shield away from midship was mounted at the SAME fixed X as the one at midship, so it
        // increasingly floated off the real, narrower hull surface the further it sat from centre -
        // reading as the ship's side curving away underneath a row of shields stuck in one flat plane.
        private static readonly (float z, float x)[] HullSideProfile =
        {
            (-2.0f, 0.248f), (-1.9f, 0.227f), (-1.7f, 0.149f), (-1.6f, 0.120f), (-1.5f, 0.142f),
            (-1.4f, 0.173f), (-1.3f, 0.215f), (-1.2f, 0.266f), (-1.1f, 0.288f), (-1.0f, 0.323f),
            (-0.9f, 0.379f), (-0.8f, 0.376f), (-0.7f, 0.400f), (-0.6f, 0.428f), (-0.5f, 0.440f),
            (-0.4f, 0.463f), (-0.3f, 0.468f), (-0.2f, 0.540f), (-0.1f, 0.487f), (0.0f, 0.504f),
            (0.3f, 0.524f), (0.4f, 0.504f), (0.5f, 0.487f), (0.6f, 0.487f), (0.7f, 0.468f),
            (0.8f, 0.463f), (0.9f, 0.440f), (1.0f, 0.428f), (1.1f, 0.400f), (1.2f, 0.376f),
            (1.3f, 0.379f), (1.4f, 0.323f), (1.5f, 0.288f), (1.6f, 0.266f), (1.7f, 0.215f),
            (1.8f, 0.173f), (1.9f, 0.142f), (2.0f, 0.120f),
        };

        // Same clearance the original ShieldRailX used past the decal's own measured surface (0.545
        // - 0.52) - kept here so every mounted shield sits just proud of the hull instead of
        // z-fighting with it, same reasoning, now applied at whatever X the hull actually has at
        // that shield's own Z instead of only at the one Z that was ever measured directly.
        private const float HullClearance = 0.025f;

        // Linear interpolation over HullSideProfile; clamps to the nearest measured end outside the
        // table's own range (Z +-2.0, matching the note above - beyond that the hull curls out of
        // the mount-height band the diagnostic sampled, i.e. exactly where the row should stop).
        private static float HullOuterXAtZ(float z)
        {
            if (z <= HullSideProfile[0].z) return HullSideProfile[0].x + HullClearance;
            for (int i = 1; i < HullSideProfile.Length; i++)
            {
                if (z <= HullSideProfile[i].z)
                {
                    var (z0, x0) = HullSideProfile[i - 1];
                    var (z1, x1) = HullSideProfile[i];
                    float t = (z - z0) / (z1 - z0);
                    return Mathf.Lerp(x0, x1, t) + HullClearance;
                }
            }
            return HullSideProfile[^1].x + HullClearance;
        }

        // Row runs bow (-Z) to stern (+Z) per human request 2026-09-28 ("sazet ty stity musis od
        // zacatku lodi (jeji prid) a kazdy dalsi jde do prava smerem k zadi" - you have to plant the
        // shields starting from the front of the ship, its bow, and each next one goes right toward
        // the stern), replacing the earlier centre-outward alternating row - see ShieldSlotGap's
        // note. Bow-at-negative-Z matches the flythrough's own direction convention (see its own
        // note) and the human's "doprava" (rightward) cue: the established screen-axis mapping for
        // this camera has screen-right pointing mostly along +Z.
        //
        // The row must include the ORIGINAL decal as one of its own evenly-spaced members (human
        // report 2026-09-28: "Musi ti to vyjit tak, aby na konci vsechny stity tvorily nadhernou
        // radu od sebe stejne vzdalenych stitu - vcetne toho PUVODNIHO" - it has to work out so
        // that in the end all the shields form a beautiful row of equally-spaced shields, INCLUDING
        // the original one). Picking ShieldRowStartZ as its own independent number and separately
        // rounding to find which slot the original happens to land nearest (the previous approach)
        // can't guarantee that - the two numbers only lined up by coincidence, and each of the two
        // rounds of feedback below nudged this constant on its own without re-deriving it from
        // ShieldRailZ, so the alignment silently broke both times. Defined the other way around
        // instead, below: the original's own precisely-measured Z (ShieldRailZ) IS one of the row's
        // slots BY CONSTRUCTION, and SlotsBeforeOriginal counts back from it to find the row's
        // start - the two can no longer drift apart.
        //
        // First report ("Prvni 2 stity jsou uplne mimo lod! Dej prvni tam, kde je momentalne az
        // ctvrty" - the first 2 shields are completely off the ship, put the first one where the
        // fourth currently is): the original -1.95 start sat right at HullSideProfile's own measured
        // extent, which turned out not to be reliable mount surface that close to the very tip -
        // likely thin/sparse geometry from the curled bow decoration itself (see HullSideProfile's
        // own note on the unexpected uptick right at Z=-2.0) rather than a real side wall. Second
        // report the same day ("zacni jeste vice vpravo, tak do (puvodne) 6 pozice" - start even
        // further right, at the [original] 6th position): moved forward again, landing on
        // SlotsBeforeOriginal = 8 (measured back from ShieldRailZ instead of forward from a
        // since-abandoned start point, so it lands exactly on a real slot). Third report, once the
        // exact-alignment fix above made the row actually look right for the first time ("Nadhera.
        // Zkus zacit o jeden slot vpravo" - gorgeous, try starting one slot further right): one
        // more step, 8 -> 7. Fourth report, after the ship-sailing rework further down changed how
        // this whole row reads on screen ("Opet posun ty sloty vice do pride lodi, tak o 3 sloty" -
        // again, shift those slots more toward the bow, by about 3 slots): back out again, 7 -> 10.
        // No equivalent report yet on the stern end, so that side is untouched - see HullOuterXAtZ's
        // clamp for what happens if the row ever reaches that far.
        private const int SlotsBeforeOriginal = 10;
        private static readonly float ShieldRowStartZ = ShieldRailZ - SlotsBeforeOriginal * ShieldSlotGap;

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
        private GameObject _shieldPack2Asset;
        private Material _pack2SteelMaterial;
        private Material _pack2WoodMaterial;
        private Material[] _pack2PatternMaterials;
        private Transform _heldShield;
        private float _heldShieldNativeDiameter;
        // Just a spawn counter for unique GameObject names now - which variant shows up comes from
        // _variantBag (see SpawnHeldShield), not derived from this.
        private int _shieldsSpawnedCount;

        // Shuffle-bag for variant selection: "random" per human request 2026-09-28 must still not
        // repeat a design until every one of the 15 has appeared once ("at se ty same stity
        // neopakuji... minimalne ne dokud nedojdou unikatni designy" - the same shields shouldn't
        // repeat, at least not until we run out of unique designs) - plain Random.Range per spawn
        // (the first pass at "random") can't guarantee that, since nothing stops it drawing the same
        // index twice in a row. Refilled with a freshly shuffled 0..ShieldVariantCount-1 run whenever
        // it empties out.
        private readonly List<int> _variantBag = new();

        private void BuildShieldRewardAssets()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            _shieldAsset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShield");
            if (_shieldAsset == null)
                Debug.LogError("[VikingBoat] Could not load VikingShield model from Resources.");

            _shieldMaterials = new Material[OldStyleVariantCount];
            for (int i = 0; i < OldStyleVariantCount; i++)
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

            _shieldPack2Asset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShieldPack2");
            if (_shieldPack2Asset == null)
                Debug.LogError("[VikingBoat] Could not load VikingShieldPack2 model from Resources.");

            // No normal maps came with this pack's Texture.rar (just BaseColor-equivalent flats), so
            // these are plain untextured-normal materials - flatter-looking than the OldStyle family,
            // an acceptable trade rather than fabricating fake normal data.
            _pack2SteelMaterial = new Material(shader) { mainTexture = Resources.Load<Texture2D>("Worlds/VikingBoat/Textures/Shields/Pack2_Steel") };
            _pack2WoodMaterial = new Material(shader) { mainTexture = Resources.Load<Texture2D>("Worlds/VikingBoat/Textures/Shields/Pack2_Wood") };
            _pack2PatternMaterials = new Material[Pack2VariantCount];
            for (int i = 0; i < Pack2VariantCount; i++)
                _pack2PatternMaterials[i] = new Material(shader) { mainTexture = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/Shields/Pack2_{i + 1}") };

            SpawnHeldShield();
        }

        // Builds one shield's visual - either an OldStyle instance (shared mesh, one swapped
        // material) or a Pack2 instance (that variant's own complete "Shield N" child, its submeshes
        // remapped by FBX material-slot name onto the shared Pack2 materials plus that variant's
        // pattern material.
        //
        // Two earlier guesses at this mapping were both wrong, corrected only by actually looking at
        // a zoomed-in screenshot (human prompt 2026-09-28: "Vidis tam ty barevny tecky? Zkus to
        // nejdriv interpretovat" - do you see those colored dots? try to interpret it first) rather
        // than continuing to guess from triangle counts/UV boxes alone:
        //   1. First guess: shared slot name = shared material, the one odd name out per shield = the
        //      pattern. Human report: shields render all-wood, color completely missing - the pattern
        //      landed on a ~30-tri submesh (the boss dome), too small to notice.
        //   2. Second guess, from a triangle-count/UV diagnostic: "Material.001" (the biggest submesh,
        //      near-full 0-1 UV) is the front face and needs the pattern. Human report: colored dots
        //      visible only around the rim and boss, in a different color per shield. Zooming into a
        //      screenshot showed why - those "dots" are the individual rivet studs, and each one is
        //      rendering a tiny complete copy of the whole checkered pattern image. "Material.001" is
        //      the rivet ring (many small studs, each independently mapped 0-1, hence both the high
        //      combined triangle count and the full UV range), not the face.
        // The actual big flat painted face - confirmed by opening Pack2_1.png directly, which is a
        // full circular checkered graphic - is "Material.004": a small triangle count (~74) is normal
        // for a simple flat fan-triangulated disc, and it never changed appearance across any of the
        // above guesses (always wood), which is exactly why the pattern always looked "missing": the
        // one submesh actually meant to show it was hardcoded away from it the whole time.
        // Everything else (the rim, the rivets, the boss dome) is a fixed steel look now - real
        // Viking shields have a painted wooden face inside a plain metal fitting, which also matches
        // what's on screen. No per-submesh z-fighting workaround is needed for the face itself since
        // nothing else in the mesh occupies that same surface. Caller is responsible for the
        // resulting object's local position/rotation/scale.
        private GameObject CreateShieldVisual(int variantIndex)
        {
            // OldStyle occupies indices 0..OldStyleVariantCount-1, Pack2 the rest - see
            // NativeDiameterFor's matching note.
            if (variantIndex < OldStyleVariantCount)
            {
                int oldStyleIndex = variantIndex;
                var instance = Instantiate(_shieldAsset);
                var renderer = instance.GetComponentInChildren<Renderer>();
                if (renderer != null) renderer.sharedMaterial = _shieldMaterials[oldStyleIndex];
                return instance;
            }
            else
            {
                int pack2Index = variantIndex - OldStyleVariantCount;
                var source = _shieldPack2Asset.transform.Find($"Shield {pack2Index + 1}");
                var instance = Instantiate(source.gameObject);
                var renderer = instance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var sourceMats = renderer.sharedMaterials;
                    var newMats = new Material[sourceMats.Length];
                    for (int i = 0; i < sourceMats.Length; i++)
                    {
                        string name = sourceMats[i] != null ? sourceMats[i].name : "";
                        newMats[i] = name switch
                        {
                            "Material.004" => _pack2PatternMaterials[pack2Index],
                            _ => _pack2SteelMaterial,
                        };
                    }
                    renderer.sharedMaterials = newMats;
                }
                return instance;
            }
        }

        private void SpawnHeldShield()
        {
            // No cap here any more - every reward shield replaces the last one at the single shared
            // mount spot (see AttachShieldRoutine), so there is no "ran out of room on the rail" case
            // left to guard against. Variant drawn from the shuffle-bag below - see _variantBag.
            if (_shieldAsset == null || _shieldPack2Asset == null) return;

            int variantIndex = DrawNextVariant();
            var instance = CreateShieldVisual(variantIndex);
            instance.name = $"HeldShield_{_shieldsSpawnedCount}";
            instance.transform.SetParent(_camera.transform, false);
            instance.transform.localPosition = HeldShieldLocalPosition;
            instance.transform.localRotation = HeldShieldLocalRotation;
            instance.transform.localScale = Vector3.one * (HeldShieldTargetDiameter / NativeDiameterFor(variantIndex));
            SetLayerRecursively(instance, HeldShieldLayer);

            _heldShield = instance.transform;
            _heldShieldNativeDiameter = NativeDiameterFor(variantIndex);
            _shieldsSpawnedCount++;
        }

        // Draws one variant index from _variantBag, refilling it with a freshly shuffled full run
        // of 0..ShieldVariantCount-1 whenever it's empty - see _variantBag's note on why plain
        // Random.Range per draw isn't enough.
        private int DrawNextVariant()
        {
            if (_variantBag.Count == 0)
            {
                for (int i = 0; i < ShieldVariantCount; i++)
                    _variantBag.Add(i);
                for (int i = _variantBag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (_variantBag[i], _variantBag[j]) = (_variantBag[j], _variantBag[i]);
                }
            }

            int lastIndex = _variantBag.Count - 1;
            int variantIndex = _variantBag[lastIndex];
            _variantBag.RemoveAt(lastIndex);
            return variantIndex;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        // Sequential arrival count, bow to stern - see ShieldRowStartZ's note. The slot that must be
        // skipped (it belongs to the original decal, already sitting there) is SlotsBeforeOriginal
        // by construction - no rounding needed, unlike before.
        private int _totalMounted;

        private IEnumerator AttachShieldRoutine()
        {
            if (_heldShield == null)
            {
                RaiseListeningSafe();
                yield break;
            }

            Transform shield = _heldShield;
            float nativeDiameter = _heldShieldNativeDiameter;
            _heldShield = null;

            // slots 0..SlotsBeforeOriginal-1 land bow-ward of the original (smaller Z); slot
            // SlotsBeforeOriginal itself is the original's own position (never assigned here); every
            // slot after that lands stern-ward of it (larger Z) - see ShieldRowStartZ's derivation.
            int slot = _totalMounted < SlotsBeforeOriginal ? _totalMounted : _totalMounted + 1;
            _totalMounted++;

            float targetZ = ShieldRowStartZ + slot * ShieldSlotGap;
            Vector3 targetPosition = new(HullOuterXAtZ(targetZ), ShieldRailY, targetZ);

            // Detach from the camera, keeping its current world pose as the flight's start point.
            // Reset off the held-shield overlay layer back to Default (0) at the same time - once
            // mounted this is a normal world object again and must be depth-tested like everything
            // else, not forced in front of the ship it is about to land on. Parented to _shipPivot
            // (the ship's own moving root), not the stationary world `transform` - see Update()'s
            // note on why the ship now actually travels: a shield parented to the wrong one would
            // get left behind in open water the moment the hull moved out from under it.
            shield.SetParent(_shipPivot, true);
            SetLayerRecursively(shield.gameObject, 0);
            Vector3 startPosition = shield.localPosition;
            Quaternion startRotation = shield.localRotation;
            Vector3 startScale = shield.localScale;
            Vector3 targetScale = Vector3.one * (MountedShieldTargetDiameter / nativeDiameter);

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
            _shipPivot = pivot;

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
