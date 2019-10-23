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
        public bool IsVertical { get; set; }
        public int Thickness { get; set; } = 1;
        public Color Color { get; set; }
        public GridlineTickMarkStyle TickMarkStyle { get; set; }
        public double Offset { get; set; }

        public GridlineInfo(double offset, bool isVertical, bool isMinor = false)
        {
            IsVertical = isVertical;
            Color = isMinor ? Colors.LightGray : Colors.Gray;
            TickMarkStyle = isMinor ? GridlineTickMarkStyle.None : GridlineTickMarkStyle.Cross;
            Offset = offset;
        }

        public GeometryInfo GetGeometryInfo(PageInformation page, double minimumY, double maximumY)
        {
            var pathBuilder = new CanvasPathBuilder(CanvasDevice.GetSharedDevice());

            if (IsVertical)
            {
                var start = page.StartFootage - (page.StartFootage % Offset);
                var numLines = (int)(page.Width / Offset);
                for (int i = 0; i <= numLines; ++i)
                {
                    var current = start + i * Offset;
                    pathBuilder.BeginFigure((float)current, (float)minimumY);
                    pathBuilder.AddLine((float)current, (float)maximumY);
                    pathBuilder.EndFigure(CanvasFigureLoop.Open);
                }
            }
            else
            {
                var height = maximumY - minimumY;
                var start = ((int)(minimumY / Offset) * Offset);
                var numLines = (int)(height / Offset);
                for (int i = 0; i <= numLines; ++i)
                {
                    var current = start + i * Offset;
                    pathBuilder.BeginFigure((float)page.StartFootage, (float)current);
                    pathBuilder.AddLine((float)page.EndFootage, (float)current);
                    pathBuilder.EndFigure(CanvasFigureLoop.Open);
                }
            }

            return new GeometryInfo
            {
                Geometry = CanvasGeometry.CreatePath(pathBuilder),
                Color = Color
            };
        }
    }

    public enum GridlineTickMarkStyle
    {
        None, Cross
    }
}
