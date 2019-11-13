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
        public List<(double, bool)> DirectionData { get; set; }
        public float ArrowWidthInches { get; set; } = 0.45f;
        public float ArrowWidth => (float)Math.Round(ArrowWidthInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public bool FancySingleMiddle { get; set; } = false;
        public float FinWidthPercent { get; set; } = 0.25f;
        public float FinHeightPercent { get; set; } = 0.95f;
        public float TailPercent { get; set; } = 0.5f;

        private float FinXShift => (float)Math.Round(ArrowWidth * FinWidthPercent, GraphicalReport.DIGITS_TO_ROUND);
        private float FinYShift => (float)Math.Round(Height * FinHeightPercent / 2, GraphicalReport.DIGITS_TO_ROUND);
        private float TailYShift => (float)Math.Round(TailPercent * FinYShift, GraphicalReport.DIGITS_TO_ROUND);

        public SurveyDirectionSeries(List<(double, bool)> directionData)
        {
            DirectionData = directionData;
            HeightInches = 0.25f;
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
                        DrawArrowsAway(session, x, y);
                    else
                        DrawArrowsTowards(session, x, y);
                    firstVal = isReverseRun;
                }
                var middleFoot = page.StartFootage + (lastFootage - page.StartFootage) / 2;
                var middleX = transform.ToDrawArea(middleFoot);
                if (!FancySingleMiddle)
                    middleX = drawArea.GetMiddlePoint().X;
                if (!foundOther)
                    DrawSingleArrow(session, middleX, y, firstVal.Value);
            }
        }

        private void DrawArrowsTowards(CanvasDrawingSession session, float x, float y)
        {
            using (var pathBuilder = new CanvasPathBuilder(session))
            {
                pathBuilder.BeginFigure(x, y);
                pathBuilder.AddLine(x - FinXShift, y - FinYShift);
                pathBuilder.AddLine(x - FinXShift, y - TailYShift);
                pathBuilder.AddLine(x - ArrowWidth, y - TailYShift);
                pathBuilder.AddLine(x - ArrowWidth, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + FinYShift);
                pathBuilder.AddLine(x, y);

                pathBuilder.AddLine(x + FinXShift, y - FinYShift);
                pathBuilder.AddLine(x + FinXShift, y - TailYShift);
                pathBuilder.AddLine(x + ArrowWidth, y - TailYShift);
                pathBuilder.AddLine(x + ArrowWidth, y + TailYShift);
                pathBuilder.AddLine(x + FinXShift, y + TailYShift);
                pathBuilder.AddLine(x + FinXShift, y + FinYShift);
                pathBuilder.AddLine(x, y);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                using (var geo = CanvasGeometry.CreatePath(pathBuilder))
                {
                    session.DrawGeometry(geo, Colors.Black);
                    session.FillGeometry(geo, Colors.White);
                }
            }
        }

        private void DrawArrowsAway(CanvasDrawingSession session, float x, float y)
        {
            using (var pathBuilder = new CanvasPathBuilder(session))
            {
                x -= ArrowWidth;
                --x;
                pathBuilder.BeginFigure(x, y);
                pathBuilder.AddLine(x + FinXShift, y - FinYShift);
                pathBuilder.AddLine(x + FinXShift, y - TailYShift);
                pathBuilder.AddLine(x + ArrowWidth, y - TailYShift);
                pathBuilder.AddLine(x + ArrowWidth, y + TailYShift);
                pathBuilder.AddLine(x + FinXShift, y + TailYShift);
                pathBuilder.AddLine(x + FinXShift, y + FinYShift);
                pathBuilder.AddLine(x, y);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                ++x;

                x += 2 * ArrowWidth;
                ++x;
                pathBuilder.BeginFigure(x, y);
                pathBuilder.AddLine(x - FinXShift, y - FinYShift);
                pathBuilder.AddLine(x - FinXShift, y - TailYShift);
                pathBuilder.AddLine(x - ArrowWidth, y - TailYShift);
                pathBuilder.AddLine(x - ArrowWidth, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + FinYShift);
                pathBuilder.AddLine(x, y);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);

                using (var geo = CanvasGeometry.CreatePath(pathBuilder))
                {
                    session.DrawGeometry(geo, Colors.Black);
                    session.FillGeometry(geo, Colors.White);
                }
            }
        }

        private void DrawSingleArrow(CanvasDrawingSession session, float x, float y, bool isReverse)
        {
            var points = new List<Vector2>();
            using (var pathBuilder = new CanvasPathBuilder(session))
            {
                if (isReverse)
                {
                    x -= ArrowWidth / 2;
                    points.Add(new Vector2(x, y));
                    pathBuilder.BeginFigure(x, y);
                    points.Add(new Vector2(x, y));
                    pathBuilder.AddLine(x + FinXShift, y - FinYShift);
                    points.Add(new Vector2(x + FinXShift, y - FinYShift));
                    pathBuilder.AddLine(x + FinXShift, y - TailYShift);
                    points.Add(new Vector2(x + FinXShift, y - TailYShift));
                    pathBuilder.AddLine(x + ArrowWidth, y - TailYShift);
                    points.Add(new Vector2(x + ArrowWidth, y - TailYShift));
                    pathBuilder.AddLine(x + ArrowWidth, y + TailYShift);
                    points.Add(new Vector2(x + ArrowWidth, y + TailYShift));
                    pathBuilder.AddLine(x + FinXShift, y + TailYShift);
                    points.Add(new Vector2(x + FinXShift, y + TailYShift));
                    pathBuilder.AddLine(x + FinXShift, y + FinYShift);
                    points.Add(new Vector2(x + FinXShift, y + FinYShift));
                    pathBuilder.AddLine(x, y);
                    points.Add(new Vector2(x, y));
                    pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                }
                else
                {
                    x += ArrowWidth / 2;
                    pathBuilder.BeginFigure(x, y);
                    points.Add(new Vector2(x, y));
                    pathBuilder.AddLine(x - FinXShift, y - FinYShift);
                    points.Add(new Vector2(x - FinXShift, y - FinYShift));
                    pathBuilder.AddLine(x - FinXShift, y - TailYShift);
                    points.Add(new Vector2(x - FinXShift, y - TailYShift));
                    pathBuilder.AddLine(x - ArrowWidth, y - TailYShift);
                    points.Add(new Vector2(x - ArrowWidth, y - TailYShift));
                    pathBuilder.AddLine(x - ArrowWidth, y + TailYShift);
                    points.Add(new Vector2(x - ArrowWidth, y + TailYShift));
                    pathBuilder.AddLine(x - FinXShift, y + TailYShift);
                    points.Add(new Vector2(x - FinXShift, y + TailYShift));
                    pathBuilder.AddLine(x - FinXShift, y + FinYShift);
                    points.Add(new Vector2(x - FinXShift, y + FinYShift));
                    pathBuilder.AddLine(x, y);
                    points.Add(new Vector2(x, y));
                    pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                }

                using (var geo = CanvasGeometry.CreatePolygon(session, points.ToArray()))
                {
                    session.DrawGeometry(geo, Colors.Black);
                    session.FillGeometry(geo, Colors.White);
                }
            }
        }
    }
}
