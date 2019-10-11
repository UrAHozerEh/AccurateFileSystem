using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace AccurateReportSystem
{
    public class Graph : Container
    {
        public bool HasMinorHorizontalLines { get; set; } = false;
        public bool HasMajorHorizontalLines { get; set; } = true;
        public bool HasMinorVerticalLines { get; set; } = false;
        public bool HasMajorVerticalLines { get; set; } = true;
        public double MaximumYValue { get; set; } = 0;
        public double MinimumYValue { get; set; } = -3;
        public double YValueHeight => MaximumYValue - MinimumYValue;
        public bool IsInverted { get; set; } = false;
        public List<GraphSeries> Series { get; set; } = new List<GraphSeries>();
        private List<Path> Paths;
        private List<(CanvasGeometry, ICanvasBrush)> Geometries;

        public Graph(Rect drawArea, Container parent, GraphicalReport report) : base(drawArea, parent, report)
        {

        }

        public void GeneratePaths()
        {
            Paths = new List<Path>();
            foreach (var series in Series)
            {
                var curPaths = series.GetPaths();
                foreach (var path in curPaths)
                    Paths.Add(path);
            }
        }

        public void GenerateGeometries()
        {
            Geometries = new List<(CanvasGeometry, ICanvasBrush)>();
            foreach (var series in Series)
            {
                var geos = series.GetGeomitries();
                foreach (var geo in geos)
                    Geometries.Add(geo);
            }
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session)
        {
            GeneratePaths();
            GenerateGeometries();
            var newClip = CanvasGeometry.CreateRectangle(session, (float)page.StartFootage, (float)MinimumYValue, (float)page.Width, (float)YValueHeight);
            var clip = new RectangleGeometry();
            var clipRect = new Rect(page.StartFootage, -3, page.Width, 3);
            clip.Rect = clipRect;
            var widthScalar = DrawArea.Width / clipRect.Width;
            var heightScalar = DrawArea.Height / clipRect.Height;
            var xTranslate = DrawArea.X - clipRect.X;
            var yTranslate = DrawArea.Y - clipRect.Y;
            var transform = new CompositeTransform();
            transform.TranslateX = xTranslate;
            transform.TranslateY = yTranslate;
            transform.ScaleX = widthScalar;
            transform.ScaleY = heightScalar;
            var translateMatrix = Matrix3x2.CreateTranslation(new Vector2((float)xTranslate, (float)yTranslate));
            var scaleMatrix = Matrix3x2.CreateScale((float)widthScalar, (float)heightScalar, new Vector2());
            foreach (var path in Paths)
            {
                path.Clip = clip;
                path.RenderTransform = transform;
                Report.Children.Add(path);
            }
            foreach (var (geo, brush) in Geometries)
            {
                var clippedGeo = geo.CombineWith(newClip, Matrix3x2.Identity, CanvasGeometryCombine.Intersect);
                var transformedGeo = clippedGeo.Transform(translateMatrix).Transform(scaleMatrix);
                session.DrawGeometry(geo, brush);
            }
        }
    }
}
