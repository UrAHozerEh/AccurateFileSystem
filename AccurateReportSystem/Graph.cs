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
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace AccurateReportSystem
{
    public class Graph : Container
    {
        public double MaximumYValue { get; set; } = 0;
        public double MinimumYValue { get; set; } = -3;
        public double YValueHeight => MaximumYValue - MinimumYValue;
        public bool IsInverted { get; set; } = false;
        public List<GraphSeries> Series { get; set; } = new List<GraphSeries>();
        private List<GeometryInfo> Geometries;
        public CommentSeries CommentSeries { get; set; }
        public GridlineInfo[] Gridlines { get; set; }

        public Graph(Rect drawArea, Container parent, GraphicalReport report) : base(drawArea, parent, report)
        {
            Gridlines = new GridlineInfo[4];
            Gridlines[(int)GridlineName.MinorHorizontal] = new GridlineInfo(50, false, true);
            Gridlines[(int)GridlineName.MajorHorizontal] = new GridlineInfo(100, false, false);
            Gridlines[(int)GridlineName.MinorHorizontal] = new GridlineInfo(0.5, true, true);
            Gridlines[(int)GridlineName.MinorHorizontal] = new GridlineInfo(0.1, true, true);
        }

        public void GenerateGeometries()
        {
            Geometries = new List<GeometryInfo>();
            foreach (var series in Series)
            {
                var geos = series.GetGeomitries();
                foreach (var geo in geos)
                    Geometries.Add(geo);
            }
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session)
        {
            Geometries = new List<GeometryInfo>();
            foreach (var series in Series)
            {
                var geos = series.GetGeomitries();
                Geometries.Add(geos[0]);
            }
            var clipRect = new Rect(page.StartFootage, (float)MinimumYValue, page.Width, (float)YValueHeight);
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

            DrawGridLines(page, session, clipRect, translateMatrix, scaleMatrix);

            foreach (var geoInfo in Geometries)
            {
                var transformedGeo = geoInfo.Geometry.Transform(translateMatrix).Transform(scaleMatrix);
                session.DrawGeometry(transformedGeo, geoInfo.GetCanvasBrush(session));
            }
            if (CommentSeries != null)
            {
                var commentGeoInfo = CommentSeries.GetGeometry(page, DrawArea);
                var style = new CanvasStrokeStyle();
                style.TransformBehavior = CanvasStrokeTransformBehavior.Hairline;
                session.DrawGeometry(commentGeoInfo.Geometry, commentGeoInfo.Color, 1, style);
                //TODO: Canvas Stroke Style should be in Geo Info. Also should have different styles for text and the indicators.
            }
        }

        private void DrawGridLines(PageInformation page, CanvasDrawingSession session, Rect clipRect, Matrix3x2 translateMatrix, Matrix3x2 scaleMatrix)
        {

        }

        public enum GridlineName
        {
            MinorHorizontal = 0, MajorHorizontal = 2, MinorVertical = 1, MajorVertical = 3
        }
    }
}
