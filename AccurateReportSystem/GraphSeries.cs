using AccurateReportSystem.AccurateDrawingDevices;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;


namespace AccurateReportSystem
{
    public class GraphSeries : Series
    {
        public Color FillColor { get; set; } = Colors.Black;
        public Color LineColor { get; set; } = Colors.Black;
        public int LineThickness { get; set; } = 2;
        public Color PointColor { get; set; } = Colors.Black;
        public Shape PointShape { get; set; } = Shape.None;
        public List<(double footage, double value)> Values { get; set; }
        public Type GraphType { get; set; } = Type.Line;
        public double MaxDrawDistance { get; set; } = 10;
        public bool IsY1Axis { get; set; } = true;
        public override bool IsDrawnInLegend { get; set; } = true;
        public float ShapeRadius { get; set; } = 3f;
        public override Color LegendNameColor
        {
            get
            {
                if (legendNameColor.HasValue)
                    return legendNameColor.Value;
                switch (GraphType)
                {
                    case Type.Line:
                        return LineColor;
                    case Type.Bar:
                        return FillColor;
                    case Type.Point:
                        return PointColor;
                    default:
                        return Colors.Black;
                }
            }
            set
            {
                legendNameColor = value;
            }
        }
        private Color? legendNameColor = null;
        public float Opcaity { get; set; } = 1f;

        public GraphSeries(string name, List<(double footage, double value)> values)
        {
            Name = name;
            Values = values;
            Values.Sort((first, second) => first.footage.CompareTo(second.footage));
        }

        public virtual void Draw(AccurateDrawingDevice device, PageInformation page, TransformInformation2d transform)
        {
            if (Values == null || Values.Count == 0)
            {
                return;
            }
            if (GraphType == Type.Line)
            {
                DrawLineGeometry(device, page, transform);
            }
            if (PointShape != Shape.None)
            {
                DrawPoints(device, page, transform);
            }
        }

        protected void DrawLineGeometry(AccurateDrawingDevice device, PageInformation page, TransformInformation2d transform)
        {
            if (Values == null || Values.Count == 0)
                return;
            var hasBegun = false;
            (double footage, double value)? last = null;
            (double footage, double value)? next = null;

            //for (int i = 0; i < Values.Count; ++i)
            //{
            //    var (footage, value) = Values[i];
            //    if (i + 1 < Values.Count)
            //        next = Values[i + 1];
            //    if (footage < page.StartFootage)
            //    {
            //        last = (footage, value);
            //        continue;
            //    }
            //    if (!hasBegun)
            //    {
            //        if (footage >= page.StartFootage)
            //        {
            //            if (last.HasValue)
            //            {
            //                pathBuilder.BeginFigure(transform.ToDrawArea(last.Value));
            //            }
            //            else
            //            {
            //                pathBuilder.BeginFigure(transform.ToDrawArea(footage, value));
            //            }
            //            hasBegun = true;
            //        }
            //    }
            //    if (hasBegun)
            //    {
            //        var lastFootDiff = footage - (last?.footage ?? 0);
            //        var nextFootDiff = (next?.footage ?? 0) - footage;
            //        var drawFromLast = last.HasValue && lastFootDiff <= MaxDrawDistance;
            //        var drawToNext = next.HasValue && nextFootDiff <= MaxDrawDistance;
            //        if (drawFromLast)
            //        {
            //            pathBuilder.AddLine(transform.ToDrawArea(footage, value));
            //        }
            //        else if (drawToNext)
            //        {
            //            pathBuilder.EndFigure(CanvasFigureLoop.Open);
            //            pathBuilder.BeginFigure(transform.ToDrawArea(footage, value));
            //        }
            //        else
            //        {
            //            //TODO: Gotta draw the single stranded points.
            //            pathBuilder.EndFigure(CanvasFigureLoop.Open);
            //            var pretendOffset = Math.Round(MaxDrawDistance / 2, GraphicalReport.DIGITS_TO_ROUND); // Doesn't really fit, but why not.
            //            if (last.HasValue)
            //            {
            //                var lastValue = last.Value.value;
            //                var pretendFoot = footage - pretendOffset;
            //                var lastValueDiff = value - lastValue;

            //                var lastValueDiffPerFoot = lastValueDiff / lastFootDiff;
            //                var pretendFootDiff = lastFootDiff - pretendOffset;

            //                var pretendValueOffset = Math.Round(lastValueDiffPerFoot * pretendFootDiff, GraphicalReport.DIGITS_TO_ROUND); // Again doesn't fit, but oh well.
            //                var pretendValue = lastValue + pretendValueOffset;

            //                pathBuilder.BeginFigure(transform.ToDrawArea(pretendFoot, pretendValue));
            //                pathBuilder.AddLine(transform.ToDrawArea(footage, value));
            //                pathBuilder.EndFigure(CanvasFigureLoop.Open);
            //            }
            //            if (next.HasValue)
            //            {
            //                var nextValue = next.Value.value;
            //                var pretendFoot = footage + pretendOffset;
            //                var nextValueDiff = value - nextValue;

            //                var nextValueDiffPerFoot = nextValueDiff / nextFootDiff;
            //                var pretendFootDiff = nextFootDiff - pretendOffset;

            //                var pretendValueOffset = Math.Round(nextValueDiffPerFoot * pretendFootDiff, GraphicalReport.DIGITS_TO_ROUND); // Again doesn't fit, but oh well.
            //                var pretendValue = nextValue + pretendValueOffset;

            //                pathBuilder.BeginFigure(transform.ToDrawArea(pretendFoot, pretendValue));
            //                pathBuilder.AddLine(transform.ToDrawArea(footage, value));
            //                pathBuilder.EndFigure(CanvasFigureLoop.Open);
            //            }
            //            pathBuilder.BeginFigure(transform.ToDrawArea(footage, value));
            //        }
            //    }
            //    last = (footage, value);
            //}
            throw new NotImplementedException("Need to add logic to draw series lines.");

        }

        protected void DrawPoints(AccurateDrawingDevice device, PageInformation page, TransformInformation2d transform)
        {
            if (Values == null || Values.Count == 0 || PointShape == Shape.None)
                return;
            for (int i = 0; i < Values.Count; ++i)
            {
                var (footage, value) = Values[i];
                if (footage < page.StartFootage)
                    continue;
                if (footage > page.EndFootage)
                    break;
                var (x, y) = transform.ToDrawArea(footage, value);
                switch (PointShape)
                {
                    case Shape.Square:
                        var rect = new Rect() { X = x - ShapeRadius, Y = y - ShapeRadius, Height = ShapeRadius * 2, Width = ShapeRadius * 2 };
                        device.FillRectangle(rect, PointColor);
                        device.DrawRectangle(rect, PointColor);
                        break;
                    case Shape.Circle:
                        device.FillCircle(x, y, ShapeRadius, PointColor);
                        device.DrawCircle(x, y, ShapeRadius, Colors.Black);
                        break;
                    case Shape.Triangle:
                        device.FillTriangle(x, y, ShapeRadius, PointColor);
                        device.DrawTriangle(x, y, ShapeRadius, Colors.Black);
                        break;
                    case Shape.InvertedTriangle:
                        break;
                    case Shape.Rectangle:
                        break;
                    default:
                        break;
                }
            }
        }

        public enum Type
        {
            Line, Bar, Point
        }
        public enum Shape
        {
            Square, Circle, Triangle, InvertedTriangle, Rectangle, None
        }
    }
}
