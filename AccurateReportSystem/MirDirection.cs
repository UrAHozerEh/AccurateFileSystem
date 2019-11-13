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
        public List<ReconnectTestStationRead> Data { get; set; }

        public ArrowInfo OnArrowInfo { get; set; }
        public ArrowInfo OffArrowInfo { get; set; }
        public float GapBetweenInches { get; set; } = 0.05f;
        public float GapBetween => (float)Math.Round(GapBetweenInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        public MirDirection(List<ReconnectTestStationRead> data)
        {
            Data = data;

            OnArrowInfo = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White);
            OffArrowInfo = new ArrowInfo(0.45f, 0.25f, Colors.Black, Colors.White);
        }

        public override void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform)
        {
            //session.DrawRectangle(drawArea, Colors.Orange);
            var filtered = Data.Where(r => r.DoesIntersectPage(page)).ToList();

            var onHalfHeight = Math.Round(OnArrowInfo.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
            var onY = (float)(drawArea.Top + onHalfHeight);

            var offHalfHeight = Math.Round(OffArrowInfo.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
            var offY = (float)(drawArea.Top + GapBetween + OnArrowInfo.Height + offHalfHeight);

            if (filtered.Count == 0)
                return;
            if (filtered.Count == 1)
            {
                var reconnect = filtered.First();
                var positiveDirection = !reconnect.EndPoint.IsReverseRun ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                var negativeDirection = !reconnect.EndPoint.IsReverseRun ? ArrowInfo.Direction.Right : ArrowInfo.Direction.Left;

                var onMirPerFoot = Math.Abs(reconnect.MirOnPerFoot) * 1000;
                var onDirection = reconnect.MirOnPerFoot < 0 ? negativeDirection : positiveDirection;

                var offMirPerFoot = Math.Abs(reconnect.MirOffPerFoot) * 1000;
                var offDirection = reconnect.MirOffPerFoot < 0 ? negativeDirection : positiveDirection;

                var middleX = drawArea.GetMiddlePoint().X;
                var halfOnWidth = (float)Math.Round(OnArrowInfo.Width / 2, GraphicalReport.DIGITS_TO_ROUND);
                var halfOffWidth = (float)Math.Round(OffArrowInfo.Width / 2, GraphicalReport.DIGITS_TO_ROUND);

                OnArrowInfo.DrawOnTopOf(session, middleX, onY, onDirection, onMirPerFoot.ToString("F3"));
                OffArrowInfo.DrawOnTopOf(session, middleX, offY, offDirection, offMirPerFoot.ToString("F3"));
            }
            else
            {
                for (int i = 0; i < filtered.Count - 1; ++i)
                {
                    var recon1 = filtered[i];
                    var recon2 = filtered[i + 1];

                    var (foot1, foot2) = recon1.GetClosestFootages(recon2);
                    var x1 = transform.ToDrawArea(foot1);
                    var x2 = transform.ToDrawArea(foot2);


                    var positiveDirection = !recon1.EndPoint.IsReverseRun ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                    var negativeDirection = !recon1.EndPoint.IsReverseRun ? ArrowInfo.Direction.Right : ArrowInfo.Direction.Left;

                    var onMirPerFoot = Math.Abs(recon1.MirOnPerFoot) * 1000;
                    var onDirection = recon1.MirOnPerFoot < 0 ? negativeDirection : positiveDirection;

                    var offMirPerFoot = Math.Abs(recon1.MirOffPerFoot) * 1000;
                    var offDirection = recon1.MirOffPerFoot < 0 ? negativeDirection : positiveDirection;
                    OnArrowInfo.DrawLeftOf(session, x1, onY, onDirection, onMirPerFoot.ToString("F3"));
                    OffArrowInfo.DrawLeftOf(session, x1, offY, offDirection, offMirPerFoot.ToString("F3"));


                    positiveDirection = !recon2.EndPoint.IsReverseRun ? ArrowInfo.Direction.Left : ArrowInfo.Direction.Right;
                    negativeDirection = !recon2.EndPoint.IsReverseRun ? ArrowInfo.Direction.Right : ArrowInfo.Direction.Left;

                    onMirPerFoot = Math.Abs(recon2.MirOnPerFoot) * 1000;
                    onDirection = recon2.MirOnPerFoot < 0 ? negativeDirection : positiveDirection;

                    offMirPerFoot = Math.Abs(recon2.MirOffPerFoot) * 1000;
                    offDirection = recon2.MirOffPerFoot < 0 ? negativeDirection : positiveDirection;
                    OnArrowInfo.DrawRightOf(session, x2, onY, onDirection, onMirPerFoot.ToString("F3"));
                    OffArrowInfo.DrawRightOf(session, x2, offY, offDirection, offMirPerFoot.ToString("F3"));
                }
            }
        }
    }
}
