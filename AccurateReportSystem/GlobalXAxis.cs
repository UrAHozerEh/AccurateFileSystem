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

        public GlobalXAxis(GraphicalReport report)
        {
            Report = report;
            XAxisInfo = new XAxisInfo(report.XAxisInfo);
        }

        public override void Draw(PageInformation pageInformation, CanvasDrawingSession session, Rect drawArea)
        {
            throw new NotImplementedException();
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
