using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class Chart : Container
    {
        public GraphicalReport Report { get; set; }
        public XAxisInfo XAxisInfo { get; set; }
        public LegendInfo LegendInfo { get; set; }
        public YAxesInfo YAxesInfo { get; set; }
        public float TopPaddingInches { get; set; } = 0.1f;
        public float TopPadding => (float)Math.Round(TopPaddingInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public float BottomPaddingInches { get; set; } = 0.1f;
        public float BottomPadding => (float)Math.Round(BottomPaddingInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public float BetweenPaddingInches { get; set; } = 0.1f;
        public float BetweenPadding => (float)Math.Round(BetweenPaddingInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public List<ChartSeries> Series { get; set; } = new List<ChartSeries>();
        public bool DrawTopBorder { get; set; } = true;
        public bool DrawBottomBorder { get; set; } = false;

        public Chart(GraphicalReport report, string name)
        {
            Report = report;
            XAxisInfo = new XAxisInfo(report.XAxisInfo);
            LegendInfo = new LegendInfo(report.LegendInfo, name);
            YAxesInfo = new YAxesInfo(report.YAxesInfo);
            YAxesInfo.Y2IsDrawn = false;
            YAxesInfo.Y1IsDrawn = false;
        }
        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea)
        {
            var leftSpace = LegendInfo.Width + YAxesInfo.Y1TotalHeight;
            var rightSpace = YAxesInfo.Y2TotalHeight;
            var remainingWidth = drawArea.Width - leftSpace - rightSpace;

            var topSpace = ((XAxisInfo.IsEnabled && XAxisInfo.IsFlippedVertical) ? XAxisInfo.TotalHeight : 0) + TopPadding;
            var bottomSpace = ((XAxisInfo.IsEnabled && !XAxisInfo.IsFlippedVertical) ? XAxisInfo.TotalHeight : 0) + TopPadding;
            var remainingHeight = drawArea.Height - topSpace - bottomSpace;

            var bodyDrawArea = new Rect(drawArea.Left + leftSpace, drawArea.Top + topSpace, remainingWidth, remainingHeight);
            //session.DrawRectangle(drawArea, Colors.Orange);
            var offset = 0f;
            var transform = new TransformInformation1d((float)bodyDrawArea.Left, (float)bodyDrawArea.Right, page.StartFootage, page.EndFootage, false);
            var gridlineDrawArea = new Rect(bodyDrawArea.Left, drawArea.Top, bodyDrawArea.Width, drawArea.Height);
            session.DrawRectangle(gridlineDrawArea, Colors.Black);
            XAxisInfo.DrawGridlines(session, page, gridlineDrawArea, transform);
            foreach (ChartSeries series in Series)
            {
                var curDrawArea = new Rect(bodyDrawArea.Left, bodyDrawArea.Top + offset, bodyDrawArea.Width, series.Height);
                //session.DrawRectangle(curDrawArea, Colors.Blue);
                series.Draw(page, session, curDrawArea, transform);
                offset += series.Height + BetweenPadding;
            }

            var xAxisY = XAxisInfo.IsFlippedVertical ? (bodyDrawArea.Top - XAxisInfo.TotalHeight) : bodyDrawArea.Bottom;
            var xAxisDrawArea = new Rect(bodyDrawArea.Left, xAxisY, bodyDrawArea.Width, XAxisInfo.TotalHeight);
            XAxisInfo.DrawInfo(session, page, transform, xAxisDrawArea);
            

            if (DrawTopBorder)
                session.DrawLine((float)drawArea.Left, (float)drawArea.Top, (float)drawArea.Right, (float)drawArea.Top, Colors.Black, 1);
            if (DrawBottomBorder)
                session.DrawLine((float)drawArea.Left, (float)drawArea.Bottom, (float)drawArea.Right, (float)drawArea.Bottom, Colors.Black, 1);
            DrawOverlapShadow(page, session, transform, gridlineDrawArea);

            var legendWidth = LegendInfo.Width + (YAxesInfo.Y1IsDrawn ? 0 : YAxesInfo.Y1TotalHeight);
            var legendDrawArea = new Rect(drawArea.X, drawArea.Y, legendWidth, drawArea.Height);
            LegendInfo.Draw(session, Series, legendDrawArea);
        }

        private void DrawOverlapShadow(PageInformation page, CanvasDrawingSession session, TransformInformation1d transform, Rect drawArea)
        {
            var color = Colors.Black;
            var opacity = 0.25f;
            var startShadowStart = transform.ToDrawArea(page.StartFootage);
            var startShadowWidth = transform.ToDrawArea(page.StartFootage + page.Overlap) - startShadowStart;
            var endShadowStart = transform.ToDrawArea(page.EndFootage - page.Overlap);
            var endShadowWidth = transform.ToDrawArea(page.EndFootage) - endShadowStart;
            var startRect = new Rect(startShadowStart, drawArea.Y, startShadowWidth, drawArea.Height);
            var endRect = new Rect(endShadowStart, drawArea.Y, endShadowWidth, drawArea.Height);
            using (var layer = session.CreateLayer(opacity))
            {
                session.FillRectangle(startRect, color);
                session.FillRectangle(endRect, color);
            }
        }

        public override double GetRequestedHeight()
        {
            var numGaps = Math.Max(0f, Series.Count - 1f);
            var seriesSizes = 0f;
            foreach (var series in Series)
                seriesSizes += series.Height;
            return TopPadding + BottomPadding + (BetweenPadding * numGaps) + seriesSizes;
        }

        public override double GetRequestedWidth()
        {
            return double.MaxValue;
        }
    }
}
