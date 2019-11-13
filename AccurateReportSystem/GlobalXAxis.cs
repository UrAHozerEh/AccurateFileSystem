using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;

namespace AccurateReportSystem
{
    public class GlobalXAxis : Container
    {
        public GraphicalReport Report { get; set; }
        public XAxisInfo XAxisInfo { get; set; }

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

            var leftSpace = Report.LegendInfo.WidthDIP + Report.YAxesInfo.Y1TotalHeight;
            var rightSpace = Report.YAxesInfo.Y2TotalHeight;
            var width = drawArea.Width - leftSpace - rightSpace;
            var realDrawArea = new Rect(drawArea.Left + leftSpace, drawArea.Top, width, drawArea.Height);

            //TODO: No need to make a 2d transform.
            var transform = new TransformInformation2d(realDrawArea, graphArea, false);

            XAxisInfo.DrawInfo(session, page, transform.GetXTransform(), realDrawArea);
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
