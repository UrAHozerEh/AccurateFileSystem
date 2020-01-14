using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace AccurateReportSystem
{
    public abstract class ExceptionsChartSeries : ChartSeries
    {
        public override bool IsDrawnInLegend { get; set; } = false;
        public override Color LegendNameColor { get; set; } = Colors.Black;
        public override float Height => (MasterYAxesInfo.Y1LabelFontSize + 2) * NumberOfValues;
        public float LabelFontSize { get; set; } = 8f;
        public abstract int NumberOfValues { get; }
        public float Opacity { get; set; } = 0.75f;
        public float LegendLabelSplit { get; set; } = 0.33f;

        private readonly LegendInfo MasterLegendInfo;
        private readonly YAxesInfo MasterYAxesInfo;
        

        public ExceptionsChartSeries(LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo)
        {
            MasterYAxesInfo = masterYAxesInfo;
            MasterLegendInfo = masterLegendInfo;
            MasterLegendInfo.HorizontalAlignment = CanvasHorizontalAlignment.Left;
        }

        public abstract List<(double Start, double End, Color Color)> GetColorBounds(PageInformation page);
        protected abstract List<(string, Color)> LegendInfo();

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform)
        {

            var colors = GetColorBounds(page);
            using (var _ = session.CreateLayer(Opacity, drawArea))
            {
                foreach (var (Start, End, Color) in colors)
                {
                    var x1 = transform.ToDrawArea(Start);
                    var x2 = transform.ToDrawArea(End);
                    var width = x2 - x1;
                    var drawRect = new Rect(x1, drawArea.Top, width, drawArea.Height);
                    session.FillRectangle(drawRect, Color);
                    session.DrawRectangle(drawRect, Colors.Black);
                }
            }

            var legendWidth = (MasterLegendInfo.Width + MasterYAxesInfo.Y1TotalHeight) * LegendLabelSplit;
            var legendX = drawArea.Left - legendWidth;
            var legendRect = new Rect(legendX, drawArea.Top, legendWidth, drawArea.Height);
            var longest = DrawNames(session, legendRect);
            DrawSquares(session, legendRect, longest);
        }

        private void DrawSquares(CanvasDrawingSession session, Rect drawArea, float longest)
        {
            var horizPadding = 3f;
            var squareWidth = (float)drawArea.Width - longest - (horizPadding * 2);
            var x = (float)drawArea.Left + horizPadding;
            var height = MasterYAxesInfo.Y1LabelFontSize;
            var y = 1f + (float)drawArea.Top;

            using (var _ = session.CreateLayer(Opacity))
            {
                foreach (var (_, color) in LegendInfo())
                {
                    session.FillRectangle(x, y, squareWidth, height, color);
                    session.DrawRectangle(x, y, squareWidth, height, Colors.Black);
                    y += MasterYAxesInfo.Y1LabelFontSize + 2;
                }
            }
        }

        private float DrawNames(CanvasDrawingSession session, Rect drawArea)
        {
            var longest = 0f;
            var layouts = new List<CanvasTextLayout>();
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = MasterLegendInfo.SeriesNameFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                foreach (var (name, _) in LegendInfo())
                {
                    var layout = new CanvasTextLayout(session, name, format, (float)drawArea.Width, 0f);
                    layouts.Add(layout);
                    if (layout.LayoutBounds.Width > longest)
                        longest = (float)layout.DrawBounds.Width + 3f;
                }
                var yOffset = 0f;
                foreach (var layout in layouts)
                {
                    using (var geometry = CanvasGeometry.CreateText(layout))
                    {
                        var translateMatrix = Matrix3x2.CreateTranslation((float)drawArea.Right - longest, (float)drawArea.Top + yOffset);
                        using (var geo = geometry.Transform(translateMatrix))//.Transform(rotationMatrix))
                        {
                            session.FillGeometry(geo, Colors.Black);
                        }
                    }
                    layout.Dispose();
                    yOffset += MasterLegendInfo.SeriesNameFontSize + 2;
                }
            }

            return longest;
        }
    }
}
