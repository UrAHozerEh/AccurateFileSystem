using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class SurveyDirectionSeries : ChartSeries
    {
        public override float Height => ArrowInfo.Height;
        public List<(double, bool)> DirectionData { get; set; }
        private List<(double Start, double End, bool IsReverse)> DirectionAreas;
        public bool FancySingleMiddle { get; set; } = false;
        public ArrowInfo ArrowInfo { get; set; }
        public override bool IsDrawnInLegend
        {
            get { return false; }
            set { throw new InvalidOperationException(); }
        }
        public override Color LegendNameColor { get => throw new ArgumentException(); set => throw new InvalidOperationException(); }

        public SurveyDirectionSeries(List<(double, bool)> directionData)
        {
            DirectionData = directionData;

            ArrowInfo = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White);
            UpdateAreas();
        }

        public void UpdateAreas()
        {
            DirectionAreas = new List<(double, double, bool)>();
            (double Start, double End, bool IsReverse)? curData = null;
            foreach (var (footage, isReverse) in DirectionData)
            {
                if (!curData.HasValue)
                {
                    curData = (footage, footage, isReverse);
                    continue;
                }
                var curDataValue = curData.Value;
                if (curDataValue.IsReverse == isReverse)
                {
                    curData = (curDataValue.Start, footage, isReverse);
                }
                else
                {
                    DirectionAreas.Add(curDataValue);
                    curData = (footage, footage, isReverse);
                }
            }
            if (curData.HasValue)
                DirectionAreas.Add(curData.Value);
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform)
        {
            using (session.CreateLayer(1f, drawArea))
            {
                var areasOnPage = DirectionAreas.Where(area => area.Start < page.EndFootage && area.End > page.StartFootage).ToList();
                var y = (float)(drawArea.Top + Math.Round(drawArea.Height / 2, GraphicalReport.DIGITS_TO_ROUND));
                if (areasOnPage.Count == 0)
                    return;
                if (areasOnPage.Count == 1)
                {
                    var (_, _, value) = areasOnPage[0];
                    var middleX = drawArea.GetMiddlePoint().X;
                    ArrowInfo.DrawOnTopOf(session, middleX, y, value ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right);
                }
                else
                {
                    foreach (var area in areasOnPage)
                    {
                        var direction = area.IsReverse ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                        if (area.Start < page.StartFootage)
                        {
                            var x = transform.ToDrawArea(area.End);
                            ArrowInfo.DrawLeftOf(session, x, y, direction);
                            continue;
                        }
                        if (area.End > page.EndFootage)
                        {
                            var x = transform.ToDrawArea(area.Start);
                            ArrowInfo.DrawRightOf(session, x, y, direction);
                            continue;
                        }
                        var startX = transform.ToDrawArea(area.Start);
                        var endX = transform.ToDrawArea(area.End);
                        var middleX = (startX + endX) / 2;
                        var xWidth = endX - startX;
                        if (xWidth < ArrowInfo.Width)
                        {
                            var xScale = xWidth / ArrowInfo.Width;
                            var arrowWidthInches = ArrowInfo.WidthInches;
                            ArrowInfo.WidthInches *= xScale;
                            ArrowInfo.DrawOnTopOf(session, middleX, y, direction);
                            ArrowInfo.WidthInches = arrowWidthInches;
                        }
                        else if (xWidth < ArrowInfo.Width * 2)
                        {
                            ArrowInfo.DrawOnTopOf(session, middleX, y, direction);
                        }
                        else
                        {
                            ArrowInfo.DrawRightOf(session, startX, y, direction);
                            ArrowInfo.DrawLeftOf(session, endX, y, direction);
                        }
                    }
                }
            }
        }
    }
}
