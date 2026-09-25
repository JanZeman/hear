using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell.UI
{
    /// <summary>
    /// Builds and drives the entire HEAR shell chrome. Rewritten per the hear-ui-assets-v0.3
    /// upgrade brief: the World Selector is now a hero-dominant "gallery" (real world art +
    /// Companion + ambient crossfade) rather than a flat card-grid dashboard, and navigation is
    /// deliberately quiet/subordinate to the world. Everything is constructed procedurally in
    /// code (no hand-authored UXML/USS), consistent with this project's existing convention.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShellUIController : MonoBehaviour
    {
        private UIDocument _document;
        private GameFlowController _flow;

        private VisualElement _root;
        private VisualElement _screenLayer;
        private VisualElement _navHost;
        // Fills the safe-area gesture-inset gap below the compact-overlay glass nav panel with
        // the same colour, so the bar reads as one continuous panel reaching the true screen edge
        // regardless of whether the OS's own nav bar happens to be shown or auto-hidden at that
        // moment - see ApplyBreakpointLayout. Human feedback 2026-09-25: with the gap left bare,
        // it looked fine only when the OS bar happened to be visible and filled it; empty
        // otherwise ("je-li schovany, tak tam neni 'nic' a to vypada blbe").
        private VisualElement _navBackdropExtension;
        private ResponsiveNavBar _nav;
        private Label _hudProgressLabel;
        private VisualElement _hudProgressFill;
        private ShellBreakpoint _lastBreakpoint = (ShellBreakpoint)(-1);

        // Ambient background crossfade (world selector only): _ambientBack always shows the last
        // settled world's ambient art at full opacity; _ambientFront fades 0->1 with the newly
        // selected world's art, then is "baked" into _ambientBack so the next transition starts clean.
        private VisualElement _ambientBack;
        private VisualElement _ambientFront;
        private Coroutine _ambientAnim;
        // Tracks identity against the untreated source texture - see RefreshHomeBackground.
        private Texture2D _lastAmbientSourceTex;

        // Home / World Selector carousel live references, updated in place on resize instead of a
        // full rebuild. Rebuilt per hear-home-screen-handoff-v1.0: a real carousel (one active
        // card, two peeking neighbors) rather than a hero panel + separate thumbnail row.
        private VisualElement _carousel;
        private VisualElement _activeCard;
        private VisualElement _prevCard;
        private VisualElement _nextCard;
        private float _swipeStartX;
        private bool _swipeTracking;
        private bool _swipeCaptured;
        private const float SwipeThresholdPx = 40f;
        private const float SwipeCaptureThresholdPx = 12f;
        private readonly List<VisualElement> _worldDots = new();
        private Coroutine _carouselEntranceAnim;
        private VisualElement _companion;
        private VisualElement _companionShadow;
        private Button _playButton;
        // The GOLDEN board's Play pill itself has NO glow (measured across its left edge on
        // sources/HEAR-App-UI-Home.png: a razor-sharp edge, no halo) - re-added anyway on human
        // direction 2026-09-25 as a deliberate deviation. History: max 6px/peak 0.05 invisible;
        // max 10px/peak 0.12 "moc viditelne"; max 7px/peak 0.07 (x2) still invisible; max 11px/
        // peak 0.10 finally visible, BUT now called "moc rozplizly"/"tlusty pruh" - a wide soft
        // band, not the "jen tenky prouzek, par pixelu" wanted. The lesson across all five rounds:
        // spread (padding) and strength (alpha) are independent knobs, and matching feedback to
        // the wrong one is what produced this many rounds. "Thin but visible" means BOTH small
        // padding AND enough alpha at once, not a trade-off between them - previous rounds each
        // only moved one axis.
        // Round 6 (2026-09-25): round 5's (pad 3, alpha 0.16) was verified only via a digitally
        // cropped/zoomed screenshot, which exaggerates a few-percent alpha edge into visibility -
        // on the real device at normal size (and further softened by scrcpy's video compression)
        // human feedback was that it was invisible again. Keeps the narrow pad-3 spread's small
        // physical footprint isolated - the "tlusty pruh" this whole tuning problem chases came
        // from spread, not alpha - but roughly doubles peak alpha so the ring survives real
        // on-screen viewing and lossy mirroring, not just a zoomed still. Confirmed on the human's
        // own device/scrcpy view as visible and roughly right - round 7 is a small width-only
        // trim ("nepatrne uzsi"), alpha untouched so it does not slip back toward invisible.
        private VisualElement[] _playGlowLayers;
        private const int PlayGlowLayerCount = 10;
        private const float PlayGlowMaxPadding = 3.3f;
        private const float PlayGlowPeakAlpha = 0.32f;
        private const float PlayGlowFalloffRate = 2.6f;
        private VisualElement _playRow;
        private VisualElement _bottomSpacer;
        private VisualElement _brandBlock;
        private Image _brandLogo;
        private Label _brandClaim;
        private VisualElement _dotsRow;
        private VisualElement _navLogo;
        private WorldArt.HomeBgAspect _lastHomeAspect = (WorldArt.HomeBgAspect)(-1);
        private string _lastHomeWorldId;

        /// <summary>
        /// Every Home-screen proportion, taken by direct pixel measurement of the GOLDEN board
        /// `sources/HEAR-App-UI-Home.png` (852x1846) on 2026-09-25 and stored as a ratio of a
        /// stable in-image reference, never as a raw pixel count - the mockup and any real device
        /// differ in both resolution and aspect ratio, so only ratios transfer. Horizontal sizes
        /// are fractions of screen width, vertical positions fractions of screen height, and a
        /// few sizes are fractions of the element they must stay proportional to (card radius of
        /// card width, dot diameter of Play width). Raw measurements behind each value are in
        /// `.agents/roadmap/001-home-screen-visual-parity.md`.
        /// </summary>
        private static class Golden
        {
            // Brand block: logo top y=144, wordmark bottom y=325, claim band y=351..381.
            public const float BrandTopOfScreenH = 0.078f;
            // Both re-derived from an on-device measurement after the first pass (the supplied
            // logo bitmap does not carry the GOLDEN board's own wordmark proportions, so the
            // element size that lands the wordmark on 0.277 of screen width has to be measured,
            // not computed from the texture): wordmark came out at 0.322 with 0.457 here, and the
            // claim at 0.383 against the board's 0.358.
            public const float BrandLogoOfScreenW = 0.393f;
            // Enlarged ("vetsi pismo, at je citelne") and re-anchored on human direction
            // 2026-09-25. ClaimTopOfScreenH (an absolute position measured against the GOLDEN
            // board's 852x1846/0.462 aspect) is gone: on this device's much taller 968x2376/0.407
            // aspect, screenH * 0.1901 landed far below the logo instead of hugging it - the same
            // aspect-mismatch bug already fixed once for the nav bar (see this file's notes). The
            // claim now sits a fixed small gap under the logo, full stop, matching the sketch's
            // "claim directly under the logo" placement regardless of device aspect.
            // Dialed back on human direction 2026-09-25: 0.048 read as "moc velkymi pismeny".
            public const float ClaimFontOfScreenW = 0.040f;
            public const float ClaimGapOfScreenH = 0.0141f;

            // Active card: x=184..670, y=447..1185. The board measured a card top at 0.2422 of
            // screen height, but that absolute anchor broke on this device's much taller aspect
            // (see UpdateCarouselForCurrentSize's cardTopBase) - removed 2026-09-25, kept only as
            // this historical measurement.
            public const float CardWidthOfScreenW = 0.5716f;
            public const float CardHeightOverWidth = 1.517f;
            public const float CardRadiusOfCardW = 0.082f;
            public const float CardBorderOfCardW = 0.0051f;
            public const float CardBorderAlpha = 0.7f;
            public const float CardTitleCapOfCardW = 0.0431f;
            public const float CardTitleWidthOfCardW = 0.680f;
            // The board's world name is set very airily: 331px of line for a 21px cap height,
            // and the glyph size already matches, so tracking is what has to carry the extra
            // width. CAREFUL WITH THE UNIT: two on-device measurements (0.22em -> 0.50 of card
            // width, 0.46em -> 0.52) show this Unity version's `letterSpacing` behaving as
            // hundredths of an em, not logical px - a nominal 2.8 only bought 0.36px of gap. So
            // this value is em/100 and, unlike a px value, must NOT be scaled by the font size.
            public const float CardTitleTrackingEmHundredths = 27.5f;
            public const float CardPaddingOfCardW = 0.060f;

            // Peek cards: y=523..1068 (0.739 of the active card's height), inner edge 21px from
            // the active card, same silhouette aspect as the active card. No label (approved
            // deviation from the GOLDEN board, human direction 2026-09-25).
            public const float PeekHeightOfActiveH = 0.739f;
            public const float PeekGapOfScreenW = 0.0235f;
            public const float PeekCenterRiseOfActiveH = 0.028f;

            // Play pill: x=245..606, y=1280..1375, fully rounded. Enlarged ~15% on human
            // direction 2026-09-25 ("Play tlacitko by melo byt vetsi"), aspect unchanged.
            public const float PlayWidthOfScreenW = 0.49f;
            public const float PlayHeightOverWidth = 0.2652f;
            public const float PlayFontOfPlayW = 0.115f;

            // Dots: three 20px circles, 43.5px apart, centred on the screen; card bottom 1185 ->
            // dots top 1213, dots bottom 1235 -> Play top 1280.
            public const float DotDiameterOfPlayW = 0.0552f;
            public const float DotPitchOfPlayW = 0.1202f;
            public const float CardToDotsOfScreenH = 0.0152f;
            public const float DotsToPlayOfScreenH = 0.0244f;

            // Bottom nav: icon 51px wide, cell centre y=1693.
            public const float NavIconOfScreenW = 0.0599f;
            // NavCenterOfScreenH (0.9171) removed 2026-09-25: matching the GOLDEN board's nav
            // position left a large dead gap below the panel on this device (human feedback,
            // "kolik prazdneho prostoru tam v takove situaci je") - nav position now derives from
            // the real safe-area inset instead (see ApplyBreakpointLayout).
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null)
                _document.panelSettings = CreateRuntimePanelSettings();

            BuildStaticLayout();
        }

        private void Start()
        {
            _flow = GameFlowController.Instance;
            _flow.StateChanged += OnStateChanged;
            _flow.SelectedWorldIndexChanged += OnSelectedWorldChanged;
            OnStateChanged(_flow.State);
        }

        private void OnDestroy()
        {
            if (_flow == null) return;
            _flow.StateChanged -= OnStateChanged;
            _flow.SelectedWorldIndexChanged -= OnSelectedWorldChanged;
        }

        private void Update()
        {
            if (_flow == null) return;

            if (_flow.State == GameFlowController.ShellState.Playing)
                UpdateHud();

            if (_flow.State == GameFlowController.ShellState.WorldSelector)
                UpdateCarouselForCurrentSize();

            ApplyBreakpointLayout();
        }

        private void ApplyBreakpointLayout()
        {
            if (_nav == null) return;

            float width = _root.resolvedStyle.width;
            var breakpoint = ResponsiveNavBar.BreakpointForWidth(width);
            _nav.Apply(width);

            bool compact = breakpoint == ShellBreakpoint.Compact;
            bool onHome = _flow != null && _flow.State == GameFlowController.ShellState.WorldSelector;

            // Compact Home must float the bottom nav as a translucent overlay on top of the
            // full-bleed world art rather than reserving its own opaque flex row underneath it -
            // otherwise the "overlay" tint in ResponsiveNavBar just blends into _root's own plain
            // background instead of the scene, per docs/00-home-design-freeze.md section 5.
            bool navOverlay = compact && onHome;
            _navHost.style.position = navOverlay ? Position.Absolute : Position.Relative;
            _navHost.style.left = navOverlay ? 0 : new StyleLength(StyleKeyword.Auto);
            _navHost.style.right = navOverlay ? 0 : new StyleLength(StyleKeyword.Auto);
            // Previously the GOLDEN board's nav row position (centred at 0.917 of screen height)
            // was matched via `Mathf.Max(safeArea, goldenBoardOffset)` - on this device the
            // Golden-board term won that max (~47 logical units) against the real safe-area inset,
            // leaving a large dead gap below the panel that read as wasted/empty space regardless
            // of the backdrop-extension's colour fix, per human feedback 2026-09-25 ("kolik
            // prazdneho prostoru tam v takove situaci je"). Dropped the Golden-board term entirely:
            // the panel now sits as close to the true bottom as the real OS safe area allows, plus
            // a small fixed breathing margin, rather than an aesthetic offset tuned to a static
            // reference mockup that this device's proportions don't match.
            float navBottom = GetBottomSafeAreaInsetLogical() + 4f;
            _navHost.style.bottom = navOverlay ? navBottom : new StyleLength(StyleKeyword.Auto);

            // See _navBackdropExtension's declaration comment. Spills below _navHost's own box
            // (which only spans the 56-tall icon row) via a negative bottom offset, filling the
            // reserved gesture-safe-area gap down to the true screen edge with the same glass
            // colour, so it reads as one continuous panel whether or not the OS's own nav bar
            // happens to be visible right now.
            if (navOverlay && navBottom > 0.5f)
            {
                _navBackdropExtension.style.display = DisplayStyle.Flex;
                _navBackdropExtension.style.bottom = -navBottom;
                _navBackdropExtension.style.height = navBottom;
                _navBackdropExtension.style.backgroundColor = ResponsiveNavBar.GlassBackgroundColor;
            }
            else
            {
                _navBackdropExtension.style.display = DisplayStyle.None;
            }

            if (breakpoint == _lastBreakpoint) return;
            _lastBreakpoint = breakpoint;

            _root.style.flexDirection = compact ? FlexDirection.Column : FlexDirection.Row;

            // The quiet brand mark belongs above a sidebar/rail, not repeated above a bottom nav
            // bar (confirmed looking wrong - a stray logo floating in blank space - on a real
            // folded-phone screenshot in Compact mode), and never on Home (see OnStateChanged).
            if (_navLogo != null && _flow != null)
            {
                _navLogo.style.display = (compact || onHome) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            _navHost.RemoveFromHierarchy();
            _screenLayer.RemoveFromHierarchy();
            if (compact)
            {
                _root.Add(_screenLayer);
                _root.Add(_navHost);
            }
            else
            {
                _root.Add(_navHost);
                _root.Add(_screenLayer);
            }
        }

        private static PanelSettings CreateRuntimePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            // ConstantPixelSize (1 UI unit = 1 physical pixel) made every phone look "wide" to our
            // breakpoint logic, since a narrow phone screen is still hundreds of physical pixels
            // at typical mobile DPI - confirmed on a real folded Galaxy Z Fold, which stayed in the
            // Medium icon-rail layout instead of Compact bottom-nav despite a visually narrow
            // screen. ConstantPhysicalSize divides by the real device DPI (via referenceDpi), so a
            // UI unit means roughly the same physical size (and our 600/1000 breakpoint tokens mean
            // the same thing) on desktop and mobile alike. fallbackDpi covers devices/emulators that
            // report Screen.dpi == 0.
            settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            // referenceDpi 160 (not 96) makes one UI Toolkit logical unit equal one Android
            // density-independent pixel. At 96 this device's 968-physical-px screen resolved to a
            // 221-unit-wide panel, so every token expressed in "logical px" (16px body text, 26px
            // nav icon, 32px radius) rendered roughly 1.67x too large relative to the screen -
            // measured on-device 2026-09-25: wordmark 0.435 of screen width vs. 0.277 in
            // sources/HEAR-App-UI-Home.png, nav icon 0.095 vs. 0.060. 160 puts the panel at 369
            // units here, the dp space every size token in VisualTokens was written for, and keeps
            // the 600/960 breakpoints meaning what home-layout.json says they mean.
            settings.referenceDpi = 160f;
            settings.fallbackDpi = 160f;
            return settings;
        }

        private void BuildStaticLayout()
        {
            _root = _document.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.style.flexDirection = FlexDirection.Row;
            _root.style.backgroundColor = VisualTokens.Colors.Pearl0;

            _navHost = new VisualElement { style = { flexShrink = 0 } };

            // Small quiet mark at the top of the nav - the world selector itself carries no logo
            // now (world art must dominate that screen); this is the shell's only persistent
            // identity element, per "shell is consistent, worlds are free".
            _navLogo = BrandMarkView.CreateMarkOnly(56);
            _navLogo.style.marginLeft = VisualTokens.Spacing.L;
            _navLogo.style.marginTop = VisualTokens.Spacing.L;
            _navLogo.style.marginBottom = VisualTokens.Spacing.S;
            _navHost.Add(_navLogo);

            _nav = new ResponsiveNavBar();
            _nav.DestinationSelected += OnNavDestinationSelected;
            _navHost.Add(_nav.Root);

            _navBackdropExtension = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, right = 0, display = DisplayStyle.None },
                pickingMode = PickingMode.Ignore
            };
            _navHost.Add(_navBackdropExtension);

            _screenLayer = new VisualElement { style = { flexGrow = 1, position = Position.Relative, overflow = Overflow.Hidden } };

            _root.Add(_navHost);
            _root.Add(_screenLayer);
        }

        private void OnNavDestinationSelected(NavDestination destination)
        {
            switch (destination)
            {
                case NavDestination.Worlds:
                    _flow.ReturnToSelectorFromResults();
                    break;
                case NavDestination.Results:
                    ShowResultsScreen(_flow.Engine.CurrentResult);
                    break;
                case NavDestination.Settings:
                    ShowSettingsScreen();
                    break;
            }
        }

        private void OnSelectedWorldChanged(int index)
        {
            if (_flow.State == GameFlowController.ShellState.WorldSelector)
                ShowWorldSelectorScreen();
        }

        private void OnStateChanged(GameFlowController.ShellState state)
        {
            // _root's background must be transparent during Playing, or this UI Toolkit overlay
            // (which composites on top of the scene Camera) paints solid Pearl0 over the whole
            // screen and the world's own Camera - sprites, ambient creatures, the capture gag,
            // everything - never becomes visible at all. Found while testing the Tide Troubles
            // capture gag on-device: the HUD progress bar rendered, but the entire world behind it
            // was just flat white the whole session. Every other state keeps the opaque Pearl0
            // backdrop, since those screens have no Camera under them to reveal.
            _root.style.backgroundColor = state == GameFlowController.ShellState.Playing
                ? Color.clear
                : VisualTokens.Colors.Pearl0;

            _navHost.style.display = state is GameFlowController.ShellState.Playing or GameFlowController.ShellState.Splash
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _nav?.SetOverlayMode(state == GameFlowController.ShellState.WorldSelector);

            // Home now carries its own prominent logo+claim at the top (per the v1.0 GOLDEN
            // board) - showing the small quiet nav-sidebar mark at the same time would duplicate
            // the brand moment, so it only appears on the other shell screens.
            if (_navLogo != null)
                _navLogo.style.display = state == GameFlowController.ShellState.WorldSelector ? DisplayStyle.None : DisplayStyle.Flex;

            switch (state)
            {
                case GameFlowController.ShellState.Splash: ShowSplashScreen(); break;
                case GameFlowController.ShellState.WorldSelector: ShowWorldSelectorScreen(); break;
                case GameFlowController.ShellState.HeadphoneChoice: ShowHeadphoneChoiceScreen(); break;
                case GameFlowController.ShellState.MicroInstruction: ShowMicroInstructionScreen(); break;
                case GameFlowController.ShellState.Playing: ShowPlayingHud(); break;
                case GameFlowController.ShellState.Results: ShowResultsScreen(_flow.Engine.CurrentResult); break;
                case GameFlowController.ShellState.Settings: ShowSettingsScreen(); break;
            }
        }

        private VisualElement NewScreen(bool padded = true)
        {
            _screenLayer.Clear();
            var screen = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    alignItems = padded ? Align.Center : Align.Stretch,
                    justifyContent = padded ? Justify.Center : Justify.FlexStart,
                }
            };
            if (padded)
            {
                screen.style.paddingTop = VisualTokens.Spacing.XL;
                screen.style.paddingBottom = VisualTokens.Spacing.XL;
                screen.style.paddingLeft = VisualTokens.Spacing.XL;
                screen.style.paddingRight = VisualTokens.Spacing.XL;
            }
            _screenLayer.Add(screen);
            return screen;
        }

        private static Label MakeLabel(string text, VisualTokens.TypeStyle style, Color color, float marginTop = 0f, float marginBottom = 0f)
        {
            return new Label(text)
            {
                style =
                {
                    fontSize = style.Size,
                    unityFontStyleAndWeight = style.Style,
                    color = color,
                    marginTop = marginTop,
                    marginBottom = marginBottom
                }
            };
        }

        private static Button MakePrimaryButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            StyleButton(button, VisualTokens.Colors.AuroraBlue, VisualTokens.Colors.Pearl0);
            return button;
        }

        private static Button MakeSecondaryButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            StyleButton(button, VisualTokens.Colors.Pearl50, VisualTokens.Colors.Ink900);
            return button;
        }

        private static void StyleButton(Button button, Color background, Color foreground)
        {
            button.style.backgroundColor = background;
            button.style.color = foreground;
            button.style.fontSize = VisualTokens.Type.Label.Size;
            button.style.unityFontStyleAndWeight = VisualTokens.Type.Label.Style;
            button.style.borderTopLeftRadius = VisualTokens.Radius.Pill;
            button.style.borderTopRightRadius = VisualTokens.Radius.Pill;
            button.style.borderBottomLeftRadius = VisualTokens.Radius.Pill;
            button.style.borderBottomRightRadius = VisualTokens.Radius.Pill;
            button.style.borderTopWidth = 0; button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0; button.style.borderRightWidth = 0;
            button.style.paddingTop = VisualTokens.Spacing.S; button.style.paddingBottom = VisualTokens.Spacing.S;
            button.style.paddingLeft = VisualTokens.Spacing.XL; button.style.paddingRight = VisualTokens.Spacing.XL;
            button.style.height = new StyleLength(StyleKeyword.Auto);
            button.style.minHeight = 40;
        }

        private void OnCarouselPointerDown(PointerDownEvent evt)
        {
            _swipeStartX = evt.position.x;
            _swipeTracking = true;
            _swipeCaptured = false;
            // Deliberately NOT capturing the pointer here (human feedback 2026-09-25: capturing
            // unconditionally broke tap-to-select on the peek cards, since a captured pointer's
            // up/click events go to _carousel instead of whichever card the finger is actually
            // over). Capture is deferred to OnCarouselPointerMove, only once real drag distance is
            // seen, so a plain tap is left alone and reaches the card's own ClickEvent normally.
        }

        private void OnCarouselPointerMove(PointerMoveEvent evt)
        {
            if (!_swipeTracking || _swipeCaptured) return;
            if (Mathf.Abs(evt.position.x - _swipeStartX) < SwipeCaptureThresholdPx) return;
            _swipeCaptured = true;
            _carousel.CapturePointer(evt.pointerId);
        }

        private void OnCarouselPointerUp(PointerUpEvent evt)
        {
            if (!_swipeTracking) return;
            _swipeTracking = false;
            if (_swipeCaptured)
            {
                _carousel.ReleasePointer(evt.pointerId);
                _swipeCaptured = false;
            }

            float deltaX = evt.position.x - _swipeStartX;
            if (Mathf.Abs(deltaX) < SwipeThresholdPx) return;

            int count = WorldRegistry.Worlds.Count;
            int direction = deltaX < 0 ? 1 : -1; // swipe left -> next world, swipe right -> previous
            int newIndex = (_flow.SelectedWorldIndex + direction + count) % count;
            _flow.SelectWorld(newIndex);
        }

        private void OnCarouselPointerCancel(PointerCancelEvent evt)
        {
            _swipeTracking = false;
            _swipeCaptured = false;
        }

        private static VisualElement MakeIcon(string name, float size, Color tint)
        {
            return new VisualElement
            {
                style =
                {
                    width = size, height = size,
                    backgroundImage = WorldArt.Icon(name),
                    unityBackgroundImageTintColor = tint,
                    flexShrink = 0
                }
            };
        }

        // ---------------------------------------------------------------- Splash

        private void ShowSplashScreen()
        {
            var screen = NewScreen();
            screen.style.backgroundColor = VisualTokens.Colors.Pearl0;
            screen.Add(BrandMarkView.CreateMark(320));
        }

        // ---------------------------------------------------------------- World Selector (real carousel)

        private void ShowWorldSelectorScreen()
        {
            _screenLayer.Clear();
            _lastHomeAspect = (WorldArt.HomeBgAspect)(-1);
            _lastHomeWorldId = null;
            _worldDots.Clear();

            BuildAmbientBackground();

            // Extra graduated darkening across the very top only, on top of the whole-background
            // blur/darken/desaturate treatment now applied in BuildAmbientBackground (which
            // deliberately reverses the earlier v1.0 handoff's "sharp active-world background,
            // never generic blur" rule - see WorldArt.GetTreatedAmbient) - the logo/claim still
            // need this additional top-only darkening for legibility over a bright sky (Tide
            // Troubles' daytime sky in particular). A single flat 26%-tall block left a hard
            // horizontal seam right across the screen
            // where it stopped (clearly visible on-device, absent from the GOLDEN board). UI
            // Toolkit has no gradient background, so the same darkening is stacked as bands of
            // decreasing height and alpha, which fades out instead of cutting off.
            float[] scrimHeights = { 34f, 28f, 23f, 18f, 14f, 10f, 7f, 4f };
            float[] scrimAlphas = { 0.04f, 0.04f, 0.04f, 0.04f, 0.04f, 0.04f, 0.04f, 0.04f };
            for (int i = 0; i < scrimHeights.Length; i++)
            {
                var band = new VisualElement
                {
                    style = { position = Position.Absolute, left = 0, top = 0, right = 0, height = Length.Percent(scrimHeights[i]), backgroundColor = new Color(0f, 0f, 0f, scrimAlphas[i]) },
                    pickingMode = PickingMode.Ignore
                };
                _screenLayer.Add(band);
            }

            var content = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };

            // ---- Brand: HEAR mark+wordmark (locked on-dark asset) + live claim text ----
            // Brand block size and vertical position are both driven from the screen in
            // UpdateCarouselForCurrentSize (see Golden.BrandTopOfScreenH / BrandLogoOfScreenW) -
            // a fixed 176-unit logo with a 72% cap rendered the wordmark at 0.435 of screen width
            // against the GOLDEN board's 0.277.
            var brand = new VisualElement
            {
                style =
                {
                    alignItems = Align.Center,
                    paddingLeft = VisualTokens.Spacing.L, paddingRight = VisualTokens.Spacing.L
                }
            };
            // hear-logo-handoff-v3.1: Home uses the no-claim logo variant and draws the claim
            // itself as live text - exact copy "Sound opens worlds", deliberately no trailing
            // period (design rule 7).
            var logoTex = WorldArt.LogoOnDark;
            var logoImage = new Image { scaleMode = ScaleMode.ScaleToFit, image = logoTex };
            brand.Add(logoImage);
            var claim = MakeLabel("Sound opens worlds", new VisualTokens.TypeStyle(18, 400), new Color(1f, 1f, 1f, 0.92f));
            brand.Add(claim);
            content.Add(brand);
            _brandBlock = brand;
            _brandLogo = logoImage;
            _brandClaim = claim;

            // ---- Carousel: one active card, two peeking neighbors (no separate thumbnail row) ----
            // flexGrow 0: the carousel box is exactly as tall as the active card, so the card's
            // top edge can be anchored at the GOLDEN board's own ratio instead of drifting with
            // whatever vertical slack a given screen happens to leave. All slack is collected by
            // _bottomSpacer below the Play pill, where the GOLDEN board also puts it.
            _carousel = new VisualElement { style = { flexGrow = 0, flexShrink = 0, position = Position.Relative } };
            _carousel.RegisterCallback<PointerDownEvent>(OnCarouselPointerDown);
            _carousel.RegisterCallback<PointerMoveEvent>(OnCarouselPointerMove);
            _carousel.RegisterCallback<PointerUpEvent>(OnCarouselPointerUp);
            _carousel.RegisterCallback<PointerCancelEvent>(OnCarouselPointerCancel);
            content.Add(_carousel);

            int count = WorldRegistry.Worlds.Count;
            int activeIndex = _flow.SelectedWorldIndex;
            int prevIndex = (activeIndex - 1 + count) % count;
            int nextIndex = (activeIndex + 1) % count;

            _prevCard = BuildCarouselCard(prevIndex, isActive: false, peekSide: -1);
            _nextCard = BuildCarouselCard(nextIndex, isActive: false, peekSide: 1);
            _activeCard = BuildCarouselCard(activeIndex, isActive: true, peekSide: 0);
            _carousel.Add(_prevCard);
            _carousel.Add(_nextCard);
            _carousel.Add(_activeCard);

            // The whole screen is rebuilt from scratch on every world change (simplest correct
            // approach given the shared engine/shell architecture), which otherwise made
            // selecting a world instantly "jump" with no transition - human feedback 2026-09-25
            // ("nepusobi profesionalne"). A short fade + gentle rise on the new card set gives it
            // real motion without needing to keep the old cards alive during the swap.
            if (_carouselEntranceAnim != null) StopCoroutine(_carouselEntranceAnim);
            _carouselEntranceAnim = StartCoroutine(AnimateCarouselEntrance(_prevCard, _activeCard, _nextCard));

            // ---- Dots (world counter) ----
            // Measured directly against sources/HEAR-App-UI-Home.png (852x1846) per human
            // request 2026-09-25 ("stale neměříš přesně"): dot diameter there is ~19.5px and the
            // Play button is ~344px wide - a 5.67% ratio - and, contrary to the previous guess,
            // ALL THREE dots are the SAME size; only color/opacity marks the active one, it is
            // NOT a wider pill. `DotDiameter` below is that ratio applied to our own Play button
            // width (see UpdateCarouselForCurrentSize) rather than a flat guessed constant.
            var dotsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center } };
            _dotsRow = dotsRow;
            for (int i = 0; i < count; i++)
            {
                bool isActive = i == activeIndex;
                var dot = new VisualElement
                {
                    style =
                    {
                        backgroundColor = isActive ? Color.white : new Color(1f, 1f, 1f, 0.5f)
                    }
                };
                _worldDots.Add(dot);
                dotsRow.Add(dot);
            }
            content.Add(dotsRow);

            // ---- Single Play pill, not attached to the card ----
            // The GOLDEN board itself shows no glow here (confirmed by direct pixel measurement:
            // a razor-sharp edge, no halo) and an earlier pass removed it for exactly that reason
            // - but human direction 2026-09-25 wants a very subtle one back regardless, as a
            // deliberate deviation from the board. Built the same way as the nav icon's glow (many
            // closely-spaced layers from a smooth exponential falloff, not a handful of
            // hand-tuned rings - see ResponsiveNavBar.AddItem's notes on why that matters), tuned
            // even more subtle than the nav icon per "opravdu musi byt jen velice subtilni".
            var playRow = new VisualElement { style = { alignItems = Align.Center } };
            _playGlowLayers = new VisualElement[PlayGlowLayerCount];
            for (int i = 0; i < PlayGlowLayerCount; i++)
            {
                var glow = new VisualElement
                {
                    style = { position = Position.Absolute, backgroundColor = new Color(0.55f, 0.75f, 1f, 0f) },
                    pickingMode = PickingMode.Ignore
                };
                _playGlowLayers[i] = glow;
                playRow.Add(glow);
            }
            var playButton = new Button(() => _flow.RequestPlaySelectedWorld()) { text = "Play   \u2192" };
            playButton.style.backgroundColor = Color.white;
            playButton.style.color = VisualTokens.Colors.Ink900;
            playButton.style.fontSize = VisualTokens.Type.Label.Size;
            playButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            playButton.style.borderTopLeftRadius = VisualTokens.Radius.Pill; playButton.style.borderTopRightRadius = VisualTokens.Radius.Pill;
            playButton.style.borderBottomLeftRadius = VisualTokens.Radius.Pill; playButton.style.borderBottomRightRadius = VisualTokens.Radius.Pill;
            playButton.style.borderTopWidth = 0; playButton.style.borderBottomWidth = 0; playButton.style.borderLeftWidth = 0; playButton.style.borderRightWidth = 0;
            playButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            playButton.style.paddingLeft = 0; playButton.style.paddingRight = 0; playButton.style.paddingTop = 0; playButton.style.paddingBottom = 0;
            playButton.style.marginLeft = 0; playButton.style.marginRight = 0; playButton.style.marginTop = 0; playButton.style.marginBottom = 0;
            _playButton = playButton;
            playRow.Add(playButton);
            _playRow = playRow;
            content.Add(playRow);

            // Every unit of vertical slack lands here, between the Play pill and the floating
            // bottom nav - the same place the GOLDEN board leaves open foreground for the
            // Companion. Its minimum height keeps the nav from ever covering the Play pill.
            _bottomSpacer = new VisualElement { style = { flexGrow = 1, flexShrink = 0 }, pickingMode = PickingMode.Ignore };
            content.Add(_bottomSpacer);

            _screenLayer.Add(content);

            // Companion + separate shadow, added last so they render above the carousel/dots/play,
            // anchored to the whole screen (not clipped to the carousel box) per companion.json.
            _companionShadow = new VisualElement
            {
                style = { position = Position.Absolute, unityBackgroundScaleMode = ScaleMode.ScaleToFit, backgroundImage = WorldArt.CompanionShadow },
                pickingMode = PickingMode.Ignore
            };
            _companion = new VisualElement
            {
                style = { position = Position.Absolute, unityBackgroundScaleMode = ScaleMode.ScaleToFit, backgroundImage = WorldArt.CompanionOnDarkNeutralLeft },
                pickingMode = PickingMode.Ignore
            };
            _screenLayer.Add(_companionShadow);
            _screenLayer.Add(_companion);

            UpdateCarouselForCurrentSize();
        }

        private VisualElement BuildCarouselCard(int worldIndex, bool isActive, int peekSide)
        {
            var entry = WorldRegistry.Worlds[worldIndex];
            var tex = WorldArt.GetWorldTextures(entry.Id);
            // Portrait crop (not Square) so the card reproduces the GOLDEN board's tall window -
            // sky/mountains/river/figure all visible top-to-bottom - instead of a squarer crop that
            // truncates the top of the scene.
            var cardTexture = tex.Portrait916 != null ? tex.Portrait916 : (tex.Square11 != null ? tex.Square11 : tex.Wide169);
            // Corner radius and border width are proportional to the card's own width and are
            // applied in ApplyCardRect, because both peek cards and the active card must keep the
            // GOLDEN board's single radius-to-width ratio (40px radius on a 487px card) at every
            // size. Measured border there: a ~2px near-white line at roughly 0.7 alpha.
            var card = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, top = 0,
                    overflow = Overflow.Hidden,
                    backgroundImage = cardTexture,
                    unityBackgroundScaleMode = ScaleMode.ScaleAndCrop,
                    opacity = isActive ? 1f : 0.88f
                }
            };

            if (isActive)
            {
                // GOLDEN board: title+tagline sit near the TOP of the card, directly over the sky,
                // with only a slight top-down gradient behind them - not a large opaque block
                // covering the lower half of the scene (that hid the river/figure/bridge/lantern).
                // UI Toolkit has no native gradient background, so it's faked with several stacked
                // bands of decreasing height/opacity instead of a single flat block (a single block
                // left a visible hard seam where it cut off against the sky).
                // Equal alphas, decreasing heights: stacked this way the darkening builds up
                // towards the top and thins out downwards. The previous table put its LARGEST
                // alpha (0.34) on its TALLEST band, which cut a hard horizontal line across the
                // card at 22% of its height - clearly visible on-device, absent from the board.
                float[] bandHeights = { 30f, 25f, 20f, 16f, 12f, 9f, 6f, 4f };
                // 0.10 per band composites to the same ~0.57 at the card's very top as the old
                // four-band table, so nothing is lost where the title sits on a bright daytime
                // sky (Tide Troubles) - only the hard edge at the bottom of the stack is gone.
                float[] bandAlphas = { 0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.10f, 0.10f };
                for (int i = 0; i < bandHeights.Length; i++)
                {
                    var band = new VisualElement
                    {
                        style = { position = Position.Absolute, left = 0, right = 0, top = 0, height = Length.Percent(bandHeights[i]), backgroundColor = new Color(0.02f, 0.03f, 0.06f, bandAlphas[i]) },
                        pickingMode = PickingMode.Ignore
                    };
                    card.Add(band);
                }

                var info = new VisualElement { style = { position = Position.Absolute, alignItems = Align.Center } };
                info.name = "cardInfo";
                // The GOLDEN board sets the world name in a light, widely tracked all-caps line,
                // not a bold one - measured cap height there is only 0.043 of the card's width.
                var title = MakeLabel(entry.DisplayName.ToUpperInvariant(), new VisualTokens.TypeStyle(20, 400), Color.white);
                title.name = "cardTitle";
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.whiteSpace = WhiteSpace.NoWrap;
                info.Add(title);
                var tagline = MakeLabel(entry.Tagline, VisualTokens.Type.Caption, new Color(1f, 1f, 1f, 0.88f));
                tagline.name = "cardTagline";
                tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
                tagline.style.whiteSpace = WhiteSpace.Normal;
                info.Add(tagline);
                card.Add(info);
            }
            else
            {
                // Deliberate deviation from the GOLDEN board/handoff spec (human direction,
                // 2026-09-25): side-peek cards carry no name label at all - only the active card
                // is titled. Side-peek cards stay fully vivid (no flat dim over the whole image)
                // and remain tappable to select that world.
                card.RegisterCallback<ClickEvent>(_ => _flow.SelectWorld(worldIndex));
            }
            return card;
        }

        private static void ApplyCardRect(VisualElement card, float left, float top, float width, float height, bool isActive, float activeCardWidth)
        {
            if (card == null) return;
            card.style.left = left;
            card.style.top = top;
            card.style.width = width;
            card.style.height = height;

            float radius = width * Golden.CardRadiusOfCardW;
            card.style.borderTopLeftRadius = radius; card.style.borderTopRightRadius = radius;
            card.style.borderBottomLeftRadius = radius; card.style.borderBottomRightRadius = radius;

            // Peek tiles keep a deliberately fainter frame than the active card (human direction
            // 2026-09-25); the GOLDEN board gives them none at all. Two earlier passes (a fainter
            // fixed width, then a peek-own-width-derived +1px one) still read as "rozmazany"
            // (blurry) or, worst, thicker than the active card's own line - because a peek card is
            // narrower, `Golden.CardBorderOfCardW * width` alone doesn't track the active card's
            // line weight at all. Fix, per human direction: derive the width from the ACTIVE
            // card's width for both, rounded once to a whole logical unit for a crisp (not
            // anti-aliased-soft) edge, and let only alpha - not thickness - distinguish peek from
            // active.
            float borderWidth = Mathf.Round(Mathf.Max(1f, activeCardWidth * Golden.CardBorderOfCardW));
            float borderAlpha = isActive ? Golden.CardBorderAlpha : 0.4f;
            card.style.borderTopWidth = borderWidth; card.style.borderBottomWidth = borderWidth;
            card.style.borderLeftWidth = borderWidth; card.style.borderRightWidth = borderWidth;
            var borderColor = new Color(1f, 1f, 1f, borderAlpha);
            card.style.borderTopColor = borderColor; card.style.borderBottomColor = borderColor;
            card.style.borderLeftColor = borderColor; card.style.borderRightColor = borderColor;
        }

        private void UpdateCarouselForCurrentSize()
        {
            if (_carousel == null) return;
            float screenW = _screenLayer.resolvedStyle.width;
            float screenH = _screenLayer.resolvedStyle.height;
            if (screenW <= 0 || screenH <= 0) return;

            // ---- Everything below is Golden's measured ratios applied to this screen ----
            // Horizontal sizes scale with screen width, vertical anchors with screen height, so
            // the composition transfers to an aspect ratio the mockup never showed.
            float activeW = screenW * Golden.CardWidthOfScreenW;
            float activeH = activeW * Golden.CardHeightOverWidth;
            float peekH = activeH * Golden.PeekHeightOfActiveH;
            float peekW = peekH / Golden.CardHeightOverWidth;
            float playW = screenW * Golden.PlayWidthOfScreenW;
            float playH = playW * Golden.PlayHeightOverWidth;
            float dotDiameter = playW * Golden.DotDiameterOfPlayW;
            float dotMargin = playW * (Golden.DotPitchOfPlayW - Golden.DotDiameterOfPlayW) * 0.5f;

            // ---- Brand block ----
            var logoTex = WorldArt.LogoOnDark;
            float brandBottom = 0f;
            if (_brandBlock != null)
            {
                float brandTop = Mathf.Max(GetTopSafeAreaInsetLogical() + VisualTokens.Spacing.S,
                                           screenH * Golden.BrandTopOfScreenH);
                _brandBlock.style.paddingTop = brandTop;
                float logoW = screenW * Golden.BrandLogoOfScreenW;
                float logoH = logoTex != null ? logoW * logoTex.height / logoTex.width : logoW * 0.56f;
                if (_brandLogo != null)
                {
                    _brandLogo.style.width = logoW;
                    _brandLogo.style.height = logoH;
                }
                float claimFont = screenW * Golden.ClaimFontOfScreenW;
                float claimGap = screenH * Golden.ClaimGapOfScreenH;
                // hear-logo-no-claim-on-dark-1024.png is 1024x874, but decoding its alpha channel
                // shows the visible dots+wordmark only occupy rows 51-560 (~64% of the canvas
                // height) - the bottom third-plus is transparent padding baked into the asset
                // itself (reserved for where the with-claim variant's claim text sits), not a
                // layout bug of ours. `logoH` above is sized to the FULL canvas so the Image
                // element displays the asset undistorted, so positioning the claim off `logoH`
                // put it under that invisible padding instead of under the real glyph - human
                // feedback 2026-09-25 ("claim je spatne umisteny... patri pod logo"). Pull the
                // claim up by that baked-in padding so `claimGap` measures from the actual visible
                // wordmark bottom, matching the sketch's tight "claim directly under logo" look.
                const float LogoVisibleContentBottomFrac = 0.6419f;
                float logoVisibleBottom = logoH * LogoVisibleContentBottomFrac;
                if (_brandClaim != null)
                {
                    _brandClaim.style.fontSize = claimFont;
                    _brandClaim.style.marginTop = claimGap - (logoH - logoVisibleBottom);
                }
                brandBottom = brandTop + logoVisibleBottom + claimGap + claimFont * 1.25f;
            }

            // ---- Carousel box ----
            // `Golden.CardTopOfScreenH` (an absolute screenH fraction measured on the GOLDEN
            // board's 852x1846/0.462-aspect mockup) is gone: on this device's much taller
            // 968x2376/0.407 aspect it pinned the card far too close to the claim while leaving
            // a huge unused band (~200 logical units, measured) for `_bottomSpacer` to swallow
            // below Play - human feedback 2026-09-25 ("strasne moc prostoru... uplne prazdno" /
            // "claim... temer se nedotyka karuselu"). Same aspect-mismatch bug class already fixed
            // once for the nav bar. Fix: anchor the card a comfortable fixed gap under the claim,
            // then let the real leftover space (computed from actual content, not a mockup ratio)
            // grow the carousel itself - capped, so it reads as "a bigger carousel" rather than an
            // absurd one - instead of pooling as dead air the bottom spacer alone absorbed before.
            float cardTopBase = brandBottom + VisualTokens.Spacing.XXL;

            bool compactNavOverlay = ResponsiveNavBar.BreakpointForWidth(screenW) == ShellBreakpoint.Compact;
            // Matches ApplyBreakpointLayout's navBottom (safe-area inset + 4, not the old
            // Golden-board offset - see that method's notes) plus the bar's own height, so the
            // reserved space above the nav exactly matches where it actually sits, instead of
            // leaving the same oversized dead gap this was paired with.
            float navReserve = compactNavOverlay
                ? GetBottomSafeAreaInsetLogical() + 4f + ResponsiveNavBar.CompactBarHeight + VisualTokens.Spacing.S
                : VisualTokens.Spacing.L + GetBottomSafeAreaInsetLogical();
            float dotsGap = screenH * Golden.CardToDotsOfScreenH;
            // Only a placeholder for the slack/short-screen math below - overwritten further down
            // once the card's final (possibly grown) height is known, per human direction
            // 2026-09-25 ("Play moc prilepeny k teckam... do poloviny zbyvajiciho prostoru").
            float playGap = screenH * Golden.DotsToPlayOfScreenH;
            float belowCard = dotsGap + dotDiameter + playGap + playH + navReserve;

            float slack = screenH - cardTopBase - activeH - belowCard;
            if (slack > 0f)
            {
                // Grow the card into up to ~30% of its own height worth of the leftover space
                // (rather than all of it, so it stays a "bigger carousel", not a wall) - whatever
                // remains after that still shrinks `_bottomSpacer`'s dead gap a lot, from ~200
                // logical units down to a plausible, deliberate-looking clearance above the nav.
                float growCap = activeH * 0.3f;
                float grow = Mathf.Min(slack, growCap);
                activeH += grow;
                activeW = activeH / Golden.CardHeightOverWidth;
                peekH = activeH * Golden.PeekHeightOfActiveH;
                peekW = peekH / Golden.CardHeightOverWidth;
            }

            float cardTop = cardTopBase;
            // Overlap guard for short screens: card, dots and Play are each laid out with a
            // strictly positive measured gap, so they cannot overlap each other - but on a screen
            // too short for all of them the fixed-height carousel would push the Play pill off the
            // bottom or under the floating nav instead. Shrink the card (keeping its aspect, so its
            // silhouette stays the GOLDEN one) until the whole column fits.
            float cardBudget = screenH - cardTop - belowCard;
            if (activeH > cardBudget)
            {
                activeH = Mathf.Max(40f, cardBudget);
                activeW = activeH / Golden.CardHeightOverWidth;
                peekH = activeH * Golden.PeekHeightOfActiveH;
                peekW = peekH / Golden.CardHeightOverWidth;
            }

            // ---- Play vertical position: centred in the real leftover space below the dots,
            // not glued to them ----
            // Human direction 2026-09-25: Play read as "too stuck" to the three dots and should
            // move down, roughly to the middle of whatever vertical room is actually left above
            // the nav. Replaces the small Golden-ratio DotsToPlayOfScreenH gap (now unused for
            // this) with half of the true remaining space, so the other half naturally becomes
            // `_bottomSpacer`'s clearance above the nav - the two ends of that leftover room stay
            // visually balanced instead of Play sitting at one extreme of it.
            float dotsBottom = cardTop + activeH + dotsGap + dotDiameter;
            float spaceForPlayAndSpacer = Mathf.Max(VisualTokens.Spacing.XL, screenH - navReserve - dotsBottom - playH);
            playGap = spaceForPlayAndSpacer * 0.5f;

            _carousel.style.height = activeH;
            _carousel.style.marginTop = cardTop - brandBottom;

            float activeLeft = (screenW - activeW) * 0.5f;
            const float activeTop = 0f;
            // Peek tiles keep the active card's silhouette but sit slightly higher, exactly as the
            // GOLDEN board has them (peek centre 20px above the active card's centre on 852x1846).
            float peekTop = activeTop + (activeH - peekH) * 0.5f - activeH * Golden.PeekCenterRiseOfActiveH;
            float peekGap = screenW * Golden.PeekGapOfScreenW;

            ApplyCardRect(_activeCard, activeLeft, activeTop, activeW, activeH, isActive: true, activeCardWidth: activeW);
            ApplyCardRect(_prevCard, activeLeft - peekGap - peekW, peekTop, peekW, peekH, isActive: false, activeCardWidth: activeW);
            ApplyCardRect(_nextCard, activeLeft + activeW + peekGap, peekTop, peekW, peekH, isActive: false, activeCardWidth: activeW);

            // ---- Active card's title and tagline ----
            var title = _activeCard?.Q<Label>("cardTitle");
            var tagline = _activeCard?.Q<Label>("cardTagline");
            var info = _activeCard?.Q<VisualElement>("cardInfo");
            float cardPadding = activeW * Golden.CardPaddingOfCardW;
            if (info != null)
            {
                info.style.left = cardPadding;
                info.style.right = cardPadding;
                info.style.top = cardPadding;
            }
            if (title != null)
            {
                // Size from the card, then let tracking carry the rest of the width. A long name
                // ("THE PAPER GARDEN") would otherwise overrun the card at the GOLDEN board's own
                // tracking, and whiteSpace is NoWrap here - so shrink both together to fit.
                string text = title.text ?? string.Empty;
                float innerW = Mathf.Max(1f, activeW - cardPadding * 2f);
                float fontSize = activeW * Golden.CardTitleCapOfCardW / 0.711f;
                float tracking = Golden.CardTitleTrackingEmHundredths;
                int glyphs = Mathf.Max(1, text.Length);
                // 0.42 em is this font's measured average advance for an all-caps world name, so
                // `projected` is only an overflow guard - the aimed-for width comes from the cap
                // height and the tracking above. The longest name ("THE PAPER GARDEN") is the one
                // that decides whether the guard ever fires.
                float projected = glyphs * fontSize * 0.42f + (glyphs - 1) * tracking * 0.01f * fontSize;
                if (projected > innerW)
                {
                    float shrink = innerW / projected;
                    fontSize *= shrink;
                    tracking *= shrink;
                }
                title.style.fontSize = fontSize;
                title.style.letterSpacing = tracking;
                if (tagline != null)
                {
                    tagline.style.fontSize = fontSize * 0.80f;
                    tagline.style.marginTop = fontSize * 0.55f;
                }
            }

            // ---- Play pill ----
            // Explicit pixel sizes, not Percent+maxWidth: under ConstantPhysicalSize the
            // Percent(61)+maxWidth(340) combination on the Button silently failed to clamp on real
            // Android hardware and rendered as a screen-spanning ellipse.
            if (_playButton != null)
            {
                _playButton.style.width = playW;
                _playButton.style.height = playH;
                _playButton.style.fontSize = playW * Golden.PlayFontOfPlayW;
                _playButton.style.borderTopLeftRadius = playH * 0.5f; _playButton.style.borderTopRightRadius = playH * 0.5f;
                _playButton.style.borderBottomLeftRadius = playH * 0.5f; _playButton.style.borderBottomRightRadius = playH * 0.5f;

                if (_playGlowLayers != null)
                {
                    for (int i = 0; i < _playGlowLayers.Length; i++)
                    {
                        float t = i / (PlayGlowLayerCount - 1f);
                        float pad = t * PlayGlowMaxPadding;
                        float alpha = PlayGlowPeakAlpha * Mathf.Exp(-PlayGlowFalloffRate * t);
                        SizeCenteredGlow(_playGlowLayers[i], playW + pad * 2f, playH + pad * 2f, alpha);
                    }
                }
            }

            foreach (var dot in _worldDots)
            {
                dot.style.width = dotDiameter;
                dot.style.height = dotDiameter;
                dot.style.marginLeft = dotMargin;
                dot.style.marginRight = dotMargin;
                dot.style.borderTopLeftRadius = dotDiameter * 0.5f; dot.style.borderTopRightRadius = dotDiameter * 0.5f;
                dot.style.borderBottomLeftRadius = dotDiameter * 0.5f; dot.style.borderBottomRightRadius = dotDiameter * 0.5f;
            }

            // ---- Vertical gaps between card, dots and Play ----
            // These gaps are the whole reason the three rows can never overlap: each one is a
            // strictly positive margin measured on the GOLDEN board, and nothing here draws
            // outside its own box any more, so there is no halo that could reach across one.
            if (_dotsRow != null) _dotsRow.style.marginTop = dotsGap;
            if (_playRow != null)
            {
                _playRow.style.marginTop = playGap;
                _playRow.style.marginBottom = 0f;
            }
            if (_bottomSpacer != null) _bottomSpacer.style.minHeight = navReserve;

            RefreshHomeBackground();

            // Companion + shadow: anchored to the Play button's own corner, not a screen-normalized
            // companion.json placement - human direction 2026-09-25 ("maskota bych vzdy prilepil k
            // tomu tlacitku... jakoby byl v rohu toho tlacitka, nebo z nej vyrustal"). A
            // screen-normalized position happened to look right only by coincidence of where Play
            // used to sit; now that Play's own vertical position is content-driven (see the "Play
            // vertical position" block above), anchoring off Play directly keeps the two glued
            // together through any future retune instead of drifting apart again.
            var companionTex = WorldArt.CompanionOnDarkNeutralLeft;
            if (companionTex != null && _companion != null)
            {
                var placement = GetHomeCompanionPlacement(ClassifyHomeAspect(screenW, screenH));
                // companion.json's widthFraction describes the Companion as it is SEEN. The
                // supplied sprite is 1024px wide but its non-transparent body only spans 619px of
                // that (0.6045), and ScaleToFit fits the whole texture including its transparent
                // margin - so applying widthFraction to the element box rendered the Companion at
                // 0.112 of screen width on-device against 0.203 measured on the GOLDEN board.
                float companionWidth = screenW * placement.WidthFraction / CompanionSpriteContentFraction;
                float companionHeight = companionWidth * companionTex.height / companionTex.width;

                float playRowTop = dotsBottom + playGap;
                float playLeft = (screenW - playW) * 0.5f;
                float playRight = playLeft + playW;
                // Sits mostly beside/above Play's top-right corner, only its base overlapping, as
                // if growing out of it - first pass centred too far over the button and covered
                // its own arrow glyph, confirmed on-device 2026-09-25.
                float centerX = playRight + companionWidth * 0.08f;
                float centerY = playRowTop - companionHeight * 0.32f;
                _companion.style.width = companionWidth;
                _companion.style.height = companionHeight;
                _companion.style.left = centerX - companionWidth * 0.5f;
                _companion.style.top = centerY - companionHeight * 0.5f;

                var shadowTex = WorldArt.CompanionShadow;
                if (shadowTex != null && _companionShadow != null)
                {
                    float shadowWidth = companionWidth * 0.85f;
                    float shadowHeight = shadowWidth * shadowTex.height / shadowTex.width;
                    _companionShadow.style.width = shadowWidth;
                    _companionShadow.style.height = shadowHeight;
                    _companionShadow.style.left = centerX - shadowWidth * 0.5f;
                    _companionShadow.style.top = centerY + companionHeight * 0.34f;
                }
            }
        }

        /// <summary>Fraction of `companion-neutral-left.png`'s width occupied by non-transparent
        /// pixels (619 of 1024, measured 2026-09-25). See the companion sizing in
        /// UpdateCarouselForCurrentSize.</summary>
        private const float CompanionSpriteContentFraction = 0.6045f;

        private static WorldArt.HomeBgAspect ClassifyHomeAspect(float screenW, float screenH)
        {
            float aspect = screenW / screenH;
            return aspect >= 1.8f ? WorldArt.HomeBgAspect.Ultrawide
                : aspect >= 1.15f ? WorldArt.HomeBgAspect.Landscape
                : aspect <= 0.85f ? WorldArt.HomeBgAspect.Portrait
                : WorldArt.HomeBgAspect.Square;
        }

        // Home-screen Companion placement per docs/v1.0-home-handoff/companion.json's
        // `homePlacement` block: normalized center is relative to the full screen, not the
        // carousel card - matching the GOLDEN board's "sits beside/below in the foreground"
        // composition instead of being tucked against the card's corner.
        private static (float NormX, float NormY, float WidthFraction) GetHomeCompanionPlacement(WorldArt.HomeBgAspect aspect)
        {
            return aspect switch
            {
                WorldArt.HomeBgAspect.Square => (0.83f, 0.72f, 0.15f),
                WorldArt.HomeBgAspect.Landscape or WorldArt.HomeBgAspect.Ultrawide => (0.88f, 0.74f, 0.10f),
                _ => (0.8f, 0.73f, 0.19f),
            };
        }

        private void BuildAmbientBackground()
        {
            if (_ambientBack == null)
            {
                _ambientBack = new VisualElement
                {
                    style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0, unityBackgroundScaleMode = ScaleMode.ScaleAndCrop },
                    pickingMode = PickingMode.Ignore
                };
                _ambientFront = new VisualElement
                {
                    style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0, unityBackgroundScaleMode = ScaleMode.ScaleAndCrop, opacity = 0f },
                    pickingMode = PickingMode.Ignore
                };
            }

            _screenLayer.Add(_ambientBack);
            _screenLayer.Add(_ambientFront);

            RefreshHomeBackground(force: true);
        }

        private void RefreshHomeBackground(bool force = false)
        {
            float screenW = _screenLayer.resolvedStyle.width;
            float screenH = _screenLayer.resolvedStyle.height;
            WorldArt.HomeBgAspect aspectClass = screenW > 0 && screenH > 0
                ? ClassifyHomeAspect(screenW, screenH)
                : WorldArt.HomeBgAspect.Portrait;

            string worldId = WorldRegistry.Worlds[_flow.SelectedWorldIndex].Id;
            if (!force && aspectClass == _lastHomeAspect && worldId == _lastHomeWorldId) return;
            _lastHomeAspect = aspectClass;
            _lastHomeWorldId = worldId;

            var tex = WorldArt.GetHomeBackground(worldId, aspectClass);
            if (tex == null) return;

            // Identity is tracked against the untreated source (_lastAmbientSourceTex), not
            // against what's actually on screen: the displayed image is now the blurred/darkened/
            // desaturated result (WorldArt.GetTreatedAmbient), a RenderTexture that's never `==`
            // comparable to the source Texture2D the old "already showing this" check used.
            if (_lastAmbientSourceTex == null)
            {
                // First entry into the world selector this session - no previous world to fade from.
                _ambientBack.style.backgroundImage = ToStyleBackground(WorldArt.GetTreatedAmbient(tex));
                _lastAmbientSourceTex = tex;
                return;
            }

            if (_lastAmbientSourceTex == tex) return;
            _lastAmbientSourceTex = tex;

            var treated = WorldArt.GetTreatedAmbient(tex);
            _ambientFront.style.backgroundImage = ToStyleBackground(treated);
            _ambientFront.style.opacity = 0f;
            if (_ambientAnim != null) StopCoroutine(_ambientAnim);
            _ambientAnim = StartCoroutine(CrossfadeAmbient(treated));
        }

        private static StyleBackground ToStyleBackground(Texture tex)
        {
            return tex switch
            {
                RenderTexture rt => new StyleBackground(Background.FromRenderTexture(rt)),
                Texture2D t2d => new StyleBackground(Background.FromTexture2D(t2d)),
                _ => default
            };
        }

        // Centers a pill-shaped glow layer over its (also-centered) sibling in `playRow`
        // regardless of the row's own resolved size - see the Play pill glow call site above.
        private static void SizeCenteredGlow(VisualElement glow, float width, float height, float alpha)
        {
            glow.style.width = width;
            glow.style.height = height;
            glow.style.left = new Length(50f, LengthUnit.Percent);
            glow.style.top = new Length(50f, LengthUnit.Percent);
            glow.style.marginLeft = -width * 0.5f;
            glow.style.marginTop = -height * 0.5f;
            float radius = height * 0.5f;
            glow.style.borderTopLeftRadius = radius; glow.style.borderTopRightRadius = radius;
            glow.style.borderBottomLeftRadius = radius; glow.style.borderBottomRightRadius = radius;
            var c = glow.style.backgroundColor.value;
            glow.style.backgroundColor = new Color(c.r, c.g, c.b, alpha);
        }

        private IEnumerator CrossfadeAmbient(Texture newTex)
        {
            float duration = VisualTokens.MotionMs.Scene / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _ambientFront.style.opacity = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _ambientBack.style.backgroundImage = ToStyleBackground(newTex);
            _ambientFront.style.opacity = 0f;
        }

        private IEnumerator AnimateCarouselEntrance(VisualElement prev, VisualElement active, VisualElement next)
        {
            var cards = new[] { prev, active, next };
            const float riseStartPx = 14f;
            foreach (var c in cards)
            {
                if (c == null) continue;
                c.style.opacity = 0f;
                c.style.translate = new Translate(0, riseStartPx, 0);
            }
            // One frame so UpdateCarouselForCurrentSize (driven from Update()) lays the cards out
            // at their real rect first - translate then animates relative to that correct position
            // instead of a stale/zeroed one.
            yield return null;

            float duration = VisualTokens.MotionMs.Normal / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out cubic
                foreach (var c in cards)
                {
                    if (c == null) continue;
                    c.style.opacity = eased;
                    c.style.translate = new Translate(0, riseStartPx * (1f - eased), 0);
                }
                yield return null;
            }
            foreach (var c in cards)
            {
                if (c == null) continue;
                c.style.opacity = 1f;
                c.style.translate = new Translate(0, 0, 0);
            }
        }

        // Approximate Screen.safeArea (physical px) -> our ConstantPhysicalSize logical units,
        // using the same referenceDpi/deviceDpi ratio the panel itself applies. Currently only
        // wired into the Home screen's top/bottom spacing; full safe-area coverage of every
        // screen and the bottom nav bar is a follow-up (see README known limitations).
        private static float PhysicalToLogical(float physicalPx)
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : 96f;
            return physicalPx * (96f / dpi);
        }

        private static float GetTopSafeAreaInsetLogical()
        {
            var safeArea = Screen.safeArea;
            float topPhysical = Screen.height - (safeArea.y + safeArea.height);
            return Mathf.Max(0f, PhysicalToLogical(topPhysical));
        }

        private static float GetBottomSafeAreaInsetLogical()
        {
            var safeArea = Screen.safeArea;
            return Mathf.Max(0f, PhysicalToLogical(safeArea.y));
        }

        // ---------------------------------------------------------------- Headphone choice

        private void ShowHeadphoneChoiceScreen()
        {
            var screen = NewScreen();
            screen.Add(MakeIcon("headphones", 56, VisualTokens.Colors.AuroraBlue));
            screen.Add(MakeLabel("Headphones recommended", VisualTokens.Type.Title, VisualTokens.Colors.Ink900, VisualTokens.Spacing.M));
            screen.Add(MakeLabel("More precise; left and right can be tested separately.", VisualTokens.Type.Body, VisualTokens.Colors.Ink700, VisualTokens.Spacing.S, VisualTokens.Spacing.XL));

            var headphonesButton = MakePrimaryButton("Continue with Headphones", () => _flow.ChooseOutputMode(AudioOutputMode.Headphones));
            headphonesButton.style.width = 280;
            headphonesButton.style.maxWidth = Length.Percent(85);
            headphonesButton.style.marginBottom = VisualTokens.Spacing.S;
            screen.Add(headphonesButton);

            var speakerButton = MakeSecondaryButton("Play through Speaker", () => _flow.ChooseOutputMode(AudioOutputMode.Speaker));
            speakerButton.style.width = 280;
            speakerButton.style.maxWidth = Length.Percent(85);
            screen.Add(speakerButton);
        }

        // ---------------------------------------------------------------- Micro instruction

        private void ShowMicroInstructionScreen()
        {
            var screen = NewScreen();
            screen.Add(MakeLabel("Tap when you hear the tone.", VisualTokens.Type.Title, VisualTokens.Colors.Ink900));
            var continueButton = MakePrimaryButton("Got it", () => _flow.ConfirmMicroInstruction());
            continueButton.style.marginTop = VisualTokens.Spacing.XL;
            continueButton.style.width = 160;
            continueButton.style.maxWidth = Length.Percent(85);
            screen.Add(continueButton);
        }

        // ---------------------------------------------------------------- Playing HUD

        private void ShowPlayingHud()
        {
            _screenLayer.Clear();
            var hudRoot = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, top = VisualTokens.Spacing.M, left = VisualTokens.Spacing.M, right = VisualTokens.Spacing.M,
                    flexDirection = FlexDirection.Row, alignItems = Align.Center
                },
                pickingMode = PickingMode.Ignore
            };

            var track = new VisualElement
            {
                style =
                {
                    flexGrow = 1, height = 6, backgroundColor = VisualTokens.Colors.Mist100,
                    borderTopLeftRadius = VisualTokens.Radius.S, borderTopRightRadius = VisualTokens.Radius.S,
                    borderBottomLeftRadius = VisualTokens.Radius.S, borderBottomRightRadius = VisualTokens.Radius.S
                }
            };
            _hudProgressFill = new VisualElement
            {
                style =
                {
                    height = 6, width = 0, backgroundColor = VisualTokens.Colors.AuroraBlue,
                    borderTopLeftRadius = VisualTokens.Radius.S, borderTopRightRadius = VisualTokens.Radius.S,
                    borderBottomLeftRadius = VisualTokens.Radius.S, borderBottomRightRadius = VisualTokens.Radius.S
                }
            };
            track.Add(_hudProgressFill);
            hudRoot.Add(track);

            _hudProgressLabel = MakeLabel("0%", VisualTokens.Type.Caption, VisualTokens.Colors.Ink700);
            _hudProgressLabel.style.marginLeft = VisualTokens.Spacing.S;
            hudRoot.Add(_hudProgressLabel);

            _screenLayer.Add(hudRoot);
        }

        private void UpdateHud()
        {
            if (_hudProgressFill == null) return;
            float progress = _flow.Engine.Progress;
            _hudProgressFill.style.width = new Length(progress * 100f, LengthUnit.Percent);
            _hudProgressLabel.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        }

        // ---------------------------------------------------------------- Results

        private void ShowResultsScreen(SessionResult result)
        {
            var screen = NewScreen();
            screen.Add(MakeLabel("Nice job!", VisualTokens.Type.Title, VisualTokens.Colors.Ink900));
            screen.Add(new Label("Your hearing tested like a typical listener's today.")
            {
                style =
                {
                    fontSize = VisualTokens.Type.Body.Size, unityFontStyleAndWeight = VisualTokens.Type.Body.Style,
                    color = VisualTokens.Colors.Ink700, marginTop = VisualTokens.Spacing.S, marginBottom = VisualTokens.Spacing.XS,
                    whiteSpace = WhiteSpace.Normal, maxWidth = 360, unityTextAlign = TextAnchor.MiddleCenter
                }
            });
            screen.Add(MakeLabel("(placeholder framing - real ear-age norm curve is not yet implemented)", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, 0f, VisualTokens.Spacing.L));

            if (result != null)
            {
                screen.Add(MakeLabel($"Detected {result.CorrectDetections} tones, correctly rejected {result.CorrectRejections} silent trials.",
                    VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, 0f, VisualTokens.Spacing.XL));
            }

            var again = MakeSecondaryButton("Back to Worlds", () => _flow.ReturnToSelectorFromResults());
            again.style.width = 200;
            again.style.maxWidth = Length.Percent(85);
            screen.Add(again);
        }

        // ---------------------------------------------------------------- Settings

        private void ShowSettingsScreen()
        {
            var screen = NewScreen();
            screen.Add(MakeLabel("Settings", VisualTokens.Type.Title, VisualTokens.Colors.Ink900));
            screen.Add(MakeLabel("(placeholder - exact settings surface is an open question)", VisualTokens.Type.Caption, VisualTokens.Colors.Slate400, VisualTokens.Spacing.S));
        }
    }
}
