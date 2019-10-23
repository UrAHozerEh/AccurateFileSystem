using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
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
        public double Y1AxisLabelWidth { get; set; } = 0.25;
        public double Y1AxisLabelWidthDIP => Y1AxisLabelWidth * GraphicalReport.DEFAULT_DPI;
        public double Y1AxisTitleWidth { get; set; } = 0.25;
        public double Y1AxisTitleWidthDIP => Y1AxisTitleWidth * GraphicalReport.DEFAULT_DPI;
        public string Y1AxisTitle { get; set; } = "Volts";
        public double Y2AxisLabelWidth { get; set; } = 0;
        public double Y2AxisLabelWidthDIP => Y2AxisLabelWidth * GraphicalReport.DEFAULT_DPI;
        public double Y2AxisTitleWidth { get; set; } = 0;
        public double Y2AxisTitleWidthDIP => Y2AxisTitleWidth * GraphicalReport.DEFAULT_DPI;
        public string Y2AxisTitle { get; set; } = "";
        public double XAxisLabelHeight { get; set; } = 0.25;
        public double XAxisLabelHeightDIP => XAxisLabelHeight * GraphicalReport.DEFAULT_DPI;
        public double XAxisTitleHeight { get; set; } = 0.25;
        public double XAxisTitleHeightDIP => XAxisTitleHeight * GraphicalReport.DEFAULT_DPI;
        public string XAxisTitle { get; set; } = "Footage";
        private double TotalXValueShift => LegendWidthDIP + Y1AxisLabelWidthDIP + Y1AxisTitleWidthDIP;
        private double TotalWidthShift => TotalXValueShift + Y2AxisLabelWidthDIP + Y2AxisTitleWidthDIP;
        private double TotalYValueShift => 0;
        private double TotalHeightShift => XAxisLabelHeightDIP + XAxisTitleHeightDIP;

        public Graph()
        {
            Gridlines = new GridlineInfo[4];
            Gridlines[(int)GridlineName.MinorHorizontal] = new GridlineInfo(0.1, false, true);
            Gridlines[(int)GridlineName.MajorHorizontal] = new GridlineInfo(0.5, false, false)
            {
                Thickness = 2
            };
            Gridlines[(int)GridlineName.MinorVertical] = new GridlineInfo(100, true, true);
            Gridlines[(int)GridlineName.MajorVertical] = new GridlineInfo(500, true, false)
            {
                Thickness = 2
            };
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
            var graphBodyDrawArea = new Rect(TotalXValueShift + DrawArea.X, TotalYValueShift + DrawArea.Y, DrawArea.Width - TotalWidthShift, DrawArea.Height - TotalHeightShift);

            session.DrawRectangle(graphBodyDrawArea, Colors.Black);

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
                //TODO: Maybe it should actually be drawn per page.
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

                DrawGridLines(page, session, translateMatrix, scaleMatrix);

                foreach (var geoInfo in Geometries)
                {
                    var transformedGeo = geoInfo.Geometry.Transform(translateMatrix).Transform(scaleMatrix);
                    session.DrawGeometry(transformedGeo, geoInfo.GetCanvasBrush(session));
                }
                if (CommentSeries != null)
                {
                    var (commentGeoInfo, lineGeoInfo) = CommentSeries.GetGeometry(page, graphBodyDrawArea, session);
                    var style = new CanvasStrokeStyle
                    {
                        TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                    };
                    session.DrawGeometry(lineGeoInfo.Geometry, lineGeoInfo.Color, 1, style);
                    session.FillGeometry(commentGeoInfo.Geometry, commentGeoInfo.Color);
                    //TODO: Canvas Stroke Style should be in Geo Info. Also should have different styles for text and the indicators.
                }
            }

            DrawAxisLabels(page, session, graphBodyDrawArea);
            DrawAxisTitles(page, session, graphBodyDrawArea);
        }

        private void DrawGridLines(PageInformation page, CanvasDrawingSession session, Matrix3x2 translateMatrix, Matrix3x2 scaleMatrix)
        {
            for (int i = 0; i < 4; ++i)
            {
                var geoInfo = Gridlines[i].GetGeometryInfo(page, MinimumYValue, MaximumYValue);
                var style = new CanvasStrokeStyle
                {
                    TransformBehavior = CanvasStrokeTransformBehavior.Fixed
                };

                var transformedGeo = geoInfo.Geometry.Transform(translateMatrix).Transform(scaleMatrix);
                session.DrawGeometry(transformedGeo, geoInfo.Color, Gridlines[i].Thickness, style);
            }
        }

        private void DrawAxisLabels(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea)
        {

        }

        private void DrawAxisTitles(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea)
        {
            if (XAxisTitleHeight != 0 && !string.IsNullOrWhiteSpace(XAxisTitle))
            {
                var xAxisDrawArea = new Rect(graphBodyDrawArea.X, graphBodyDrawArea.Bottom + XAxisLabelHeightDIP, graphBodyDrawArea.Width, XAxisTitleHeightDIP);
                DrawAxisTitle(XAxisTitle, session, xAxisDrawArea, 20f, 0);
            }
            if(Y1AxisTitleWidth != 0 && !string.IsNullOrWhiteSpace(Y1AxisTitle))
            {
                var y1AxisDrawArea = new Rect(graphBodyDrawArea.X - Y1AxisLabelWidthDIP - Y1AxisTitleWidthDIP, graphBodyDrawArea.Y, Y1AxisTitleWidthDIP, graphBodyDrawArea.Height);
                DrawAxisTitle(Y1AxisTitle, session, y1AxisDrawArea, 20f, 90);
            }
        }

        private void DrawAxisTitle(string title, CanvasDrawingSession session, Rect drawArea, float fontSize, float rotation)
        {
            float width, height;
            if (rotation == 90 || rotation == -90)
            {
                width = (float)drawArea.Height;
                height = (float)drawArea.Width;
            }
            else
            {
                width = (float)drawArea.Width;
                height = (float)drawArea.Height;
            }
            var middleX = (float)(drawArea.X + drawArea.Width / 2);
            var middleY = (float)(drawArea.Y + drawArea.Height / 2);

            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = fontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                using (var layout = new CanvasTextLayout(session, title, format, width, height))
                {

                    using (var geometry = CanvasGeometry.CreateText(layout))
                    {
                        var bounds = layout.LayoutBounds;
                        var boundsMiddleY = (float)(bounds.Y + bounds.Height / 2);
                        var boundsMiddleX = (float)(bounds.X + bounds.Width / 2);
                        var translateY = middleY - boundsMiddleY;
                        var translateX = middleX - boundsMiddleX;
                        var translateMatrix = Matrix3x2.CreateTranslation((float)translateX, (float)translateY);
                        var rotationMatrix = Matrix3x2.CreateRotation((float)(rotation * Math.PI / 180), new Vector2(middleX, middleY));
                        using (var rotatedGeo = geometry.Transform(translateMatrix).Transform(rotationMatrix))
                        {
                            session.FillGeometry(rotatedGeo, Colors.Black);
                        }
                    }

                }
            }
        }
    }

    public enum GridlineName
    {
        MinorHorizontal = 0, MajorHorizontal = 2, MinorVertical = 1, MajorVertical = 3
    }
}
