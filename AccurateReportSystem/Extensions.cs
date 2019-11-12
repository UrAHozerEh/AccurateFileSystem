using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

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

        public static (float X, float Y) GetMiddlePoint(this Rect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(rect.Y + rect.Height / 2);
            return (x, y);
        }

        public static Matrix3x2 CreateTranslateMiddleTo(this Rect rect, (float X, float Y) middle)
        {
            var (boundsMiddleX, boundsMiddleY) = rect.GetMiddlePoint();
            var translateY = middle.Y - boundsMiddleY;
            var translateX = middle.X - boundsMiddleX;
            return Matrix3x2.CreateTranslation(translateX, translateY);
        }

        public static Matrix3x2 CreateTranslateMiddleTo(this Rect rect, Rect other)
        {
            var otherMiddle = other.GetMiddlePoint();
            return rect.CreateTranslateMiddleTo(otherMiddle);
        }

        public static Matrix3x2 CreateRotationAroundMiddle(this Rect rect, float rotationDegrees)
        {
            var (x, y) = rect.GetMiddlePoint();
            var rotationMatrix = Matrix3x2.CreateRotation((float)(rotationDegrees * Math.PI / 180), new Vector2(x, y));
            return rotationMatrix;
        }
    }
}
