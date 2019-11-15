using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AccurateFileSystem;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class MirDirection : ChartSeries
    {
        public override float Height => OnArrowInfo.Height + GapBetween + OffArrowInfo.Height;
        public override bool IsDrawnInLegend { get => false; set => throw new InvalidOperationException(); }
        public override Color LegendNameColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public List<(double Footage, double OnMirPerFoot, double OffMirPerFoot, bool IsReverse)> Data { get; set; }

        public ArrowInfo OnArrowInfo { get; set; }
        public ArrowInfo OnArrowInfoZero { get; set; }
        public ArrowInfo OffArrowInfo { get; set; }
        public ArrowInfo OffArrowInfoZero { get; set; }
        public float GapBetweenInches { get; set; } = 0.05f;
        public float GapBetween => (float)Math.Round(GapBetweenInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public Color EdgeLineColor { get; set; } = Colors.Black;

        public MirDirection(List<(double Footage, double OnMirPerFoot, double OffMirPerFoot, bool IsReverse)> data)
        {
            Data = data;

            OnArrowInfo = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White);
            OnArrowInfoZero = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White, finWidthPercent: 0f, finHeightPercent: 0.425f, tailPercent: 1f);
            OffArrowInfo = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White);
            OffArrowInfoZero = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White, finWidthPercent: 0f, finHeightPercent: 0.425f, tailPercent: 1f);
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform)
        {

            var onHalfHeight = Math.Round(OnArrowInfo.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
            var onY = (float)(drawArea.Top + onHalfHeight);

            var offHalfHeight = Math.Round(OffArrowInfo.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
            var offY = (float)(drawArea.Top + GapBetween + OnArrowInfo.Height + offHalfHeight);

            var values = new List<(double Start, double End, double OnMirPerFoot, double OffMirPerFoot, bool IsReverse)>();
            double? start = null;
            (double On, double Off) startValue = (0, 0);
            double lastFootage = 0;
            bool lastIsReverse = false;
            for (int i = 0; i < Data.Count; ++i)
            {
                var (footage, on, off, isReverse) = Data[i];

                if (footage < page.StartFootage)
                    continue;
                if (footage > page.EndFootage)
                    break;
                if (start == null)
                {
                    start = footage;
                    startValue = (on, off);
                    lastFootage = footage;
                    lastIsReverse = isReverse;
                    continue;
                }
                if (on != startValue.On || off != startValue.Off)
                {
                    values.Add((start.Value, lastFootage, startValue.On, startValue.Off, isReverse));
                    start = footage;
                    startValue = (on, off);
                }
                lastFootage = footage;
                lastIsReverse = isReverse;
            }
            if(start.HasValue && start.Value != lastFootage)
            {
                values.Add((start.Value, lastFootage, startValue.On, startValue.Off, lastIsReverse));
            }

            if (values.Count == 0)
                return;
            using (var _ = session.CreateLayer(1f, drawArea))
            {
                if (values.Count == 1)
                {
                    var (curStart, curEnd, on, off, isReverse) = values.First();
                    if (curStart <= page.StartFootage && curEnd >= page.EndFootage)
                    {
                        var positiveDirection = !isReverse ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                        var negativeDirection = !isReverse ? ArrowInfo.Direction.Right : ArrowInfo.Direction.Left;

                        var onDirection = on < 0 ? negativeDirection : positiveDirection;
                        var mirOn = on * 1000;
                        var offDirection = off < 0 ? negativeDirection : positiveDirection;
                        var mirOff = off * 1000;

                        var middleX = drawArea.GetMiddlePoint().X;

                        if (Math.Round(mirOn, 3) != 0)
                            OnArrowInfo.DrawOnTopOf(session, middleX, onY, onDirection, mirOn.ToString("F3"));
                        else
                            OnArrowInfoZero.DrawOnTopOf(session, middleX, onY, onDirection, mirOn.ToString("F3"));

                        if (Math.Round(mirOff, 3) != 0)
                            OffArrowInfo.DrawOnTopOf(session, middleX, offY, offDirection, mirOff.ToString("F3"));
                        else
                            OffArrowInfoZero.DrawOnTopOf(session, middleX, offY, offDirection, mirOff.ToString("F3"));
                        return;
                    }
                }

                foreach (var (curStart, curEnd, on, off, isReverse) in values)
                {
                    var realStart = curStart;
                    var realEnd = curEnd;
                    if (curStart <= page.StartFootage)
                        realStart = page.StartFootage - page.Width;
                    if (curEnd >= page.EndFootage)
                        realEnd = page.EndFootage + page.Width;

                    var x1 = transform.ToDrawArea(realStart);
                    var x2 = transform.ToDrawArea(realEnd);


                    var positiveDirection = !isReverse ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                    var negativeDirection = !isReverse ? ArrowInfo.Direction.Right : ArrowInfo.Direction.Left;

                    var onDirection = on < 0 ? negativeDirection : positiveDirection;
                    var mirOn = on * 1000;
                    var offDirection = off < 0 ? negativeDirection : positiveDirection;
                    var mirOff = off * 1000;


                    if (Math.Round(mirOn, 3) != 0)
                        OnArrowInfo.DrawRightOf(session, x1, onY, onDirection, mirOn.ToString("F3"));
                    else
                        OnArrowInfoZero.DrawRightOf(session, x1, onY, onDirection, mirOn.ToString("F3"));

                    if (Math.Round(mirOff, 3) != 0)
                        OffArrowInfo.DrawRightOf(session, x1, offY, offDirection, mirOff.ToString("F3"));
                    else
                        OffArrowInfoZero.DrawRightOf(session, x1, offY, offDirection, mirOff.ToString("F3"));

                    if (Math.Round(mirOn, 3) != 0)
                        OnArrowInfo.DrawLeftOf(session, x2, onY, onDirection, mirOn.ToString("F3"));
                    else
                        OnArrowInfoZero.DrawLeftOf(session, x2, onY, onDirection, mirOn.ToString("F3"));

                    if (Math.Round(mirOff, 3) != 0)
                        OffArrowInfo.DrawLeftOf(session, x2, offY, offDirection, mirOff.ToString("F3"));
                    else
                        OffArrowInfoZero.DrawLeftOf(session, x2, offY, offDirection, mirOff.ToString("F3"));
                }
            }
        }
    }
}
