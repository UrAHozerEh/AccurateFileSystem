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
        public double LegendWidth { get; set; } = 1.5;
        public double LegendWidthDIP => LegendWidth * GraphicalReport.DEFAULT_DPI;

        public Graph()
        {
            Gridlines = new GridlineInfo[4];
            Gridlines[(int)GridlineName.MinorHorizontal] = new GridlineInfo(0.1, false, true);
            Gridlines[(int)GridlineName.MajorHorizontal] = new GridlineInfo(0.5, false, false);
            Gridlines[(int)GridlineName.MinorVertical] = new GridlineInfo(100, true, true);
            Gridlines[(int)GridlineName.MajorVertical] = new GridlineInfo(500, true, false);
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

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect DrawArea)
        {
            var graphBodyDrawArea = new Rect(LegendWidthDIP + DrawArea.X, DrawArea.Y, DrawArea.Width - LegendWidthDIP, DrawArea.Height);

            // Drawing entire graph area border.
            //TODO: Remove
            using (var graphBorder = CanvasGeometry.CreateRectangle(session, DrawArea))
            {
                var style = new CanvasStrokeStyle
                {
                    TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                };
                //session.DrawGeometry(graphBorder, Colors.Orange, 1, style);
            }

            //Drawing graph body border.
            //TODO: Remove
            using (var graphBodyBorder = CanvasGeometry.CreateRectangle(session, graphBodyDrawArea))
            {
                var style = new CanvasStrokeStyle
                {
                    TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                };
                //session.DrawGeometry(graphBodyBorder, Colors.Green, 1, style);
            }

            //Draw everything in the graph body layer. Should prevent bleed over.
            using (var layer = session.CreateLayer(1f, graphBodyDrawArea))
            {
                //TODO: Simplify getting geometries from the graph series. Should only be done once before getting all the pages.
                Geometries = new List<GeometryInfo>();
                foreach (var series in Series)
                {
                    var geos = series.GetGeomitries();
                    Geometries.Add(geos[0]);
                }
                var clipRect = new Rect(page.StartFootage, (float)MinimumYValue, page.Width, (float)YValueHeight);
                var widthScalar = graphBodyDrawArea.Width / page.Width;
                var heightScalar = graphBodyDrawArea.Height / YValueHeight;
                var xTranslate = graphBodyDrawArea.X - clipRect.X;
                var yTranslate = graphBodyDrawArea.Y - clipRect.Y;
                var transform = new CompositeTransform();
                transform.TranslateX = xTranslate;
                transform.TranslateY = yTranslate;
                transform.ScaleX = widthScalar;
                transform.ScaleY = heightScalar;
                var translateMatrix = Matrix3x2.CreateTranslation(new Vector2((float)xTranslate, (float)yTranslate));
                var scaleMatrix = Matrix3x2.CreateScale((float)widthScalar, (float)heightScalar, new Vector2((float)graphBodyDrawArea.X, (float)graphBodyDrawArea.Y));

                DrawGridLines(page, session, graphBodyDrawArea, translateMatrix, scaleMatrix);

                foreach (var geoInfo in Geometries)
                {
                    var transformedGeo = geoInfo.Geometry.Transform(translateMatrix).Transform(scaleMatrix);
                    session.DrawGeometry(transformedGeo, geoInfo.GetCanvasBrush(session));
                }
                if (CommentSeries != null)
                {
                    var commentGeoInfo = CommentSeries.GetGeometry(page, graphBodyDrawArea);
                    var style = new CanvasStrokeStyle();
                    style.TransformBehavior = CanvasStrokeTransformBehavior.Hairline;
                    session.DrawGeometry(commentGeoInfo.Geometry, commentGeoInfo.Color, 1, style);
                    //TODO: Canvas Stroke Style should be in Geo Info. Also should have different styles for text and the indicators.
                }
            }
        }

        private void DrawGridLines(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea, Matrix3x2 translateMatrix, Matrix3x2 scaleMatrix)
        {
            //TODO: Move create layer to main draw. Should draw everything in body inside this using, to make sure there is no weird clipping.
            for (int i = 0; i < 4; ++i)
            {
                var geoInfo = Gridlines[i].GetGeometryInfo(page, MinimumYValue, MaximumYValue);
                var style = new CanvasStrokeStyle
                {
                    TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                };

                var transformedGeo = geoInfo.Geometry.Transform(translateMatrix).Transform(scaleMatrix);
                session.DrawGeometry(transformedGeo, geoInfo.Color, 1, style);
            }
        }

        public enum GridlineName
        {
            MinorHorizontal = 0, MajorHorizontal = 2, MinorVertical = 1, MajorVertical = 3
        }
    }
}
