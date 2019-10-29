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
        public int Thickness { get; set; } = 1;
        public Color Color { get; set; }
        public double Offset { get; set; }

        public GridlineInfo(double offset, Color color)
        {
            Color = color;
            Offset = offset;
        }

        public GeometryInfo GetGeometryInfo(double minimumY, double maximumY, bool isYInverted, Rect drawArea)
        {
            var pathBuilder = new CanvasPathBuilder(CanvasDevice.GetSharedDevice());

            var values = GetValues(minimumY, maximumY, isYInverted, drawArea);
            foreach (var (_, location) in values)
            {
                pathBuilder.BeginFigure((float)drawArea.Left, location);
                pathBuilder.AddLine((float)drawArea.Right, location);
                pathBuilder.EndFigure(CanvasFigureLoop.Open);
            }

            return new GeometryInfo
            {
                Geometry = CanvasGeometry.CreatePath(pathBuilder),
                Color = Color
            };
        }

        public GeometryInfo GetGeometryInfo(PageInformation page, Rect drawArea)
        {
            var pathBuilder = new CanvasPathBuilder(CanvasDevice.GetSharedDevice());

            var values = GetValues(page, drawArea);
            foreach (var (_, location) in values)
            {
                pathBuilder.BeginFigure(location, (float)drawArea.Top);
                pathBuilder.AddLine(location, (float)drawArea.Bottom);
                pathBuilder.EndFigure(CanvasFigureLoop.Open);
            }

            return new GeometryInfo
            {
                Geometry = CanvasGeometry.CreatePath(pathBuilder),
                Color = Color
            };
        }

        public List<(double value, float location)> GetValues(double minimumY, double maximumY, bool isYInverted, Rect drawArea)
        {
            var output = new List<(double value, float location)>();

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

            return output;
        }

        public List<(double value, float location)> GetValues(PageInformation page, Rect drawArea)
        {
            var output = new List<(double value, float location)>();
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

            return output;
        }
    }
}
