using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HearApp.Core.Shell.UI
{
    /// <summary>
    /// Minimal reusable line-chart VisualElement, custom-drawn via <see cref="Painter2D"/> since
    /// UI Toolkit has no built-in charting. Backs both the Results screen's hearing-profile
    /// (frequency) chart and its progress/trend chart - same drawing primitives (an optional
    /// shaded band plus one or more value lines with dot markers), different data. Axis labels are
    /// ordinary sibling Labels positioned by the caller, not drawn here, so text stays crisp UI
    /// Toolkit text instead of a vector path.
    /// </summary>
    public sealed class LineChartElement : VisualElement
    {
        public readonly struct Series
        {
            /// <summary>0..1, one per x position, evenly spaced across the chart width.</summary>
            public readonly IReadOnlyList<float> ValuesNormalized;
            public readonly Color LineColor;

            public Series(IReadOnlyList<float> valuesNormalized, Color lineColor)
            {
                ValuesNormalized = valuesNormalized;
                LineColor = lineColor;
            }
        }

        private List<Series> _series = new();
        private float _bandLowNormalized = -1f;
        private float _bandHighNormalized = -1f;
        private Color _bandColor = new(0.30f, 0.78f, 0.55f, 0.14f);

        public LineChartElement()
        {
            generateVisualContent += OnGenerateVisualContent;
            pickingMode = PickingMode.Ignore;
        }

        /// <summary>Optional shaded horizontal band (e.g. the audiogram's "normal range"), in the
        /// same 0..1 normalized space as series values. Pass a negative bound to omit it.</summary>
        public void SetBand(float lowNormalized, float highNormalized, Color? color = null)
        {
            _bandLowNormalized = lowNormalized;
            _bandHighNormalized = highNormalized;
            if (color.HasValue) _bandColor = color.Value;
        }

        public void SetSeries(List<Series> series)
        {
            _series = series ?? new List<Series>();
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w <= 0f || h <= 0f) return;

            var painter = mgc.painter2D;

            if (_bandLowNormalized >= 0f && _bandHighNormalized >= 0f)
            {
                float yLow = h - Mathf.Clamp01(_bandLowNormalized) * h;
                float yHigh = h - Mathf.Clamp01(_bandHighNormalized) * h;
                painter.fillColor = _bandColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, yHigh));
                painter.LineTo(new Vector2(w, yHigh));
                painter.LineTo(new Vector2(w, yLow));
                painter.LineTo(new Vector2(0, yLow));
                painter.ClosePath();
                painter.Fill(FillRule.NonZero);
            }

            foreach (var s in _series)
            {
                int n = s.ValuesNormalized.Count;
                if (n == 0) continue;

                if (n > 1)
                {
                    painter.strokeColor = s.LineColor;
                    painter.lineWidth = 2.5f;
                    painter.lineJoin = LineJoin.Round;
                    painter.lineCap = LineCap.Round;
                    painter.BeginPath();
                    for (int i = 0; i < n; i++)
                    {
                        var p = PointAt(s, i, n, w, h);
                        if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
                    }
                    painter.Stroke();
                }

                painter.fillColor = s.LineColor;
                for (int i = 0; i < n; i++)
                {
                    var p = PointAt(s, i, n, w, h);
                    painter.BeginPath();
                    painter.Arc(p, 3.5f, 0f, 360f, ArcDirection.Clockwise);
                    painter.ClosePath();
                    painter.Fill(FillRule.NonZero);
                }
            }
        }

        private static Vector2 PointAt(Series s, int i, int n, float w, float h)
        {
            float x = n == 1 ? w * 0.5f : w * (i / (float)(n - 1));
            float y = h - Mathf.Clamp01(s.ValuesNormalized[i]) * h;
            return new Vector2(x, y);
        }
    }
}
