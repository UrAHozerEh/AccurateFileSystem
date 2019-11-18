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
    public class ExceptionsChartSeries : ChartSeries
    {
        public override bool IsDrawnInLegend { get; set; } = false;
        public override Color LegendNameColor { get; set; } = Colors.Black;
        public override float Height => (MasterYAxesInfo.Y1LabelFontSize + 2) * 4;
        public float LabelFontSize { get; set; } = 8f;

        public List<(double Footage, double On, double Off)> Data { get; set; }

        public Color OffBelow850Color { get; set; } = Colors.Yellow;
        public Color OnBelow850Color { get; set; } = Colors.Red;
        public Color BothAbove850Color { get; set; } = Colors.Green;
        public Color NoDataColor { get; set; } = Colors.Gray;
        public double MaxDistance { get; set; } = 10.0;
        public float Opacity { get; set; } = 0.75f;

        private readonly LegendInfo MasterLegendInfo;
        private readonly YAxesInfo MasterYAxesInfo;

        public ExceptionsChartSeries(List<(double Footage, double On, double Off)> data, LegendInfo masterLegendInfo, YAxesInfo masterYAxesInfo)
        {
            Data = data;
            MasterYAxesInfo = masterYAxesInfo;
            MasterLegendInfo = masterLegendInfo;
            MasterLegendInfo.HorizontalAlignment = CanvasHorizontalAlignment.Left;
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform)
        {
            var colors = new List<(double Start, double End, Color Color)>();
            (double Start, double end, Color color)? prevData = null;
            (double Footage, Color Color)? firstData = null;
            (double Footage, Color Color)? lastData = null;
            for (int i = 0; i < Data.Count; ++i)
            {
                var (curFoot, on, off) = Data[i];
                var curColor = CheckValues(on, off);
                if (curFoot < page.StartFootage)
                {
                    firstData = (curFoot, curColor);
                    continue;
                }
                if (curFoot > page.EndFootage)
                {
                    lastData = (curFoot, curColor);
                    break;
                }
                if (!prevData.HasValue)
                {
                    prevData = (curFoot, curFoot, curColor);
                    if (curFoot != page.StartFootage && firstData.HasValue)
                    {
                        var (firstFoot, firstColor) = firstData.Value;
                        if (curFoot - firstFoot > MaxDistance)
                        {
                            colors.Add((firstFoot, curFoot, NoDataColor));
                        }
                        else if (firstColor == curColor)
                        {
                            prevData = (firstFoot, curFoot, curColor);
                        }
                        else
                        {
                            colors.Add((firstFoot, curFoot, firstColor));
                        }
                    }
                    continue;
                }

                var (prevStart, prevEnd, prevColor) = prevData.Value;


                if (curFoot - prevEnd > MaxDistance)
                {
                    if (prevStart != prevEnd)
                        colors.Add((prevStart, prevEnd, prevColor));
                    else
                        colors.Add((prevStart, prevStart + (MaxDistance / 2), prevColor));
                    colors.Add((prevEnd, curFoot, NoDataColor));
                    prevData = (curFoot, curFoot, curColor);
                }
                else if (curColor.Equals(prevColor))
                {
                    prevData = (prevStart, curFoot, prevColor);
                }
                else
                {
                    var middleFoot = (prevEnd + curFoot) / 2;
                    colors.Add((prevStart, middleFoot, prevColor));
                    prevData = (middleFoot, curFoot, curColor);
                }
            }
            if (prevData.HasValue)
            {
                var (prevStart, prevEnd, prevColor) = prevData.Value;
                if (lastData.HasValue)
                {
                    var (lastFoot, lastColor) = lastData.Value;
                    if (lastFoot - prevEnd > MaxDistance)
                    {
                        colors.Add((prevEnd, lastFoot, NoDataColor));
                    }
                    else if (prevColor == lastColor)
                    {
                        prevEnd = lastFoot;
                    }
                    else
                    {
                        colors.Add((prevEnd, lastFoot, lastColor));
                    }
                }
                if (prevStart != prevEnd)
                    colors.Add((prevStart, prevEnd, prevColor));
                else
                    colors.Add((prevStart, prevStart + (MaxDistance / 2), prevColor));
            }

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

            var legendWidth = (MasterLegendInfo.WidthDIP + MasterYAxesInfo.Y1TotalHeight) / 3;
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
                session.FillRectangle(x, y, squareWidth, height, OffBelow850Color);
                session.DrawRectangle(x, y, squareWidth, height, Colors.Black);
                y += MasterYAxesInfo.Y1LabelFontSize + 2;
                session.FillRectangle(x, y, squareWidth, height, OnBelow850Color);
                session.DrawRectangle(x, y, squareWidth, height, Colors.Black);
                y += MasterYAxesInfo.Y1LabelFontSize + 2;
                session.FillRectangle(x, y, squareWidth, height, BothAbove850Color);
                session.DrawRectangle(x, y, squareWidth, height, Colors.Black);
                y += MasterYAxesInfo.Y1LabelFontSize + 2;
                session.FillRectangle(x, y, squareWidth, height, NoDataColor);
                session.DrawRectangle(x, y, squareWidth, height, Colors.Black);
            }
        }

        private float DrawNames(CanvasDrawingSession session, Rect drawArea)
        {
            var longest = 0f;
            var names = new List<string>()
            {
                "Off < 850",
                "On < 850",
                "Both > 850",
                "No Data"
            };
            var layouts = new List<CanvasTextLayout>();
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = MasterYAxesInfo.Y1LabelFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                foreach (string name in names)
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
                    yOffset += MasterYAxesInfo.Y1LabelFontSize + 2;
                }
            }

            return longest;
        }

        public Color CheckValues(double on, double off)
        {
            if (on > -0.85)
                return OnBelow850Color;
            if (off > -0.85)
                return OffBelow850Color;
            return BothAbove850Color;
        }
    }
}
