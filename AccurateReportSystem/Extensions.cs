using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    public static class Extensions
    {
        public static void BeginFigure(this CanvasPathBuilder path, (float X, float Y) value)
        {
            path.BeginFigure(value.X, value.Y);
        }

        public static void AddLine(this CanvasPathBuilder path, (float X, float Y) value)
        {
            path.AddLine(value.X, value.Y);
        }
    }
}
