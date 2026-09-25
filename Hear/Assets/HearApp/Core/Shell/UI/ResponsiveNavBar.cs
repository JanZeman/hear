using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell.UI
{
    /// <summary>Breakpoint thresholds per docs/14-visual-bible.md section 16.2 /
    /// hear-visual-tokens.json "responsive" (compact &lt; 600, medium 600-999, wide &gt;= 1000).
    /// Exact values remain tunable per docs/13-open-questions.md.</summary>
    public enum ShellBreakpoint { Wide, Medium, Compact }

    public enum NavDestination { Worlds, Results, Settings }

    /// <summary>
    /// Implements docs/10-desktop-behavior.md's Wide-sidebar / Medium-icon-rail /
    /// Compact-bottom-nav rule using the real icon assets from hear-ui-assets-v0.3
    /// (Assets/HearApp/Resources/Icons/). Per the v0.3 upgrade brief, navigation must stay
    /// visually subordinate to the world - this bar is intentionally slim and quiet rather than
    /// a dominant left-quarter dashboard rail.
    /// </summary>
    public sealed class ResponsiveNavBar
    {
        private const float WideWidth = 168f;
        private const float RailWidth = 60f;

        /// <summary>Height of the compact bottom bar, in logical px. Home positions its floating
        /// nav overlay from this (see ShellUIController.ApplyBreakpointLayout/UpdateCarouselForCurrentSize).
        /// Raised from 56 on human direction 2026-09-25 so the (also recently enlarged) icons and
        /// labels have more vertical breathing room, not just a bar sized to fit them tightly.</summary>
        public const float CompactBarHeight = 68f;

        /// <summary>Shared with ShellUIController's nav backdrop extension (the strip filling the
        /// safe-area gesture-inset gap below this bar), so the two always match exactly.</summary>
        public static readonly Color GlassBackgroundColor = new(0.04f, 0.07f, 0.14f, 0.45f);

        // +~9% ("o necelych 10%") on human direction 2026-09-25.
        private const float CompactIconSize = 28.3f;
        private const float RailIconSize = 19.6f;
        private const float CompactLabelFontSize = 12f; // was 11
        private const float CompactSideInset = 8f;

        public VisualElement Root { get; } = new();
        public event Action<NavDestination> DestinationSelected;

        private ShellBreakpoint _current = (ShellBreakpoint)(-1);
        private NavDestination _active = NavDestination.Worlds;
        private bool _overlayMode;

        /// <summary>
        /// Home/world-selector sits over full-bleed world art, so its compact bottom nav must stay
        /// an overlay - subtle darkening only, never the plain opaque Pearl50 slab used on the
        /// plain-background Results/Settings screens - per docs/00-home-design-freeze.md section 5.
        /// </summary>
        public void SetOverlayMode(bool overlay)
        {
            if (_overlayMode == overlay) return;
            _overlayMode = overlay;
            Apply(_current, force: true);
        }

        public static ShellBreakpoint BreakpointForWidth(float width)
        {
            if (width >= VisualTokens.Responsive.WideMin) return ShellBreakpoint.Wide;
            return width >= VisualTokens.Responsive.MediumMin ? ShellBreakpoint.Medium : ShellBreakpoint.Compact;
        }

        public void SetActive(NavDestination destination)
        {
            _active = destination;
            Apply(_current, force: true);
        }

        public void Apply(float viewportWidth) => Apply(BreakpointForWidth(viewportWidth), force: false);

        private void Apply(ShellBreakpoint breakpoint, bool force)
        {
            if (!force && breakpoint == _current) return;
            _current = breakpoint;
            Root.Clear();

            bool wide = breakpoint == ShellBreakpoint.Wide;
            bool compact = breakpoint == ShellBreakpoint.Compact;

            Root.style.flexDirection = compact ? FlexDirection.Row : FlexDirection.Column;
            Root.style.justifyContent = compact ? Justify.SpaceAround : Justify.FlexStart;
            Root.style.alignItems = compact ? Align.Center : Align.FlexStart;
            Root.style.width = compact ? new StyleLength(StyleKeyword.Auto) : (wide ? WideWidth : RailWidth);
            Root.style.height = compact ? CompactBarHeight : new StyleLength(StyleKeyword.Auto);
            Root.style.paddingTop = compact ? 0 : VisualTokens.Spacing.L;
            // The compact bar spaces its three cells across the full width, which put the outer
            // icons at 0.163/0.837 of screen width against the GOLDEN board's 0.181/0.818. An
            // 8-unit inset on each side moves them onto it.
            float sideInset = wide ? VisualTokens.Spacing.S : (compact ? CompactSideInset : 0f);
            Root.style.paddingLeft = sideInset;
            Root.style.paddingRight = sideInset;
            bool compactOverlay = compact && _overlayMode;
            // sources/HEAR-App-UI-Concept-Board.png shows the compact Home nav as a "glass" panel
            // - a translucent dark bar with a thin light rim right at its top edge, not bare
            // floating icons - per human direction 2026-09-25, overriding an earlier pass that
            // read a *different* reference (the GOLDEN board) as having no bar background at all.
            if (compactOverlay)
            {
                Root.style.backgroundColor = GlassBackgroundColor;
                Root.style.borderTopWidth = 1f;
                Root.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
                Root.style.borderTopLeftRadius = VisualTokens.Radius.L;
                Root.style.borderTopRightRadius = VisualTokens.Radius.L;
            }
            else
            {
                Root.style.backgroundColor = VisualTokens.Colors.Pearl50;
                Root.style.borderTopWidth = 0;
                Root.style.borderTopLeftRadius = 0;
                Root.style.borderTopRightRadius = 0;
            }

            AddItem(NavDestination.Worlds, "Worlds", "worlds", wide, compact, compactOverlay);
            AddItem(NavDestination.Results, "Results", "results", wide, compact, compactOverlay);
            AddItem(NavDestination.Settings, "Settings", "settings", wide, compact, compactOverlay);
        }

        private void AddItem(NavDestination destination, string label, string iconName, bool showLabel, bool compact, bool overlay)
        {
            bool isActive = destination == _active;
            Color inactiveTint = overlay ? Color.white : VisualTokens.Colors.Slate400;
            Color activeTint = overlay ? new Color(0.42f, 0.72f, 1f, 1f) : VisualTokens.Colors.AuroraBlue;

            var button = new VisualElement
            {
                style =
                {
                    flexDirection = compact ? FlexDirection.Column : FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    paddingTop = compact ? 4 : VisualTokens.Spacing.S,
                    paddingBottom = compact ? 4 : VisualTokens.Spacing.S,
                    paddingLeft = compact ? 8 : VisualTokens.Spacing.S,
                    paddingRight = compact ? 8 : VisualTokens.Spacing.S,
                    marginBottom = compact ? 0 : 2,
                    borderTopLeftRadius = VisualTokens.Radius.S, borderTopRightRadius = VisualTokens.Radius.S,
                    borderBottomLeftRadius = VisualTokens.Radius.S, borderBottomRightRadius = VisualTokens.Radius.S,
                    // Compact overlay never draws its own pill/box behind the item - the glow
                    // (below) is the only active-state affordance, matching the reference's plain
                    // floating icons with just a soft blue halo on "Worlds".
                    backgroundColor = (isActive && !overlay) ? VisualTokens.Colors.AuroraPearl : Color.clear
                }
            };

            var iconHost = new VisualElement
            {
                style = { width = compact ? CompactIconSize : RailIconSize, height = compact ? CompactIconSize : RailIconSize, alignItems = Align.Center, justifyContent = Justify.Center }
            };

            if (overlay && isActive)
            {
                // A prior pass here measured the reference board's halo as wide/strong (padding
                // out to 17.5px across only 9 hand-tuned layers, composite alpha ~0.5 at the icon
                // edge) and it technically matched that number, but read as a hard-edged blue
                // "blob with rings" rather than a soft bloom on a real device - human feedback
                // 2026-09-25 (side-by-side photo comparison against an earlier, subtler,
                // also-committed-but-overwritten pass). Root cause: UI Toolkit has no real
                // blur, only stacked flat-alpha circles - at a narrow spread the steps between
                // layers are imperceptible, but stretched to 17.5px across only 9 layers each
                // ring's edge becomes individually visible. Fix keeps the halo close to that
                // earlier subtler footprint (max ~9px, well short of 17.5px) but generates many
                // more, much-more-closely-spaced layers from a smooth exponential falloff formula
                // instead of a handful of hand-tuned values, so it still reads as continuous.
                float iconSize = compact ? CompactIconSize : RailIconSize;
                const int layerCount = 14;
                const float maxPadding = 9f;
                const float peakAlpha = 0.085f;
                const float falloffRate = 2.4f;
                var glowTint = new Color(0.2f, 0.6f, 1f, 1f);
                for (int i = 0; i < layerCount; i++)
                {
                    float t = i / (layerCount - 1f);
                    float pad = t * maxPadding;
                    float alpha = peakAlpha * Mathf.Exp(-falloffRate * t);
                    float size = iconSize + pad * 2f;
                    var glow = new VisualElement
                    {
                        style =
                        {
                            position = Position.Absolute, width = size, height = size, left = -pad, top = -pad,
                            borderTopLeftRadius = size * 0.5f, borderTopRightRadius = size * 0.5f,
                            borderBottomLeftRadius = size * 0.5f, borderBottomRightRadius = size * 0.5f,
                            backgroundColor = new Color(glowTint.r, glowTint.g, glowTint.b, alpha)
                        },
                        pickingMode = PickingMode.Ignore
                    };
                    iconHost.Add(glow);
                }
            }

            var icon = new VisualElement
            {
                style =
                {
                    width = compact ? CompactIconSize : RailIconSize, height = compact ? CompactIconSize : RailIconSize,
                    backgroundImage = WorldArt.Icon(iconName),
                    unityBackgroundImageTintColor = isActive ? activeTint : inactiveTint,
                    flexShrink = 0
                }
            };
            iconHost.Add(icon);
            button.Add(iconHost);

            if (showLabel || compact)
            {
                var text = new Label(label)
                {
                    style =
                    {
                        fontSize = compact ? CompactLabelFontSize : VisualTokens.Type.Label.Size,
                        unityFontStyleAndWeight = FontStyle.Normal,
                        color = isActive ? activeTint : inactiveTint,
                        marginLeft = compact ? 0 : VisualTokens.Spacing.S,
                        marginTop = compact ? 2 : 0
                    }
                };
                button.Add(text);
            }

            button.RegisterCallback<ClickEvent>(_ =>
            {
                _active = destination;
                Apply(_current, force: true);
                DestinationSelected?.Invoke(destination);
            });

            Root.Add(button);
        }
    }
}
