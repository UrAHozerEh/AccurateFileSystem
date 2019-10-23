using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class GridlineInfo
    {
        public bool IsEnabled { get; set; } = true;
        public bool IsMinor { get; set; }
        public bool IsVertical { get; set; }
        public int Thickness { get; set; } = 1;
        public Color Color { get; set; }
        public double Offset { get; set; }

        public GridlineInfo(double offset, bool isVertical, bool isMinor = false)
        {
            IsVertical = isVertical;
            IsMinor = isMinor;
            Color = isMinor ? Colors.LightGray : Colors.Gray;
            Offset = offset;
        }

        public GeometryInfo GetGeometryInfo(PageInformation page, Rect drawArea, double minimumY, double maximumY, bool isYInverted)
        {
            var pathBuilder = new CanvasPathBuilder(CanvasDevice.GetSharedDevice());

            var values = GetValues(page, drawArea, minimumY, maximumY, isYInverted);
            foreach (var (_, location) in values)
            {
                if (IsVertical)
                {
                    pathBuilder.BeginFigure(location, (float)drawArea.Top);
                    pathBuilder.AddLine(location, (float)drawArea.Bottom);
                }
                else
                {
                    pathBuilder.BeginFigure((float)drawArea.Left, location);
                    pathBuilder.AddLine((float)drawArea.Right, location);
                }
                pathBuilder.EndFigure(CanvasFigureLoop.Open);
            }

            return new GeometryInfo
            {
                Geometry = CanvasGeometry.CreatePath(pathBuilder),
                Color = Color
            };
        }

        public List<(double value, float location)> GetValues(PageInformation page, Rect drawArea, double minimumY, double maximumY, bool isYInverted)
        {
            var output = new List<(double value, float location)>();

            if (IsVertical)
            {
                var numOffsetInStart = (int)(page.StartFootage / Offset);
                var start = numOffsetInStart * Offset;
                var curVal = start;
                while (curVal <= page.EndFootage)
                {
                    if (curVal >= page.StartFootage)
                    {
                        var footageInPage = curVal - page.StartFootage;
                        var pixelToFootageRatio = drawArea.Width / page.Width;
                        var location = pixelToFootageRatio * footageInPage;
                        var finalLoc = (float)Math.Round(location + drawArea.X, GraphicalReport.DIGITS_TO_ROUND);
                        output.Add((curVal, finalLoc));
                    }
                    curVal += Offset;
                }
            }
            else
            {
                var numOffsetInStart = (int)(minimumY / Offset);
                var start = numOffsetInStart * Offset;
                var curVal = start;
                while (curVal <= maximumY)
                {
                    if (curVal >= minimumY)
                    {
                        var height = maximumY - minimumY;
                        var pixelToValueRatio = drawArea.Height / height;
                        var shiftTo0 = 0 - minimumY;
                        var location = pixelToValueRatio * (curVal + shiftTo0);
                        var finalVal = isYInverted ? drawArea.Y + location : drawArea.Bottom - location;
                        var finalLoc = (float)Math.Round(finalVal, GraphicalReport.DIGITS_TO_ROUND);
                        output.Add((curVal, finalLoc));
                    }
                    curVal += Offset;
                }
            }

            return output;
        }
    }
}
