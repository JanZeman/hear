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
            Root.style.height = compact ? 56 : new StyleLength(StyleKeyword.Auto);
            Root.style.paddingTop = compact ? 0 : VisualTokens.Spacing.L;
            Root.style.paddingLeft = wide ? VisualTokens.Spacing.S : 0;
            Root.style.paddingRight = wide ? VisualTokens.Spacing.S : 0;
            bool compactOverlay = compact && _overlayMode;
            // GOLDEN board: the compact Home nav has no bar background at all - icons/labels float
            // directly on the scene, with only a soft blue glow behind the active icon. A flat
            // translucent strip (even at low alpha) still read as a "slab" against bright skies, so
            // overlay mode now carries no Root-level background/border of its own.
            Root.style.backgroundColor = compactOverlay ? Color.clear : VisualTokens.Colors.Pearl50;
            Root.style.borderTopWidth = 0;

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
                style = { width = compact ? 26 : 18, height = compact ? 26 : 18, alignItems = Align.Center, justifyContent = Justify.Center }
            };

            if (overlay)
            {
                // Soft glow: several stacked, increasingly large/transparent rounded circles
                // behind the icon - UI Toolkit has no blur/box-shadow, so this approximates one.
                // Two rounds of human feedback (2026-09-25): first a 2-layer version (16-28%
                // opacity) read as "brutal"/naive; a 4-layer version out to +18px was still "much
                // too wide" - the reference mockup's glow, where visible at all, is only a few px
                // past the icon's own edge, not a wide halo. Active keeps the brand blue tint;
                // inactive gets a neutral dark halo that lifts it off the background without
                // implying it is selected.
                Color glowTint = isActive ? new Color(0.35f, 0.65f, 1f, 1f) : new Color(0f, 0f, 0f, 1f);
                float iconSize = compact ? 26f : 18f;
                float[] pad = { 1.5f, 3f, 5f, 8f };
                float[] alpha = isActive
                    ? new[] { 0.05f, 0.03f, 0.018f, 0.008f }
                    : new[] { 0.045f, 0.027f, 0.016f, 0.007f };
                for (int i = 0; i < pad.Length; i++)
                {
                    float size = iconSize + pad[i] * 2f;
                    var glow = new VisualElement
                    {
                        style =
                        {
                            position = Position.Absolute, width = size, height = size, left = -pad[i], top = -pad[i],
                            borderTopLeftRadius = size * 0.5f, borderTopRightRadius = size * 0.5f,
                            borderBottomLeftRadius = size * 0.5f, borderBottomRightRadius = size * 0.5f,
                            backgroundColor = new Color(glowTint.r, glowTint.g, glowTint.b, alpha[i])
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
                    width = compact ? 26 : 18, height = compact ? 26 : 18,
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
                        fontSize = compact ? 10 : VisualTokens.Type.Label.Size,
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
