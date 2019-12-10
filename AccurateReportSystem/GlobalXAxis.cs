using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace AccurateReportSystem
{
    public class GlobalXAxis : Container
    {
        public GraphicalReport Report { get; set; }
        public XAxisInfo XAxisInfo { get; set; }
        public bool DrawPageInfo { get; set; } = false;
        public string Title { get; set; } = null;
        public float TitleFontSize { get; set; } = 24f;

        public GlobalXAxis(GraphicalReport report, bool isFlippedVertical = false)
        {
            Report = report;
            XAxisInfo = new XAxisInfo(report.XAxisInfo);
            XAxisInfo.IsFlippedVertical = isFlippedVertical;
            XAxisInfo.IsEnabled = true;
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea)
        {
            var graphArea = new Rect(page.StartFootage, 0, page.Width, 0);

            var leftSpace = Report.LegendInfo.Width + Report.YAxesInfo.Y1TotalHeight;
            var rightSpace = Report.YAxesInfo.Y2TotalHeight;
            var width = drawArea.Width - leftSpace - rightSpace;
            var realDrawArea = new Rect(drawArea.Left + leftSpace, drawArea.Top, width, drawArea.Height);

            //TODO: No need to make a 2d transform.
            var transform = new TransformInformation2d(realDrawArea, graphArea, false);

            XAxisInfo.DrawInfo(session, page, transform.GetXTransform(), realDrawArea);
            var pageRect = new Rect(drawArea.Left, drawArea.Top + XAxisInfo.LabelHeight, leftSpace, XAxisInfo.TitleTotalHeight);
            var titleRect = new Rect(drawArea.Left, drawArea.Top - TitleFontSize - 5, drawArea.Width, TitleFontSize);
            if (DrawPageInfo)
                DrawPageCount(page, session, pageRect);
            if(Title != null)
                DrawTitle(page, session, titleRect);
        }

        private void DrawTitle(PageInformation page, CanvasDrawingSession session, Rect drawArea)
        {
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = TitleFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Bold;
                format.FontStyle = FontStyle.Normal;
                using (var layout = new CanvasTextLayout(session, Title, format, (float)drawArea.Width, (float)drawArea.Height))
                {
                    using (var geometry = CanvasGeometry.CreateText(layout))
                    {
                        var bounds = layout.DrawBounds;
                        var translateMatrix = bounds.CreateTranslateMiddleTo(drawArea);
                        using (var rotatedGeo = geometry.Transform(translateMatrix))//.Transform(rotationMatrix))
                        {
                            session.FillGeometry(rotatedGeo, Colors.Black);
                        }
                    }
                }
            }
        }

        private void DrawPageCount(PageInformation page, CanvasDrawingSession session, Rect drawArea)
        {
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = XAxisInfo.TitleFontSize;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;
                var text = $"Page {page.PageNumber} of {page.TotalPages}";
                using (var layout = new CanvasTextLayout(session, text, format, (float)drawArea.Width, (float)drawArea.Height))
                {
                    using (var geometry = CanvasGeometry.CreateText(layout))
                    {
                        var bounds = layout.DrawBounds;
                        var translateMatrix = bounds.CreateTranslateMiddleTo(drawArea);
                        using (var rotatedGeo = geometry.Transform(translateMatrix))//.Transform(rotationMatrix))
                        {
                            session.FillGeometry(rotatedGeo, Colors.Black);
                        }
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
            return XAxisInfo.IsEnabled ? XAxisInfo.TotalHeight : 0;
        }
    }
}
