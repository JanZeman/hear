using System.Collections;
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

        // Home / World Selector carousel live references, updated in place on resize instead of a
        // full rebuild. Rebuilt per hear-home-screen-handoff-v1.0: a real carousel (one active
        // card, two peeking neighbors) rather than a hero panel + separate thumbnail row.
        private VisualElement _carousel;
        private VisualElement _activeCard;
        // Fraction of a neighbor card's own width that peeks out past the active card on top of it
        // (shared by layout math in UpdateCarouselForCurrentSize and label placement in
        // BuildCarouselCard so the world-name label bar stays inside the visible sliver).
        private const float CarouselPeekVisibleFraction = 0.5f;
        private VisualElement _prevCard;
        private VisualElement _nextCard;
        private VisualElement _companion;
        private VisualElement _companionShadow;
        private Button _playButton;
        private VisualElement _playRow;
        private VisualElement _navLogo;
        private WorldArt.HomeBgAspect _lastHomeAspect = (WorldArt.HomeBgAspect)(-1);
        private string _lastHomeWorldId;

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
            _navHost.style.bottom = navOverlay ? 0 : new StyleLength(StyleKeyword.Auto);

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
            settings.referenceDpi = 96f;
            settings.fallbackDpi = 96f;
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

            BuildAmbientBackground();

            // Flat darkening across the very top only, so the logo/claim stay legible over any
            // world's sky (Tide Troubles' daytime sky in particular) without a blur pass over the
            // whole background, per the v1.0 rule "sharp active-world background, not blur".
            var topScrim = new VisualElement
            {
                style = { position = Position.Absolute, left = 0, top = 0, right = 0, height = Length.Percent(26), backgroundColor = new Color(0f, 0f, 0f, 0.24f) },
                pickingMode = PickingMode.Ignore
            };
            _screenLayer.Add(topScrim);

            var content = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };

            // ---- Brand: HEAR mark+wordmark (locked on-dark asset) + live claim text ----
            var brand = new VisualElement
            {
                style =
                {
                    alignItems = Align.Center,
                    paddingTop = VisualTokens.Spacing.L + GetTopSafeAreaInsetLogical(),
                    paddingLeft = VisualTokens.Spacing.L, paddingRight = VisualTokens.Spacing.L
                }
            };
            var logoTex = WorldArt.LogoOnDark;
            var logoImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                image = logoTex,
                style = { width = 176, maxWidth = Length.Percent(72), height = logoTex != null ? 176f * logoTex.height / logoTex.width : 60f }
            };
            brand.Add(logoImage);
            var claim = MakeLabel("Sound opens worlds.", new VisualTokens.TypeStyle(18, 400), new Color(1f, 1f, 1f, 0.92f), VisualTokens.Spacing.XS);
            brand.Add(claim);
            content.Add(brand);

            // ---- Carousel: one active card, two peeking neighbors (no separate thumbnail row) ----
            _carousel = new VisualElement { style = { flexGrow = 1, position = Position.Relative, marginTop = VisualTokens.Spacing.S } };
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

            // ---- Dots ----
            var dotsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, marginTop = VisualTokens.Spacing.S } };
            for (int i = 0; i < count; i++)
            {
                bool isActive = i == activeIndex;
                var dot = new VisualElement
                {
                    style =
                    {
                        width = isActive ? 18 : 7, height = 7,
                        borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                        backgroundColor = isActive ? Color.white : new Color(1f, 1f, 1f, 0.4f),
                        marginLeft = 4, marginRight = 4
                    }
                };
                dotsRow.Add(dot);
            }
            content.Add(dotsRow);

            // ---- Single Play pill (not attached to the card) ----
            var playRow = new VisualElement
            {
                style = { alignItems = Align.Center, marginTop = VisualTokens.Spacing.M, marginBottom = VisualTokens.Spacing.L + GetBottomSafeAreaInsetLogical() }
            };
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
            var card = new VisualElement
            {
                style =
                {
                    position = Position.Absolute, top = 0,
                    borderTopLeftRadius = VisualTokens.Radius.XL, borderTopRightRadius = VisualTokens.Radius.XL,
                    borderBottomLeftRadius = VisualTokens.Radius.XL, borderBottomRightRadius = VisualTokens.Radius.XL,
                    overflow = Overflow.Hidden,
                    backgroundImage = cardTexture,
                    unityBackgroundScaleMode = ScaleMode.ScaleAndCrop,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new Color(1f, 1f, 1f, isActive ? 0.45f : 0.12f),
                    borderBottomColor = new Color(1f, 1f, 1f, isActive ? 0.45f : 0.12f),
                    borderLeftColor = new Color(1f, 1f, 1f, isActive ? 0.45f : 0.12f),
                    borderRightColor = new Color(1f, 1f, 1f, isActive ? 0.45f : 0.12f)
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
                float[] bandHeights = { 22f, 17f, 12f, 8f };
                float[] bandAlphas = { 0.34f, 0.22f, 0.12f, 0.05f };
                for (int i = 0; i < bandHeights.Length; i++)
                {
                    var band = new VisualElement
                    {
                        style = { position = Position.Absolute, left = 0, right = 0, top = 0, height = Length.Percent(bandHeights[i]), backgroundColor = new Color(0.02f, 0.03f, 0.06f, bandAlphas[i]) },
                        pickingMode = PickingMode.Ignore
                    };
                    card.Add(band);
                }

                var info = new VisualElement { style = { position = Position.Absolute, left = VisualTokens.Spacing.L, right = VisualTokens.Spacing.L, top = VisualTokens.Spacing.L, alignItems = Align.Center } };
                var title = MakeLabel(entry.DisplayName.ToUpperInvariant(), VisualTokens.Type.Headline, Color.white);
                title.name = "cardTitle";
                title.style.letterSpacing = 2;
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.whiteSpace = WhiteSpace.Normal;
                info.Add(title);
                var tagline = MakeLabel(entry.Tagline, VisualTokens.Type.Caption, new Color(1f, 1f, 1f, 0.88f), 4);
                tagline.name = "cardTagline";
                tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
                tagline.style.whiteSpace = WhiteSpace.Normal;
                info.Add(tagline);
                card.Add(info);
            }
            else
            {
                // GOLDEN board: side-peek cards stay fully vivid (no flat dim over the whole
                // image) - only a solid label bar at the bottom carries the world name, matching
                // "TIDE TROUBLES" / "THE PAPER GARDEN" in the reference. The active card sits on
                // top and hides most of the neighbor, so the label bar is anchored to the visible
                // outer edge instead of spanning the whole card width - otherwise its centered text
                // gets sliced into unreadable fragments behind the active card. Width is set in
                // pixels from UpdateCarouselForCurrentSize (matching the real peek math) rather
                // than Length.Percent, which resolved unreliably against this absolutely
                // positioned parent and caused the label to wrap one letter per line.
                var labelBar = new VisualElement
                {
                    name = "peekLabelBar",
                    style =
                    {
                        position = Position.Absolute, bottom = 0,
                        left = peekSide < 0 ? 0 : new StyleLength(StyleKeyword.Auto),
                        right = peekSide < 0 ? new StyleLength(StyleKeyword.Auto) : 0,
                        paddingTop = 4, paddingBottom = 4,
                        paddingLeft = 2, paddingRight = 2,
                        backgroundColor = new Color(0.04f, 0.06f, 0.12f, 0.78f)
                    },
                    pickingMode = PickingMode.Ignore
                };
                card.Add(labelBar);
                // Small, tight font (no letter-spacing) - the visible peek sliver is only ~35
                // logical units wide, far narrower than the active card's own title area, so the
                // Caption style (13px + spacing) still broke individual words mid-letter.
                var sideLabel = MakeLabel(entry.DisplayName.ToUpperInvariant(), new VisualTokens.TypeStyle(9, 600), Color.white);
                sideLabel.style.whiteSpace = WhiteSpace.Normal;
                sideLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                labelBar.Add(sideLabel);
                card.RegisterCallback<ClickEvent>(_ => _flow.SelectWorld(worldIndex));
            }
            return card;
        }

        private static void ApplyCardRect(VisualElement card, float left, float top, float width, float height)
        {
            if (card == null) return;
            card.style.left = left;
            card.style.top = top;
            card.style.width = width;
            card.style.height = height;
        }

        private void UpdateCarouselForCurrentSize()
        {
            if (_carousel == null) return;
            float screenW = _screenLayer.resolvedStyle.width;
            float screenH = _screenLayer.resolvedStyle.height;
            if (screenW <= 0 || screenH <= 0) return;

            // Fractions from metadata/home-layout.json's "compact" carousel block - used across all
            // breakpoints for this first pass rather than a second Wide-specific tuning table.
            float activeW = screenW * 0.71f;
            float activeH = screenH * 0.49f;
            // Widened from 0.285/0.42 - the previous, narrower peek sliver left so little logical
            // width for the neighbor-card label that even short words ("TIDE", "GARDEN") broke
            // mid-word into unreadable single-glyph lines.
            float neighborW = screenW * 0.34f;
            const float peekVisibleFraction = CarouselPeekVisibleFraction;

            float activeLeft = (screenW - activeW) * 0.5f;
            float carouselH = _carousel.resolvedStyle.height;
            float activeTop = Mathf.Max(0f, (carouselH - activeH) * 0.5f);

            ApplyCardRect(_activeCard, activeLeft, activeTop, activeW, activeH);
            ApplyCardRect(_prevCard, activeLeft - neighborW * peekVisibleFraction, activeTop, neighborW, activeH);
            ApplyCardRect(_nextCard, activeLeft + activeW - neighborW * peekVisibleFraction, activeTop, neighborW, activeH);

            // Peek label bars: sized in pixels (not Length.Percent, which resolved unreliably
            // against these absolutely positioned cards and made the label wrap one letter per
            // line) - matching the same visible-sliver math used for the card rects above.
            var prevLabelBar = _prevCard?.Q<VisualElement>("peekLabelBar");
            if (prevLabelBar != null) prevLabelBar.style.width = neighborW * peekVisibleFraction;
            var nextLabelBar = _nextCard?.Q<VisualElement>("peekLabelBar");
            if (nextLabelBar != null) nextLabelBar.style.width = neighborW * (1f - peekVisibleFraction);

            // The active card's title previously used a fixed 36px Display style and severely
            // word-wrapped (even mid-word) on a narrow real phone screen - size it from the card's
            // own resolved width instead.
            var title = _activeCard?.Q<Label>("cardTitle");
            var tagline = _activeCard?.Q<Label>("cardTagline");
            if (title != null)
            {
                bool narrow = activeW < 260f;
                title.style.fontSize = narrow ? 18 : 22;
                if (tagline != null) tagline.style.fontSize = narrow ? 12 : 13;
            }

            // Play pill: computed as an explicit pixel size (not Percent+maxWidth) because at
            // runtime under PanelScaleMode.ConstantPhysicalSize the Percent(61)+maxWidth(340)
            // combination on the Button silently failed to clamp on real Android hardware and
            // rendered as a screen-spanning ellipse instead of a compact pill.
            if (_playButton != null)
            {
                float playW = Mathf.Min(screenW * 0.61f, 340f);
                const float playH = 59f;
                _playButton.style.width = playW;
                _playButton.style.height = playH;
                _playButton.style.borderTopLeftRadius = playH * 0.5f; _playButton.style.borderTopRightRadius = playH * 0.5f;
                _playButton.style.borderBottomLeftRadius = playH * 0.5f; _playButton.style.borderBottomRightRadius = playH * 0.5f;
            }

            // Compact Home floats the bottom nav as an absolute-positioned overlay (see
            // ApplyBreakpointLayout) so it no longer reserves its own flex row - the Play row must
            // reserve that space itself instead, or the floating nav visually covers the Play pill.
            if (_playRow != null)
            {
                bool compactNavOverlay = ResponsiveNavBar.BreakpointForWidth(screenW) == ShellBreakpoint.Compact;
                float navClearance = compactNavOverlay ? 56f + GetBottomSafeAreaInsetLogical() : 0f;
                _playRow.style.marginBottom = VisualTokens.Spacing.L + GetBottomSafeAreaInsetLogical() + navClearance;
            }

            RefreshHomeBackground();

            // Companion + shadow: per the GOLDEN board, the Companion sits beside/below the
            // carousel card's bottom-right corner - not pasted on top of the card art - so its
            // anchor is derived from the active card's own resolved rect (right edge / bottom
            // edge) rather than a fixed screen-fraction that happened to land inside the card.
            var companionTex = WorldArt.CompanionOnDarkNeutralLeft;
            if (companionTex != null && _companion != null)
            {
                float companionWidth = screenW * 0.22f;
                float companionHeight = companionWidth * companionTex.height / companionTex.width;
                // activeTop/activeH are in _carousel's local space, but the Companion is a sibling
                // of `content` anchored directly under _screenLayer - so its bottom edge must be
                // converted into _screenLayer space via worldBound (brand/logo height above the
                // carousel otherwise gets silently dropped, placing the Companion far too high).
                float carouselTopInScreenLayer = _carousel.worldBound.yMin - _screenLayer.worldBound.yMin;
                float cardRight = activeLeft + activeW;
                float cardBottom = carouselTopInScreenLayer + activeTop + activeH;
                float centerX = cardRight + companionWidth * 0.12f;
                float centerY = cardBottom + companionHeight * 0.18f;
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
            WorldArt.HomeBgAspect aspectClass;
            if (screenW <= 0 || screenH <= 0)
            {
                aspectClass = WorldArt.HomeBgAspect.Portrait;
            }
            else
            {
                float aspect = screenW / screenH;
                aspectClass = aspect >= 1.8f ? WorldArt.HomeBgAspect.Ultrawide
                    : aspect >= 1.15f ? WorldArt.HomeBgAspect.Landscape
                    : aspect <= 0.85f ? WorldArt.HomeBgAspect.Portrait
                    : WorldArt.HomeBgAspect.Square;
            }

            string worldId = WorldRegistry.Worlds[_flow.SelectedWorldIndex].Id;
            if (!force && aspectClass == _lastHomeAspect && worldId == _lastHomeWorldId) return;
            _lastHomeAspect = aspectClass;
            _lastHomeWorldId = worldId;

            var tex = WorldArt.GetHomeBackground(worldId, aspectClass);
            if (tex == null) return;

            if (_ambientBack.style.backgroundImage.value.texture == null)
            {
                // First entry into the world selector this session - no previous world to fade from.
                _ambientBack.style.backgroundImage = tex;
                return;
            }

            if (_ambientBack.style.backgroundImage.value.texture == tex) return;

            _ambientFront.style.backgroundImage = tex;
            _ambientFront.style.opacity = 0f;
            if (_ambientAnim != null) StopCoroutine(_ambientAnim);
            _ambientAnim = StartCoroutine(CrossfadeAmbient(tex));
        }

        private IEnumerator CrossfadeAmbient(Texture2D newTex)
        {
            float duration = VisualTokens.MotionMs.Scene / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _ambientFront.style.opacity = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _ambientBack.style.backgroundImage = newTex;
            _ambientFront.style.opacity = 0f;
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
