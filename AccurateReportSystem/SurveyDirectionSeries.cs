using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AccurateReportSystem.AccurateDrawingDevices;
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

        public override void Draw(PageInformation page, AccurateDrawingDevice device, Rect drawArea, TransformInformation1d transform)
        {
            var areasOnPage = DirectionAreas.Where(area => area.Start < page.EndFootage && area.End > page.StartFootage).ToList();
            var y = (float)(drawArea.Top + Math.Round(drawArea.Height / 2, GraphicalReport.DIGITS_TO_ROUND));
            if (areasOnPage.Count == 0)
                return;
            if (areasOnPage.Count == 1)
            {
                var (_, _, value) = areasOnPage[0];
                var middleX = drawArea.GetMiddlePoint().X;
                ArrowInfo.DrawCenteredOn(device, middleX, drawArea, value ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right);
            }
            else
            {
                foreach (var (start, end, isReverse) in areasOnPage)
                {
                    var direction = isReverse ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                    if (start < page.StartFootage)
                    {
                        var x = transform.ToDrawArea(end);
                        ArrowInfo.DrawLeftOf(device, x, drawArea, direction);
                        continue;
                    }
                    if (end > page.EndFootage)
                    {
                        var x = transform.ToDrawArea(start);
                        ArrowInfo.DrawRightOf(device, x, drawArea, direction);
                        continue;
                    }
                    var startX = transform.ToDrawArea(start);
                    var endX = transform.ToDrawArea(end);
                    var middleX = (startX + endX) / 2;
                    var xWidth = endX - startX;
                    if (xWidth < ArrowInfo.Width)
                    {
                        var xScale = xWidth / ArrowInfo.Width;
                        var arrowWidthInches = ArrowInfo.WidthInches;
                        ArrowInfo.WidthInches *= xScale;
                        ArrowInfo.DrawCenteredOn(device, middleX, drawArea, direction);
                        ArrowInfo.WidthInches = arrowWidthInches;
                    }
                    else if (xWidth < ArrowInfo.Width * 2)
                    {
                        ArrowInfo.DrawCenteredOn(device, middleX, drawArea, direction);
                    }
                    else
                    {
                        ArrowInfo.DrawRightOf(device, startX, drawArea, direction);
                        ArrowInfo.DrawLeftOf(device, endX, drawArea, direction);
                    }
                }
            }

        }
    }

    public class SurveyDirectionWithDateSeries : ChartSeries
    {
        public override float Height => ArrowInfo.Height;
        public List<(double, bool, string)> DirectionData { get; set; }
        private List<(double Start, double End, bool IsReverse, string Date)> DirectionAreas;
        public bool FancySingleMiddle { get; set; } = false;
        public ArrowInfo ArrowInfo { get; set; }
        public override bool IsDrawnInLegend
        {
            get { return false; }
            set { throw new InvalidOperationException(); }
        }
        public override Color LegendNameColor { get => throw new ArgumentException(); set => throw new InvalidOperationException(); }

        public SurveyDirectionWithDateSeries(List<(double, bool, string)> directionData)
        {
            DirectionData = directionData;

            ArrowInfo = new ArrowInfo(0.5f, 0.35f, Colors.Black, Colors.White, fontSize: 8f);
            UpdateAreas();
        }

        public void UpdateAreas()
        {
            DirectionAreas = new List<(double, double, bool, string)>();
            (double Start, double End, bool IsReverse, string Date)? curData = null;
            foreach (var (footage, isReverse, date) in DirectionData)
            {
                if (!curData.HasValue)
                {
                    curData = (footage, footage, isReverse, date);
                    continue;
                }
                var curDataValue = curData.Value;
                if (curDataValue.IsReverse == isReverse && date.Equals(curDataValue.Date))
                {
                    curData = (curDataValue.Start, footage, isReverse, date);
                }
                else
                {
                    DirectionAreas.Add(curDataValue);
                    curData = (footage, footage, isReverse, date);
                }
            }
            if (curData.HasValue)
                DirectionAreas.Add(curData.Value);
        }

        public override void Draw(PageInformation page, AccurateDrawingDevice device, Rect drawArea, TransformInformation1d transform)
        {
            var areasOnPage = DirectionAreas.Where(area => area.Start < page.EndFootage && area.End > page.StartFootage).ToList();
            var y = (float)(drawArea.Top + Math.Round(drawArea.Height / 2, GraphicalReport.DIGITS_TO_ROUND));
            if (areasOnPage.Count == 0)
                return;
            if (areasOnPage.Count == 1)
            {
                var (_, _, value, date) = areasOnPage[0];
                var middleX = drawArea.GetMiddlePoint().X;
                ArrowInfo.DrawCenteredOn(device, middleX, drawArea, value ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right, date);
            }
            else
            {
                foreach (var (start, end, isReverse, date) in areasOnPage)
                {
                    var direction = isReverse ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                    if (start < page.StartFootage)
                    {
                        var x = transform.ToDrawArea(end);
                        ArrowInfo.DrawLeftOf(device, x, drawArea, direction, date);
                        continue;
                    }
                    if (end > page.EndFootage)
                    {
                        var x = transform.ToDrawArea(start);
                        ArrowInfo.DrawRightOf(device, x, drawArea, direction, date);
                        continue;
                    }
                    var startX = transform.ToDrawArea(start);
                    var endX = transform.ToDrawArea(end);
                    var middleX = (startX + endX) / 2;
                    var xWidth = endX - startX;
                    if (xWidth < ArrowInfo.Width)
                    {
                        var xScale = xWidth / ArrowInfo.Width;
                        var arrowWidthInches = ArrowInfo.WidthInches;
                        ArrowInfo.WidthInches *= xScale;
                        ArrowInfo.DrawCenteredOn(device, middleX, drawArea, direction, date);
                        ArrowInfo.WidthInches = arrowWidthInches;
                    }
                    else if (xWidth < ArrowInfo.Width * 2)
                    {
                        ArrowInfo.DrawCenteredOn(device, middleX, drawArea, direction, date);
                    }
                    else
                    {
                        ArrowInfo.DrawRightOf(device, startX, drawArea, direction, date);
                        ArrowInfo.DrawLeftOf(device, endX, drawArea, direction, date);
                    }
                }
            }

        }
    }
}
