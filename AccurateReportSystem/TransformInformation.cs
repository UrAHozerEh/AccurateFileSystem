using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AccurateReportSystem
{
    public struct TransformInformation
    {
        public Rect DrawArea { get; private set; }
        public Rect GraphArea { get; private set; }
        public bool IsInverted { get; private set; }

        public TransformInformation(Rect drawArea, Rect graphArea, bool isInverted)
        {
            DrawArea = drawArea;
            GraphArea = graphArea;
            IsInverted = isInverted;
        }

        public (float X, float Y) ToDrawArea(double footage, double value)
        {
            var xScale = DrawArea.Width / GraphArea.Width;
            var yScale = DrawArea.Height / GraphArea.Height;

            var footageInGraph = footage - GraphArea.X;
            var scaledFootage = footageInGraph * xScale;

            var valueInGraph = value - GraphArea.Y;
            var scaledValue = valueInGraph * yScale;

            var x = (float)Math.Round(DrawArea.X + scaledFootage, GraphicalReport.DIGITS_TO_ROUND);
            var y = (float)Math.Round(IsInverted ? DrawArea.Top + scaledValue : DrawArea.Bottom - scaledValue, GraphicalReport.DIGITS_TO_ROUND);
            return (x, y);
        }
        public (float X, float Y) ToDrawArea((double Footage, double Value) values)
        {
            return ToDrawArea(values.Footage, values.Value);
        }
        public (double Footage, double Value) ToGraphArea(float x, float y)
        {
            return (0, 0);
        }
    }
}
