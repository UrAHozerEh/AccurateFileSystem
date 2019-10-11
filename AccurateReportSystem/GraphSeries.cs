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

        public GraphSeries(string name, List<(double footage, double value)> values)
        {
            Name = name;
            Values = values;
            Values.Sort((first, second) => first.footage.CompareTo(second.footage));
        }

        public List<Path> GetPaths()
        {
            var paths = new List<Path>();
            if (Values != null && Values.Count != 0)
                paths.Add(GetLinePath());
            if (PointShape != Shape.None && Values != null && Values.Count != 0)
                paths.Add(GetPointPath());
            return paths;
        }

        public List<(CanvasGeometry, Color)> GetGeomitries()
        {
            var paths = new List<(CanvasGeometry, Color)>();
            if (Values != null && Values.Count != 0)
                paths.Add(GetLineGeometry());
            if (PointShape != Shape.None && Values != null && Values.Count != 0)
                paths.Add(GetPointGeometry());
            return paths;
        }

        private (CanvasGeometry, Color) GetLineGeometry()
        {
            return null;
        }

        private (CanvasGeometry, Color) GetPointGeometry()
        {
            return null;
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
            for(int i = 1; i < Values.Count; ++i)
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
