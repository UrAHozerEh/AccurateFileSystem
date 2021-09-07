using AccurateReportSystem.AccurateDrawingDevices;
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
        private double TotalXValueShift => LegendInfo.Width + YAxesInfo.Y1TotalHeight;
        private double TotalWidthShift => TotalXValueShift + YAxesInfo.Y2TotalHeight;
        private double TotalYValueShift => XAxisInfo.IsFlippedVertical ? XAxisInfo.TotalHeight : 0;
        public bool DrawTopBorder { get; set; } = true;
        public bool DrawBottomBorder { get; set; } = true;

        public Graph(GraphicalReport report)
        {
            XAxisInfo = new XAxisInfo(report.XAxisInfo);
            YAxesInfo = new YAxesInfo(report.YAxesInfo, "Potential (Volts)", "Depth (Inches)");
            LegendInfo = new LegendInfo(report.LegendInfo, "CIS Data");
        }

        public override void Draw(PageInformation page, AccurateDrawingDevice device, Rect DrawArea)
        {
            var graphBodyDrawArea = new Rect(TotalXValueShift + DrawArea.X, TotalYValueShift + DrawArea.Y, DrawArea.Width - TotalWidthShift, DrawArea.Height - XAxisInfo.TotalHeight);

            device.DrawRectangle(graphBodyDrawArea, Colors.Black);

            Rect y1GraphArea = new Rect(page.StartFootage, YAxesInfo.Y1MinimumValue, page.Width, YAxesInfo.Y1ValuesHeight);
            Rect y2GraphArea = new Rect(page.StartFootage, YAxesInfo.Y2MinimumValue, page.Width, YAxesInfo.Y2ValuesHeight);
            TransformInformation2d y1Transform = new TransformInformation2d(graphBodyDrawArea, y1GraphArea, YAxesInfo.Y1IsInverted);
            TransformInformation2d y2Transform = new TransformInformation2d(graphBodyDrawArea, y2GraphArea, YAxesInfo.Y2IsInverted);

            var (x1, y1) = y1Transform.ToDrawArea(page.StartFootage, -3);
            var (x2, y2) = y1Transform.ToDrawArea(page.EndFootage, 0);
            var (foot1, val1) = y2Transform.ToGraphArea(x1, y1);
            var (foot2, val2) = y2Transform.ToGraphArea(x2, y2);

            //TODO: Simplify getting geometries from the graph series. Should only be done once before getting all the pages.
            //TODO: Maybe it should actually be drawn per page.
            XAxisInfo.DrawGridlines(device, page, graphBodyDrawArea, y1Transform.GetXTransform());
            YAxesInfo.DrawGridlines(device, graphBodyDrawArea, y1Transform);

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
                series.Draw(device, page, curTransform);
            }

            //TODO: Comment should draw itself. Should have a way to order drawing of everything (gridlines, series, shadow, comments, comment backdrop, etc.)
            if (CommentSeries != null)
            {
                var (commentGeoInfo, lineGeoInfo, backdropGeo) = CommentSeries.GetGeometry(page, graphBodyDrawArea, device);
                var style = new CanvasStrokeStyle
                {

                };
                if (backdropGeo != null)
                {
                    using (var _ = device.CreateLayer(CommentSeries.BackdropOpacity))
                        device.FillGeometry(backdropGeo, CommentSeries.BackdropColor);
                }
                if (lineGeoInfo != null)
                    device.DrawGeometry(lineGeoInfo.Geometry, lineGeoInfo.Color, 1, style);
                if (commentGeoInfo != null)
                    device.FillGeometry(commentGeoInfo.Geometry, commentGeoInfo.Color);

                //TODO: Canvas Stroke Style should be in Geo Info. Also should have different styles for text and the indicators.
            }

            DrawOverlapShadow(page, device, graphBodyDrawArea);

            var xAxisY = XAxisInfo.IsFlippedVertical ? (graphBodyDrawArea.Top - XAxisInfo.TotalHeight) : graphBodyDrawArea.Bottom;
            var xAxisDrawArea = new Rect(graphBodyDrawArea.Left, xAxisY, graphBodyDrawArea.Width, XAxisInfo.TotalHeight);
            XAxisInfo.DrawInfo(device, page, y1Transform.GetXTransform(), xAxisDrawArea);
            YAxesInfo.DrawInfo(device, page, y1Transform, y2Transform, graphBodyDrawArea);


            //DrawAxisLabels(page, session, graphBodyDrawArea);
            //DrawAxisTitles(session, graphBodyDrawArea);

            var legendWidth = LegendInfo.Width + (YAxesInfo.Y1IsDrawn ? 0 : YAxesInfo.Y1TotalHeight);
            var legendDrawArea = new Rect(DrawArea.X, DrawArea.Y, legendWidth, graphBodyDrawArea.Height);
            LegendInfo.Draw(device, Series, legendDrawArea);

            if (DrawTopBorder)
                device.DrawLine((float)DrawArea.Left, (float)DrawArea.Top, (float)DrawArea.Right, (float)DrawArea.Top, Colors.Black, 1);
            if (DrawBottomBorder)
                device.DrawLine((float)DrawArea.Left, (float)DrawArea.Bottom, (float)DrawArea.Right, (float)DrawArea.Bottom, Colors.Black, 1);
        }

        private void DrawOverlapShadow(PageInformation page, CanvasDrawingSession session, Rect graphBodyDrawArea)
        {
            var color = XAxisInfo.OverlapColor;
            var opacity = XAxisInfo.OverlapOpacity;
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
