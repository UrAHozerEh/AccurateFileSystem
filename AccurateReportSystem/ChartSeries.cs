using AccurateReportSystem.AccurateDrawingDevices;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace AccurateReportSystem
{
    public abstract class ChartSeries : Series
    {
        public float HeightInches { get; set; }
        public virtual float Height => (float)Math.Round(HeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        public abstract void Draw(PageInformation page, AccurateDrawingDevice device, Rect drawArea, TransformInformation1d transform);
    }
}
