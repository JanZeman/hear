using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell.UI
{
    /// <summary>
    /// Displays the real HEAR brand mark supplied in hear-logo-handoff-v3.1
    /// (Assets/HearApp/Resources/Brand/, archived in full at Art/Reference/brand/v3.1/). Per that
    /// package's explicit instruction, the logo is never hand-recreated or reinterpreted - this
    /// view only sizes/places the approved raster export. Use <see cref="CreateMark"/> for the
    /// with-claim full lockup (Splash only - Home uses the no-claim variant directly, see
    /// ShellUIController) and <see cref="CreateMarkOnly"/> for the compact symbol-only version
    /// (e.g. a quiet nav header).
    /// </summary>
    public static class BrandMarkView
    {
        /// <summary>With-claim full lockup: four-element mark + HEAR wordmark + "Sound opens worlds" claim (no trailing period).</summary>
        public static VisualElement CreateMark(float widthPx)
        {
            var texture = WorldArt.LogoWithClaim;
            return CreateImage(texture, widthPx);
        }

        /// <summary>Symbol-only mark (no wordmark/claim) for compact/quiet contexts.</summary>
        public static VisualElement CreateMarkOnly(float widthPx)
        {
            var texture = WorldArt.MarkOnly;
            return CreateImage(texture, widthPx);
        }

        private static VisualElement CreateImage(Texture2D texture, float widthPx)
        {
            var image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = widthPx,
                    // Cap by percent-of-parent too: a fixed pixel width alone overflowed small
                    // physical phone screens once panel scaling became DPI-aware (confirmed on a
                    // folded Galaxy Z Fold's narrow cover display) - ScaleToFit then letterboxes
                    // cleanly within whatever width this resolves to, so it never distorts.
                    maxWidth = Length.Percent(85),
                    height = texture != null ? widthPx * texture.height / texture.width : widthPx * 0.6f
                }
            };
            if (texture != null)
                image.image = texture;
            else
                Debug.LogWarning("[BrandMarkView] Logo texture not found in Resources/Brand.");
            return image;
        }
    }
}
