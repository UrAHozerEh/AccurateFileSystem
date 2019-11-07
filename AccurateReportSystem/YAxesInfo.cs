using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public float Y1LabelHeight => (float)Math.Round(Y1LabelHeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

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
        public float Y1TitleHeight => Y1IsEnabled ? Y1TitleFontSize + Y1ExtraTitleHeight : 0;
        public float Y1TotalHeight => Y1TitleHeight + Y1LabelHeight;
        public double Y1ValuesHeight => Y1MaximumValue - Y1MinimumValue;


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
                return Y2IsEnabled ? y2LabelHeightInches ?? Master.Y2LabelHeightInches : 0;
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
        public float Y2LabelHeight => (float)Math.Round(Y2LabelHeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

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
        public float Y2TitleHeight => Y2IsEnabled ? Y2TitleFontSize + Y2ExtraTitleHeight : 0;
        public float Y2TotalHeight => Y2TitleHeight + Y2LabelHeight;
        public double Y2ValuesHeight => Y2MaximumValue - Y2MinimumValue;
        

        public YAxesInfo()
        {
            // Y1 Axis
            Y1MaximumValue = 0;
            Y1MinimumValue = -3;

            Y1LabelTickLength = 5;
            Y1LabelFontSize = 8f;
            Y1LabelHeightInches = 0.25f;
            Y1LabelFormat = "F2";

            Y1ExtraTitleHeight = 2f;
            Y1TitleFontSize = 16f;

            Y1IsEnabled = true;
            Y1IsDrawn = true;
            Y1IsInverted = true;

            // Y2 Axis
            Y2MaximumValue = 150;
            Y2MinimumValue = 0;

            Y2LabelTickLength = 5;
            Y2LabelFontSize = 8f;
            Y2LabelHeightInches = 0.25f;
            Y2LabelFormat = "F1";

            Y2ExtraTitleHeight = 2f;
            Y2TitleFontSize = 16f;

            Y2IsEnabled = false;
            Y2IsDrawn = true;
            Y2IsInverted = true;
        }

        public YAxesInfo(YAxesInfo master, string y1Name)
        {
            Master = master;
            Y1Title = y1Name;
        }

        public YAxesInfo(YAxesInfo master, string y1Name, string y2Name) : this(master, y1Name)
        {
            Y2Title = y2Name;
        }
    }
}
