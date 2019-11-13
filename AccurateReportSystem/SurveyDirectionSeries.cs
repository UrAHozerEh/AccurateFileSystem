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
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform)
        {
            using (session.CreateLayer(1f, drawArea))
            {
                bool? firstVal = null;
                bool foundOther = false;
                var y = (float)(drawArea.Top + Math.Round(drawArea.Height / 2, GraphicalReport.DIGITS_TO_ROUND));
                double lastFootage = 0;
                foreach (var (footage, isReverseRun) in DirectionData)
                {

                    if (footage < page.StartFootage)
                        continue;
                    if (footage > page.EndFootage)
                        break;
                    lastFootage = footage;
                    if (!firstVal.HasValue)
                        firstVal = isReverseRun;
                    if (isReverseRun == firstVal)
                        continue;
                    foundOther = true;
                    var x = transform.ToDrawArea(footage);
                    if (firstVal.Value)
                    {
                        ArrowInfo.DrawLeftOf(session, x, y, ArrowInfo.Direction.Left);
                        ArrowInfo.DrawRightOf(session, x, y, ArrowInfo.Direction.Right);
                    }
                    else
                    {
                        ArrowInfo.DrawLeftOf(session, x, y, ArrowInfo.Direction.Right);
                        ArrowInfo.DrawRightOf(session, x, y, ArrowInfo.Direction.Left);
                    }
                    firstVal = isReverseRun;
                }
                var middleFoot = page.StartFootage + (lastFootage - page.StartFootage) / 2;
                var middleX = transform.ToDrawArea(middleFoot);
                if (!FancySingleMiddle)
                    middleX = drawArea.GetMiddlePoint().X;
                if (!foundOther)
                    ArrowInfo.DrawOnTopOf(session, middleX, y, firstVal.Value ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right);
            }
        }
    }
}
