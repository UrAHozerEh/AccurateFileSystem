using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;


namespace AccurateReportSystem
{
    public class GraphSeries
    {
        public string Name { get; set; }
        public Color LineColor { get; set; } = Colors.Black;
        public int LineThickness { get; set; } = 2;
        public Color PointColor { get; set; } = Colors.Black;
        public Shape PointShape { get; set; } = Shape.None;
        public List<(double footage, double value)> Values { get; set; }
        public Type GraphType { get; set; } = Type.Line;
        public double MaxDrawDistance { get; set; } = 10;
        public bool IsY1Axis { get; set; } = true;

        public GraphSeries(string name, List<(double footage, double value)> values)
        {
            Name = name;
            Values = values;
            Values.Sort((first, second) => first.footage.CompareTo(second.footage));
        }

        public virtual void Draw(CanvasDrawingSession session, PageInformation page, TransformInformation transform)
        {
            if (Values == null || Values.Count == 0)
                return;

            if (GraphType == Type.Line)
                DrawLineGeometry(session, page, transform);
            if (PointShape != Shape.None)
                DrawPoints(session, page, transform);
        }

        private void DrawLineGeometry(CanvasDrawingSession session, PageInformation page, TransformInformation transform)
        {
            if (Values == null || Values.Count == 0)
                return;
            var hasBegun = false;
            (double footage, double value)? last = null;
            (double footage, double value)? next = null;

            using (var pathBuilder = new CanvasPathBuilder(session))
            {
                for (int i = 0; i < Values.Count; ++i)
                {
                    var (footage, value) = Values[i];
                    if (i + 1 < Values.Count)
                        next = Values[i + 1];
                    if (footage < page.StartFootage)
                    {
                        last = (footage, value);
                        continue;
                    }
                    if (!hasBegun)
                    {
                        if (footage >= page.StartFootage)
                        {
                            if (last.HasValue)
                            {
                                pathBuilder.BeginFigure(transform.ToDrawArea(last.Value));
                            }
                            else
                            {
                                pathBuilder.BeginFigure(transform.ToDrawArea(footage, value));
                            }
                            hasBegun = true;
                        }
                    }
                    if (hasBegun)
                    {
                        var lastFootDiff = footage - (last?.footage ?? 0);
                        var nextFootDiff = (next?.footage ?? 0) - footage;
                        var drawFromLast = last.HasValue && lastFootDiff <= MaxDrawDistance;
                        var drawToNext = next.HasValue && nextFootDiff <= MaxDrawDistance;
                        if (drawFromLast)
                        {
                            pathBuilder.AddLine(transform.ToDrawArea(footage, value));
                        }
                        else if (drawToNext)
                        {
                            pathBuilder.EndFigure(CanvasFigureLoop.Open);
                            pathBuilder.BeginFigure(transform.ToDrawArea(footage, value));
                        }
                        else
                        {
                            //TODO: Gotta draw the single stranded points.
                            pathBuilder.EndFigure(CanvasFigureLoop.Open);
                            var pretendOffset = Math.Round(MaxDrawDistance / 2, GraphicalReport.DIGITS_TO_ROUND); // Doesn't really fit, but why not.
                            if (last.HasValue)
                            {
                                var lastValue = last.Value.value;
                                var pretendFoot = footage - pretendOffset;
                                var lastValueDiff = value - lastValue;

                                var lastValueDiffPerFoot = lastValueDiff / lastFootDiff;
                                var pretendFootDiff = lastFootDiff - pretendOffset;

                                var pretendValueOffset = Math.Round(lastValueDiffPerFoot * pretendFootDiff, GraphicalReport.DIGITS_TO_ROUND); // Again doesn't fit, but oh well.
                                var pretendValue = lastValue + pretendValueOffset;

                                pathBuilder.BeginFigure(transform.ToDrawArea(pretendFoot, pretendValue));
                                pathBuilder.AddLine(transform.ToDrawArea(footage, value));
                                pathBuilder.EndFigure(CanvasFigureLoop.Open);
                            }
                            if (next.HasValue)
                            {
                                var nextValue = next.Value.value;
                                var pretendFoot = footage + pretendOffset;
                                var nextValueDiff = value - nextValue;

                                var nextValueDiffPerFoot = nextValueDiff / nextFootDiff;
                                var pretendFootDiff = nextFootDiff - pretendOffset;

                                var pretendValueOffset = Math.Round(nextValueDiffPerFoot * pretendFootDiff, GraphicalReport.DIGITS_TO_ROUND); // Again doesn't fit, but oh well.
                                var pretendValue = nextValue + pretendValueOffset;

                                pathBuilder.BeginFigure(transform.ToDrawArea(pretendFoot, pretendValue));
                                pathBuilder.AddLine(transform.ToDrawArea(footage, value));
                                pathBuilder.EndFigure(CanvasFigureLoop.Open);
                            }
                            pathBuilder.BeginFigure(transform.ToDrawArea(footage, value));
                        }
                    }
                    last = (footage, value);
                }
                pathBuilder.EndFigure(CanvasFigureLoop.Open);
                using(var geometry = CanvasGeometry.CreatePath(pathBuilder))
                {
                    var style = new CanvasStrokeStyle
                    {
                        TransformBehavior = CanvasStrokeTransformBehavior.Fixed
                    };
                    session.DrawGeometry(geometry, LineColor, LineThickness, style);
                }
            }
        }

        private void DrawPoints(CanvasDrawingSession session, PageInformation page, TransformInformation transform)
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
                session.FillCircle(x, y, 3, PointColor);
            }
        }

        private Path GetLinePath()
        {
            if (Values == null || Values.Count == 0)
                return null;
            var path = new Path();
            path.StrokeThickness = LineThickness;
            path.Stroke = new SolidColorBrush(LineColor);
            var geomitry = new PathGeometry();
            var figure = new PathFigure();
            figure.IsClosed = false;
            figure.StartPoint = GetPoint(Values[0]);
            var pathSegments = new PathSegmentCollection();
            var curSegment = new PolyLineSegment();
            for (int i = 1; i < Values.Count; ++i)
            {
                curSegment.Points.Add(GetPoint(Values[i]));
            }
            pathSegments.Add(curSegment);
            figure.Segments = pathSegments;
            return path;
        }

        private Path GetPointPath()
        {
            if (PointShape == Shape.None || Values == null || Values.Count == 0)
                return null;
            var path = new Path();



            return path;
        }

        private Point GetPoint((double footage, double value) read)
        {
            return new Point(read.footage, read.value);
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
