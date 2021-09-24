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
    public class YAxesInfo
    {
        public YAxesInfo Master { get; set; }

        // Y1 Axis
        public double Y1MinimumValue
        {
            get
            {
                return y1MinimumValue ?? Master.Y1MinimumValue;
            }
            set
            {
                if (double.IsNaN(value))
                    y1MinimumValue = null;
                else
                    y1MinimumValue = value;
            }
        }
        private double? y1MinimumValue = null;
        public double Y1MaximumValue
        {
            get
            {
                return y1MaximumValue ?? Master.Y1MaximumValue;
            }
            set
            {
                if (double.IsNaN(value))
                    y1MaximumValue = null;
                else
                    y1MaximumValue = value;
            }
        }
        private double? y1MaximumValue = null;

        // Y1 Labels
        public string Y1LabelFormat
        {
            get
            {
                return y1LabelFormat ?? Master.Y1LabelFormat;
            }
            set
            {
                y1LabelFormat = value;
            }
        }
        private string y1LabelFormat = null;

        public string Y1LabelSuffix
        {
            get
            {
                return y1LabelSuffix ?? Master.Y1LabelSuffix;
            }
            set
            {
                y1LabelSuffix = value;
            }
        }
        private string y1LabelSuffix = null;
        public float Y1LabelHeightInches
        {
            get
            {
                return Y1IsEnabled ? y1LabelHeightInches ?? Master.Y1LabelHeightInches : 0;
            }
            set
            {
                if (float.IsNaN(value))
                    y1LabelHeightInches = null;
                else
                    y1LabelHeightInches = value;
            }
        }
        private float? y1LabelHeightInches = null;
        public float Y1LabelTickLength
        {
            get
            {
                return y1LabelTickLength ?? Master.Y1LabelTickLength;
            }
            set
            {
                if (float.IsNaN(value))
                    y1LabelTickLength = null;
                else
                    y1LabelTickLength = value;
            }
        }
        private float? y1LabelTickLength = null;

        public float Y1LabelFontSize
        {
            get
            {
                return y1LabelFontSize ?? Master.Y1LabelFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    y1LabelFontSize = null;
                else
                    y1LabelFontSize = value;
            }
        }
        private float? y1LabelFontSize = null;
        public float Y1LabelHeight => (float)Math.Round(Y1LabelHeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND) + Y1LabelTickLength;

        // Y1 Title
        public string Y1Title { get; set; }
        public float Y1ExtraTitleHeight
        {
            get
            {
                return y1ExtraTitleHeight ?? Master.Y1ExtraTitleHeight;
            }
            set
            {
                if (float.IsNaN(value))
                    y1ExtraTitleHeight = null;
                else
                    y1ExtraTitleHeight = value;
            }
        }
        private float? y1ExtraTitleHeight = null;
        public float Y1TitleFontSize
        {
            get
            {
                return y1TitleFontSize ?? Master.Y1TitleFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    y1TitleFontSize = null;
                else
                    y1TitleFontSize = value;
            }
        }
        private float? y1TitleFontSize = null;

        // Y1 bools
        public bool Y1TickIsDrawn
        {
            get
            {
                return y1TickIsDrawn ?? Master.Y1TickIsDrawn;
            }
            set
            {
                y1TickIsDrawn = value;
            }
        }
        private bool? y1TickIsDrawn = null;
        public bool Y1IsEnabled
        {
            get
            {
                return y1IsEnabled ?? Master.Y1IsEnabled;
            }
            set
            {
                y1IsEnabled = value;
            }
        }
        private bool? y1IsEnabled = null;
        public bool Y1IsDrawn
        {
            get
            {
                return y1IsDrawn ?? Master.Y1IsDrawn;
            }
            set
            {
                y1IsDrawn = value;
            }
        }
        private bool? y1IsDrawn = null;
        public bool Y1IsInverted
        {
            get
            {
                return y1IsInverted ?? Master.Y1IsInverted;
            }
            set
            {
                y1IsInverted = value;
            }
        }
        private bool? y1IsInverted = null;

        // Y1 calculated sizes.
        public float Y1TitleHeight => Y1TitleFontSize + Y1ExtraTitleHeight;
        public float Y1TotalHeight => Y1TitleHeight + Y1LabelHeight;
        public double Y1ValuesHeight => Math.Abs(Y1MaximumValue - Y1MinimumValue);


        // Y2 Axis
        public double Y2MinimumValue
        {
            get
            {
                return y2MinimumValue ?? Master.Y2MinimumValue;
            }
            set
            {
                if (double.IsNaN(value))
                    y2MinimumValue = null;
                else
                    y2MinimumValue = value;
            }
        }
        private double? y2MinimumValue = null;
        public double Y2MaximumValue
        {
            get
            {
                return y2MaximumValue ?? Master.Y2MaximumValue;
            }
            set
            {
                if (double.IsNaN(value))
                    y2MaximumValue = null;
                else
                    y2MaximumValue = value;
            }
        }
        private double? y2MaximumValue = null;

        // Y2 Labels
        public string Y2LabelFormat
        {
            get
            {
                return y2LabelFormat ?? Master.Y2LabelFormat;
            }
            set
            {
                y2LabelFormat = value;
            }
        }
        private string y2LabelFormat = null;
        public float Y2LabelHeightInches
        {
            get
            {
                return y2LabelHeightInches ?? Master.Y2LabelHeightInches;
            }
            set
            {
                if (float.IsNaN(value))
                    y2LabelHeightInches = null;
                else
                    y2LabelHeightInches = value;
            }
        }
        private float? y2LabelHeightInches = null;
        public float Y2LabelTickLength
        {
            get
            {
                return y2LabelTickLength ?? Master.Y2LabelTickLength;
            }
            set
            {
                if (float.IsNaN(value))
                    y2LabelTickLength = null;
                else
                    y2LabelTickLength = value;
            }
        }
        private float? y2LabelTickLength = null;
        public float Y2LabelFontSize
        {
            get
            {
                return y2LabelFontSize ?? Master.Y2LabelFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    y2LabelFontSize = null;
                else
                    y2LabelFontSize = value;
            }
        }
        private float? y2LabelFontSize = null;
        public float Y2LabelHeight => (float)Math.Round(Y2LabelHeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND) + Y2LabelTickLength;

        // Y2 Title
        public string Y2Title { get; set; }
        public float Y2ExtraTitleHeight
        {
            get
            {
                return y2ExtraTitleHeight ?? Master.Y2ExtraTitleHeight;
            }
            set
            {
                if (float.IsNaN(value))
                    y2ExtraTitleHeight = null;
                else
                    y2ExtraTitleHeight = value;
            }
        }
        private float? y2ExtraTitleHeight = null;
        public float Y2TitleFontSize
        {
            get
            {
                return y2TitleFontSize ?? Master.Y2TitleFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    y2TitleFontSize = null;
                else
                    y2TitleFontSize = value;
            }
        }
        private float? y2TitleFontSize = null;

        // Y2 bools
        public bool Y2TickIsDrawn
        {
            get
            {
                return y2TickIsDrawn ?? Master.Y2TickIsDrawn;
            }
            set
            {
                y2TickIsDrawn = value;
            }
        }
        private bool? y2TickIsDrawn = null;
        public bool Y2IsEnabled
        {
            get
            {
                return y2IsEnabled ?? Master.Y2IsEnabled;
            }
            set
            {
                y2IsEnabled = value;
            }
        }
        private bool? y2IsEnabled = null;
        public bool Y2IsDrawn
        {
            get
            {
                return y2IsDrawn ?? Master.Y2IsDrawn;
            }
            set
            {
                y2IsDrawn = value;
            }
        }
        private bool? y2IsDrawn = null;
        public bool Y2IsInverted
        {
            get
            {
                return y2IsInverted ?? Master.Y2IsInverted;
            }
            set
            {
                y2IsInverted = value;
            }
        }
        private bool? y2IsInverted = null;

        // Y2 calculated sizes.
        public float Y2TitleHeight => Y2TitleFontSize + Y2ExtraTitleHeight;
        public float Y2TotalHeight => Y2TitleHeight + Y2LabelHeight;
        public double Y2ValuesHeight => Y2MaximumValue - Y2MinimumValue;

        // Gridline Infos
        public GridlineInfo MinorGridlines
        {
            get
            {
                return minorGridlines ?? Master.MinorGridlines;
            }
            set
            {
                minorGridlines = value;
            }
        }
        private GridlineInfo minorGridlines = null;
        public GridlineInfo MajorGridlines
        {
            get
            {
                return majorGridlines ?? Master.MajorGridlines;
            }
            set
            {
                majorGridlines = value;
            }
        }
        private GridlineInfo majorGridlines = null;


        public YAxesInfo()
        {
            // Y1 Axis
            Y1MaximumValue = 0;
            Y1MinimumValue = -3;

            Y1LabelTickLength = 2f;
            Y1LabelFontSize = 8f;
            Y1LabelHeightInches = 0.25f;
            Y1LabelFormat = "F2";
            Y1LabelSuffix = "";

            Y1ExtraTitleHeight = 2f;
            Y1TitleFontSize = 16f;

            Y1TickIsDrawn = false;
            Y1IsEnabled = true;
            Y1IsDrawn = true;
            Y1IsInverted = true;

            // Y2 Axis
            Y2MaximumValue = 150;
            Y2MinimumValue = 0;

            Y2LabelTickLength = 2f;
            Y2LabelFontSize = 8f;
            Y2LabelHeightInches = 0.25f;
            Y2LabelFormat = "F0";

            Y2ExtraTitleHeight = 2f;
            Y2TitleFontSize = 16f;

            Y2TickIsDrawn = false;
            Y2IsEnabled = true;
            Y2IsDrawn = false;
            Y2IsInverted = true;

            // Gridlines
            MinorGridlines = new GridlineInfo(0.1, Colors.LightGray);
            MajorGridlines = new GridlineInfo(0.5, Colors.Gray)
            {
                Thickness = 2
            };
        }

        public YAxesInfo(YAxesInfo master) : this(master, "N/A", "N/A")
        {

        }

        public YAxesInfo(YAxesInfo master, string y1Name)
        {
            Master = master;
            Y1Title = y1Name;

            MinorGridlines = new GridlineInfo(Master.MinorGridlines);
            MajorGridlines = new GridlineInfo(Master.MajorGridlines);
        }

        public YAxesInfo(YAxesInfo master, string y1Name, string y2Name) : this(master, y1Name)
        {
            Y2Title = y2Name;
        }

        public void DrawGridlines(AccurateDrawingDevice device, Rect graphBodyDrawArea, TransformInformation2d y1Transform)
        {
            if (MinorGridlines.IsEnabled)
            {
                var values = GetGridlineValues(y1Transform, MinorGridlines.Offset);
                foreach (var (y, _) in values)
                {
                    device.DrawLine((float)graphBodyDrawArea.Left, y, (float)graphBodyDrawArea.Right, y, MinorGridlines.Color, MinorGridlines.Thickness);
                }
            }
            if (MajorGridlines.IsEnabled)
            {
                var values = GetGridlineValues(y1Transform, MajorGridlines.Offset);
                foreach (var (y, _) in values)
                {
                    device.DrawLine((float)graphBodyDrawArea.Left, y, (float)graphBodyDrawArea.Right, y, MajorGridlines.Color, MajorGridlines.Thickness);
                }
            }
        }

        private List<(float Y, double Value)> GetGridlineValues(TransformInformation2d transform, double offset)
        {
            var output = new List<(float Y, double Value)>();

            var numOffsetInStart = (int)(Y1MinimumValue / offset);
            var start = numOffsetInStart * offset;
            var curVal = start;
            while (curVal <= Y1MaximumValue)
            {
                if (curVal >= Y1MinimumValue)
                {
                    var (_, y) = transform.ToDrawArea(0, curVal);
                    output.Add((y, curVal));
                }
                curVal += offset;
            }

            return output;
        }

        public void DrawInfo(AccurateDrawingDevice device, PageInformation page, TransformInformation2d y1Transform, TransformInformation2d y2Transform, Rect graphBodyDrawArea)
        {
            var gridlineValues = GetGridlineValues(y1Transform, MajorGridlines.Offset);
            if (Y1IsDrawn)
            {
                Rect labelDrawArea = new Rect(graphBodyDrawArea.Left - Y1LabelHeight, graphBodyDrawArea.Top, Y1LabelHeight, graphBodyDrawArea.Height);
                DrawY1Labels(device, y1Transform, labelDrawArea);
                Rect titleDrawArea = new Rect(graphBodyDrawArea.Left - Y1TotalHeight, graphBodyDrawArea.Top, Y1TitleHeight, graphBodyDrawArea.Height);
                DrawTitle(device, Y1Title, Y1TitleFontSize, titleDrawArea, 90f);
            }
            if (Y2IsDrawn)
            {
                Rect labelDrawArea = new Rect(graphBodyDrawArea.Right, graphBodyDrawArea.Top, Y2LabelHeight, graphBodyDrawArea.Height);
                DrawY2Labels(device, gridlineValues, y2Transform, labelDrawArea);
                Rect titleDrawArea = new Rect(graphBodyDrawArea.Right + Y2LabelHeight, graphBodyDrawArea.Top, Y2TitleHeight, graphBodyDrawArea.Height);
                DrawTitle(device, Y2Title, Y2TitleFontSize, titleDrawArea, 90f);
            }
        }

        public void DrawTitle(AccurateDrawingDevice device, string title, float fontSize, Rect drawArea, float rotation)
        {
            //session.DrawRectangle(drawArea, Colors.Purple);
            var format = new AccurateTextFormat()
            {
                HorizontalAlignment = AccurateAlignment.Center,
                WordWrapping = AccurateWordWrapping.WholeWord,
                FontSize = fontSize,
                FontWeight = AccurateFontWeight.Thin
            };
            device.DrawText(title, format, Colors.Black, drawArea, rotation);
        }

        public void DrawY1Labels(AccurateDrawingDevice device, TransformInformation2d transform, Rect drawArea)
        {
            //session.DrawRectangle(drawArea, Colors.Orange);
            var tickColor = MajorGridlines.Color;
            var tickThickness = MajorGridlines.Thickness;
            var format = new AccurateTextFormat()
            {
                HorizontalAlignment = AccurateAlignment.Start,
                WordWrapping = AccurateWordWrapping.WholeWord,
                FontSize = Y1LabelFontSize,
                FontWeight = AccurateFontWeight.Thin
            };

            var values = GetGridlineValues(transform, MajorGridlines.Offset);
            foreach (var (location, value) in values)
            {
                var endLocation = location;
                var label = value.ToString(Y1LabelFormat) + (Y1LabelSuffix ?? "");
                var textSize = device.GetTextSize(label, format);

                var halfLayoutHeight = (float)Math.Round(textSize.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
                var finalLocation = location - halfLayoutHeight;
                if (finalLocation < drawArea.Top)
                {
                    endLocation = (float)drawArea.Top + halfLayoutHeight;
                    finalLocation = (float)drawArea.Top;
                }
                else if (finalLocation + (2 * halfLayoutHeight) > drawArea.Bottom)
                {
                    endLocation = (float)drawArea.Bottom - halfLayoutHeight;
                    finalLocation = (float)Math.Round(drawArea.Bottom - (2 * halfLayoutHeight), GraphicalReport.DIGITS_TO_ROUND);
                }
                var x = (float)(drawArea.Right - Y1LabelTickLength - textSize.Width);
                device.DrawTextCenteredOn(label, format, Colors.Black, x, finalLocation);

                if (Y1TickIsDrawn)
                {
                    device.DrawLine(drawArea.Right, location, drawArea.Right - Y1LabelTickLength, endLocation, tickColor, tickThickness);
                }
            }
        }

        public void DrawY2Labels(AccurateDrawingDevice device, List<(float Y, double _)> values, TransformInformation2d y2Transform, Rect drawArea)
        {
            //session.DrawRectangle(drawArea, Colors.Orange);
            var tickColor = MajorGridlines.Color;
            var tickThickness = MajorGridlines.Thickness;
            var format = new AccurateTextFormat()
            {
                HorizontalAlignment = AccurateAlignment.Start,
                WordWrapping = AccurateWordWrapping.WholeWord,
                FontSize = Y1LabelFontSize,
                FontWeight = AccurateFontWeight.Thin
            };

            foreach (var (location, _) in values)
            {
                var (_, value) = y2Transform.ToGraphArea(0, location);
                var endLocation = location;
                var label = value.ToString(Y2LabelFormat);
                var textBox = device.GetTextSize(label, format);

                var halfLayoutHeight = (float)Math.Round(textBox.Height / 2, GraphicalReport.DIGITS_TO_ROUND);
                var finalLocation = location - halfLayoutHeight;
                if (finalLocation < drawArea.Top)
                {
                    endLocation = (float)drawArea.Top + halfLayoutHeight;
                    finalLocation = (float)drawArea.Top;
                }
                else if (finalLocation + (2 * halfLayoutHeight) > drawArea.Bottom)
                {
                    endLocation = (float)drawArea.Bottom - halfLayoutHeight;
                    finalLocation = (float)Math.Round(drawArea.Bottom - (2 * halfLayoutHeight), GraphicalReport.DIGITS_TO_ROUND);
                }
                var x = (float)(drawArea.Left + Y1LabelTickLength);
                var translate = Matrix3x2.CreateTranslation(x, finalLocation);

                device.DrawTextCenteredOn(label, format, tickColor, x, location);


                if (Y2TickIsDrawn)
                {
                    device.DrawLine(drawArea.Left, location, drawArea.Left - Y2LabelTickLength, endLocation, tickColor, tickThickness);
                }
            }
        }
    }
}
