using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AccurateReportSystem.AccurateDrawingDevices;
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
        public Color? OutlineColor { get; set; } = Colors.Black;
        public Color? LegendOutlineColor { get; set; } = Colors.Black;

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

        public override void Draw(PageInformation page, AccurateDrawingDevice device, Rect drawArea, TransformInformation1d transform)
        {

            var colors = GetColorBounds(page);

            foreach (var (Start, End, Color) in colors)
            {
                var x1 = transform.ToDrawArea(Start);
                var x2 = transform.ToDrawArea(End);
                var width = x2 - x1;
                var drawRect = new Rect(x1, drawArea.Top, width, drawArea.Height);
                device.FillRectangle(drawRect, Color);
                if (OutlineColor.HasValue)
                    device.DrawRectangle(drawRect, OutlineColor.Value);
            }


            var legendWidth = (MasterLegendInfo.Width + MasterYAxesInfo.Y1TotalHeight) * LegendLabelSplit;
            var legendX = drawArea.Left - legendWidth;
            var legendRect = new Rect(legendX, drawArea.Top, legendWidth, drawArea.Height);
            var longest = DrawNames(device, legendRect);
            DrawSquares(device, legendRect, longest);
        }

        private void DrawSquares(AccurateDrawingDevice device, Rect drawArea, float longest)
        {
            var horizPadding = 3f;
            var squareWidth = (float)drawArea.Width - longest - (horizPadding * 2);
            var x = (float)drawArea.Left + horizPadding;
            var height = MasterYAxesInfo.Y1LabelFontSize;
            var y = 1f + (float)drawArea.Top;

            foreach (var (_, color) in LegendInfo())
            {
                device.FillRectangle(x, y, squareWidth, height, color);
                device.DrawRectangle(x, y, squareWidth, height, Colors.Black);
                y += MasterYAxesInfo.Y1LabelFontSize + 2;
            }
        }

        private float DrawNames(AccurateDrawingDevice device, Rect drawArea)
        {
            var longest = 0f;
            var format = new AccurateTextFormat()
            {
                HorizontalAlignment = AccurateAlignment.Start,
                FontWeight = AccurateFontWeight.Thin,
                WordWrapping = AccurateWordWrapping.WholeWord,
                FontSize = MasterLegendInfo.SeriesNameFontSize
            };
            foreach (var (name, _) in LegendInfo())
            {
                var layout = device.GetTextSize(name, format);
                if (layout.Width > longest)
                    longest = (float)layout.Width;
            }
            longest += 3f;
            var yOffset = 0f;

            foreach (var (name, _) in LegendInfo())
            {
                var layout = device.GetTextSize(name, format);
                device.DrawTextCenteredOn(name, format, Colors.Black, drawArea.Right - longest, drawArea.Top + yOffset);
                yOffset += (float)layout.Height + 2;
            }

            return longest;
        }
    }
}
