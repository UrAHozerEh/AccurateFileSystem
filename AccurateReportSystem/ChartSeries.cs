using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AccurateReportSystem
{
    public abstract class ChartSeries
    {
        public float HeightInches { get; set; }
        public float Height => (float)Math.Round(HeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        public abstract void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform);
    }
}
