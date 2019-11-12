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
        public bool IsInverted { get; set; } = false;
        public List<GraphSeries> Series { get; set; } = new List<GraphSeries>();
        public CommentSeries CommentSeries { get; set; }
        public XAxisInfo XAxisInfo { get; set; }
        public LegendInfo LegendInfo { get; set; }
        public YAxesInfo YAxesInfo { get; set; }
        private double TotalXValueShift => LegendInfo.WidthDIP + YAxesInfo.Y1TotalHeight;
        private double TotalWidthShift => TotalXValueShift + YAxesInfo.Y2TotalHeight;
        private double TotalYValueShift => XAxisInfo.IsFlippedVertical ? XAxisInfo.TotalHeight : 0;
        public bool DrawTopBorder { get; set; } = true;
        public bool DrawBottomBorder { get; set; } = true;

        public Graph(GraphicalReport report)
        {
            XAxisInfo = new XAxisInfo(report.XAxisInfo);
            YAxesInfo = new YAxesInfo(report.YAxesInfo, "Potential (Volts)", "Depth (inches)");
            LegendInfo = new LegendInfo(report.LegendInfo, "CIS Data");
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect DrawArea)
        {
            var graphBodyDrawArea = new Rect(TotalXValueShift + DrawArea.X, TotalYValueShift + DrawArea.Y, DrawArea.Width - TotalWidthShift, DrawArea.Height - XAxisInfo.TotalHeight);

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
            Rect y1GraphArea = new Rect(page.StartFootage, YAxesInfo.Y1MinimumValue, page.Width, YAxesInfo.Y1ValuesHeight);
            Rect y2GraphArea = new Rect(page.StartFootage, YAxesInfo.Y2MinimumValue, page.Width, YAxesInfo.Y2ValuesHeight);
            TransformInformation y1Transform = new TransformInformation(graphBodyDrawArea, y1GraphArea, YAxesInfo.Y1IsInverted);
            TransformInformation y2Transform = new TransformInformation(graphBodyDrawArea, y2GraphArea, YAxesInfo.Y2IsInverted);

            var (x1, y1) = y1Transform.ToDrawArea(page.StartFootage, -3);
            var (x2, y2) = y1Transform.ToDrawArea(page.EndFootage, 0);
            var (foot1, val1) = y2Transform.ToGraphArea(x1, y1);
            var (foot2, val2) = y2Transform.ToGraphArea(x2, y2);

            //Draw everything in the graph body layer. Should prevent bleed over.
            using (var layer = session.CreateLayer(1f, graphBodyDrawArea))
            {
                //TODO: Simplify getting geometries from the graph series. Should only be done once before getting all the pages.
                //TODO: Maybe it should actually be drawn per page.
                XAxisInfo.DrawGridlines(session, page, graphBodyDrawArea, y1Transform);
                YAxesInfo.DrawGridlines(session, graphBodyDrawArea, y1Transform);

                var clipRect = new Rect(page.StartFootage, (float)YAxesInfo.Y1MinimumValue, page.Width, (float)YAxesInfo.Y1ValuesHeight);
                var widthScalar = graphBodyDrawArea.Width / page.Width;
                var heightScalar = graphBodyDrawArea.Height / YAxesInfo.Y1ValuesHeight;
                var xTranslate = graphBodyDrawArea.X - clipRect.X;
                var yTranslate = graphBodyDrawArea.Y - clipRect.Y;
                var transform = new CompositeTransform();
                transform.TranslateX = xTranslate;
                transform.TranslateY = yTranslate;
                transform.ScaleX = widthScalar;
                transform.ScaleY = heightScalar;
                var translateMatrix = Matrix3x2.CreateTranslation(new Vector2((float)xTranslate, (float)yTranslate));
                var scaleMatrix = Matrix3x2.CreateScale((float)widthScalar, (float)heightScalar, new Vector2((float)graphBodyDrawArea.X, (float)graphBodyDrawArea.Y));

                //DrawGridLines(page, graphBodyDrawArea, session, translateMatrix, scaleMatrix);



                foreach (var series in Series)
                {
                    var curTransform = series.IsY1Axis ? y1Transform : y2Transform;
                    series.Draw(session, page, curTransform);
                }
                if (CommentSeries != null)
                {
                    var (commentGeoInfo, lineGeoInfo) = CommentSeries.GetGeometry(page, graphBodyDrawArea, session);
                    var style = new CanvasStrokeStyle
                    {
                        TransformBehavior = CanvasStrokeTransformBehavior.Hairline
                    };
                    if (lineGeoInfo != null)
                        session.DrawGeometry(lineGeoInfo.Geometry, lineGeoInfo.Color, 1, style);
                    if (commentGeoInfo != null)
                        session.FillGeometry(commentGeoInfo.Geometry, commentGeoInfo.Color);
                    //TODO: Canvas Stroke Style should be in Geo Info. Also should have different styles for text and the indicators.
                }
            }
            var xAxisY = XAxisInfo.IsFlippedVertical ? (graphBodyDrawArea.Top - XAxisInfo.TotalHeight) : graphBodyDrawArea.Bottom;
            var xAxisDrawArea = new Rect(graphBodyDrawArea.Left, xAxisY, graphBodyDrawArea.Width, XAxisInfo.TotalHeight);
            XAxisInfo.DrawInfo(session, page, y1Transform, xAxisDrawArea);
            YAxesInfo.DrawInfo(session, page, y1Transform, y2Transform, graphBodyDrawArea);


            //DrawAxisLabels(page, session, graphBodyDrawArea);
            //DrawAxisTitles(session, graphBodyDrawArea);
            DrawOverlapShadow(page, session, graphBodyDrawArea);
            var legendDrawArea = new Rect(DrawArea.X, DrawArea.Y, LegendInfo.WidthDIP, graphBodyDrawArea.Height);
            DrawLegend(legendDrawArea, session);

            if (DrawTopBorder)
                session.DrawLine((float)DrawArea.Left, (float)DrawArea.Top, (float)DrawArea.Right, (float)DrawArea.Top, Colors.Black, 1);
            if(DrawBottomBorder)
                session.DrawLine((float)DrawArea.Left, (float)DrawArea.Bottom, (float)DrawArea.Right, (float)DrawArea.Bottom, Colors.Black, 1);
        }

        private void DrawOverlapShadow(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea)
        {
            var color = Colors.Black;
            var opacity = 0.25f;
            var pixelToFootRatio = graphBodyDrawArea.Width / page.Width;
            var shadowWidth = (float)Math.Round(pixelToFootRatio * page.Overlap, GraphicalReport.DIGITS_TO_ROUND);
            var startRect = new Rect(graphBodyDrawArea.X, graphBodyDrawArea.Y, shadowWidth, graphBodyDrawArea.Height);
            var endRect = new Rect(graphBodyDrawArea.Right - shadowWidth, graphBodyDrawArea.Y, shadowWidth, graphBodyDrawArea.Height);
            using (var layer = session.CreateLayer(opacity))
            {
                session.FillRectangle(startRect, color);
                session.FillRectangle(endRect, color);
            }
        }

        private void DrawLegend(Rect legendDrawArea, CanvasDrawingSession session)
        {
            //session.DrawRectangle(legendDrawArea, Colors.Orange);
            var height = LegendInfo.NameFontSize + LegendInfo.SeriesNameFontSize * Series.Count;
            var exampleRect = new Rect(legendDrawArea.X, legendDrawArea.Y, legendDrawArea.Width, height);
            //session.DrawRectangle(exampleRect, Colors.Orange);


            var nextHeight = 0f;
            if (LegendInfo.VerticalAlignment == CanvasVerticalAlignment.Center)
                nextHeight = (float)Math.Round(legendDrawArea.Height / 2 - height / 2, GraphicalReport.DIGITS_TO_ROUND);
            else if (LegendInfo.VerticalAlignment == CanvasVerticalAlignment.Bottom)
                nextHeight = (float)(legendDrawArea.Bottom - height);
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = LegendInfo.HorizontalAlignment;
                format.FontSize = LegendInfo.NameFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Bold;
                format.FontStyle = FontStyle.Normal;
                using (var layout = new CanvasTextLayout(session, LegendInfo.Name, format, (float)legendDrawArea.Width, 0))
                {
                    //var translateX = (float)Math.Round(legendDrawArea.X, GraphicalReport.DIGITS_TO_ROUND);
                    //var translateY = (float)Math.Round(legendDrawArea.Y, GraphicalReport.DIGITS_TO_ROUND);
                    //var translate = Matrix3x2.CreateTranslation(translateX, translateY);
                    using (var geo = CanvasGeometry.CreateText(layout))
                    {
                        session.FillGeometry(geo, (float)legendDrawArea.X, (float)legendDrawArea.Y + nextHeight, LegendInfo.NameColor);
                    }
                    nextHeight += LegendInfo.NameFontSize;
                }
            }
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = LegendInfo.HorizontalAlignment;
                format.FontSize = LegendInfo.SeriesNameFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Bold;
                format.FontStyle = FontStyle.Normal;
                foreach (var series in Series)
                {
                    using (var layout = new CanvasTextLayout(session, series.Name, format, (float)legendDrawArea.Width, 0))
                    {
                        //var translateX = (float)Math.Round(legendDrawArea.X, GraphicalReport.DIGITS_TO_ROUND);
                        //var translateY = (float)Math.Round(legendDrawArea.Y, GraphicalReport.DIGITS_TO_ROUND);
                        //var translate = Matrix3x2.CreateTranslation(translateX, translateY);
                        using (var geo = CanvasGeometry.CreateText(layout))
                        {
                            session.FillGeometry(geo, (float)legendDrawArea.X, (float)legendDrawArea.Y + nextHeight, series.LineColor);
                            using(var strokeStyle = new CanvasStrokeStyle())
                            {
                                strokeStyle.TransformBehavior = CanvasStrokeTransformBehavior.Hairline;
                                session.DrawGeometry(geo, (float)legendDrawArea.X, (float)legendDrawArea.Y + nextHeight, Colors.Black, 1, strokeStyle);
                            }

                        }
                        nextHeight += LegendInfo.SeriesNameFontSize;
                    }
                }
            }
        }

        public override double GetRequestedWidth()
        {
            return double.MaxValue;
        }

        public override double GetRequestedHeight()
        {
            return double.MaxValue;
        }
    }

    public enum GridlineName
    {
        MinorHorizontal = 0, MajorHorizontal = 2, MinorVertical = 1, MajorVertical = 3
    }
}
