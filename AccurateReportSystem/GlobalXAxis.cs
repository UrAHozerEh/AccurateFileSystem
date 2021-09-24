using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AccurateReportSystem.AccurateDrawingDevices;
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

        public override void Draw(PageInformation page, AccurateDrawingDevice device, Rect drawArea)
        {
            var graphArea = new Rect(page.StartFootage, 0, page.Width, 0);

            var leftSpace = Report.LegendInfo.Width + Report.YAxesInfo.Y1TotalHeight;
            var rightSpace = Report.YAxesInfo.Y2TotalHeight;
            var width = drawArea.Width - leftSpace - rightSpace;
            var realDrawArea = new Rect(drawArea.Left + leftSpace, drawArea.Top, width, drawArea.Height);

            //TODO: No need to make a 2d transform.
            var transform = new TransformInformation2d(realDrawArea, graphArea, false);

            XAxisInfo.DrawInfo(device, page, transform.GetXTransform(), realDrawArea);
            var pageRect = new Rect(drawArea.Left, drawArea.Top + XAxisInfo.LabelHeight, leftSpace, XAxisInfo.TitleTotalHeight);
            var titleRect = new Rect(drawArea.Left, drawArea.Top - TitleFontSize - 5, drawArea.Width, TitleFontSize);
            if (DrawPageInfo)
            {
                DrawPageCount(page, device, pageRect);
            }

            if (Title != null)
            {
                DrawTitle(page, device, titleRect);
            }
        }

        private void DrawTitle(PageInformation page, AccurateDrawingDevice device, Rect drawArea)
        {
            var format = new AccurateTextFormat()
            {
                FontSize = TitleFontSize,
                FontWeight = AccurateFontWeight.Bold
            };
            device.DrawText(Title, format, Colors.Black, drawArea);
        }

        private void DrawPageCount(PageInformation page, AccurateDrawingDevice device, Rect drawArea)
        {
            var format = new AccurateTextFormat()
            {
                HorizontalAlignment = AccurateAlignment.Center,
                WordWrapping = AccurateWordWrapping.WholeWord,
                FontSize = XAxisInfo.TitleFontSize,
                FontWeight = AccurateFontWeight.Thin
            };

            var text = $"Page {page.PageNumber} of {page.TotalPages}";
            device.DrawText(text, format, Colors.Black, drawArea);
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
