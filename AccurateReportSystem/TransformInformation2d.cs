using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AccurateReportSystem
{
    public struct TransformInformation2d
    {
        public Rect DrawArea { get; private set; }
        public Rect GraphArea { get; private set; }
        public bool IsInverted { get; private set; }

        public TransformInformation2d(Rect drawArea, Rect graphArea, bool isInverted)
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
            var xScale = DrawArea.Width / GraphArea.Width;
            var yScale = DrawArea.Height / GraphArea.Height;

            var scaledFootage = x - DrawArea.X;
            var footageInGraph = Math.Round(scaledFootage / xScale, GraphicalReport.DIGITS_TO_ROUND);
            var footage = footageInGraph + GraphArea.X;

            var scaledValue = (IsInverted ? y - DrawArea.Top : DrawArea.Bottom - y);
            var valueInGraph = Math.Round(scaledValue / yScale, GraphicalReport.DIGITS_TO_ROUND);
            var value = valueInGraph + GraphArea.Y;

            return (footage, value);
        }

        public (double Footage, double Value) ToGraphArea((float X, float Y) values)
        {
            return ToGraphArea(values.X, values.Y);
        }

        public TransformInformation1d GetXTransform()
        {
            return new TransformInformation1d((float)DrawArea.Left, (float)DrawArea.Right, GraphArea.Left, GraphArea.Right, false);
        }

        public TransformInformation1d GetYTransform()
        {
            return new TransformInformation1d((float)DrawArea.Top, (float)DrawArea.Bottom, GraphArea.Top, GraphArea.Bottom, IsInverted);
        }
    }

    public struct TransformInformation1d
    {
        public float DrawAreaMin { get; set; }
        public float DrawAreaMax { get; set; }
        public float DrawAreaWidth => DrawAreaMax - DrawAreaMin;
        public double GraphAreaMin { get; set; }
        public double GraphAreaMax { get; set; }
        public double GraphAreaWidth => GraphAreaMax - GraphAreaMin;
        public bool IsInverted { get; private set; }

        public TransformInformation1d(float drawAreaMin, float drawAreaMax, double graphAreaMin, double graphAreaMax, bool isInverted)
        {
            DrawAreaMin = drawAreaMin;
            DrawAreaMax = drawAreaMax;

            GraphAreaMin = graphAreaMin;
            GraphAreaMax = graphAreaMax;

            IsInverted = isInverted;
        }

        public float ToDrawArea(double value)
        {
            var scale = DrawAreaWidth / GraphAreaWidth;

            var valueInGraph = value - GraphAreaMin;
            var scaledValue = valueInGraph * scale;

            var y = (float)Math.Round(!IsInverted ? DrawAreaMin + scaledValue : DrawAreaMax - scaledValue, GraphicalReport.DIGITS_TO_ROUND);
            return y;
        }

        public double ToGraphArea(float value)
        {
            var scale = DrawAreaWidth / GraphAreaWidth;

            var scaledValue = (!IsInverted ? value - DrawAreaMin : DrawAreaMax - value);
            var valueInGraph = Math.Round(scaledValue / scale, GraphicalReport.DIGITS_TO_ROUND);
            var finalValue = valueInGraph + GraphAreaMin;

            return finalValue;
        }
    }
}
