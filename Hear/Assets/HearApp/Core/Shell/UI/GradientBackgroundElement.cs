using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell.UI
{
    /// <summary>
    /// Flat vertical multi-stop gradient, custom-drawn via <see cref="Painter2D"/> as a strip of
    /// thin lerped rectangles (Painter2D's own gradient API needs a texture-space unfamiliar to
    /// this codebase; this is the same "approximate smoothly with many thin layers" technique
    /// already used for glow halos elsewhere in the shell). Used for the Results screen's neutral
    /// "aurora" background on Overall Results - no aurora photo asset has been supplied, and the
    /// visual bible already names these exact colors for this kind of moment, so this stays
    /// on-brand without inventing artwork.
    /// </summary>
    public sealed class GradientBackgroundElement : VisualElement
    {
        private Color[] _stops;

        public GradientBackgroundElement(params Color[] stops)
        {
            _stops = stops != null && stops.Length >= 2 ? stops : new[] { Color.black, Color.black };
            generateVisualContent += OnGenerateVisualContent;
            pickingMode = PickingMode.Ignore;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w <= 0f || h <= 0f) return;

            const int stripes = 40;
            var painter = mgc.painter2D;
            int segments = _stops.Length - 1;
            for (int i = 0; i < stripes; i++)
            {
                float t0 = i / (float)stripes;
                float t1 = (i + 1) / (float)stripes;
                float segF = t0 * segments;
                int seg = Mathf.Clamp(Mathf.FloorToInt(segF), 0, segments - 1);
                float localT = segF - seg;
                Color c = Color.Lerp(_stops[seg], _stops[seg + 1], localT);

                painter.fillColor = c;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, t0 * h));
                painter.LineTo(new Vector2(w, t0 * h));
                painter.LineTo(new Vector2(w, t1 * h + 1f));
                painter.LineTo(new Vector2(0, t1 * h + 1f));
                painter.ClosePath();
                painter.Fill(FillRule.NonZero);
            }
        }
    }
}
