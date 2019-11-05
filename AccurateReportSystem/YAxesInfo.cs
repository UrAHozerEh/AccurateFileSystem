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
        public double Y1AxisLabelWidth { get; set; } = 0.25;
        public double Y1AxisLabelWidthDIP => Math.Round(Y1AxisLabelWidth * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double Y1AxisLabelTickLength { get; set; } = 5;
        public float Y1AxisLabelFontSize { get; set; } = 8f;
        public string Y1AxisLabelFormat { get; set; } = "F2";

        public float Y1AxisTitleFontSize
        {
            get
            {
                return y1AxisTitleFontSize ?? Master.Y1AxisTitleFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    y1AxisTitleFontSize = null;
                else
                    y1AxisTitleFontSize = value;
            }
        }
        private float? y1AxisTitleFontSize = null;
        public bool Y1AxisIsEnabled
        {
            get
            {
                return y1AxisIsEnabled ?? Master.Y1AxisIsEnabled;
            }
            set
            {
                y1AxisIsEnabled = value;
            }
        }
        private bool? y1AxisIsEnabled = null;
        public bool Y1AxisIsDrawn
        {
            get
            {
                return y1AxisIsDrawn ?? Master.Y1AxisIsDrawn;
            }
            set
            {
                y1AxisIsDrawn = value;
            }
        }
        private bool? y1AxisIsDrawn = null;
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
        public string Y1AxisTitle { get; set; }

        // Y2 Axis
        //TODO: Add extra title height.
        public double Y2AxisLabelWidth { get; set; } = 0;
        public double Y2AxisLabelWidthDIP => Math.Round(Y2AxisLabelWidth * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double Y2AxisTitleWidthDIP => Math.Round(Y2TitleFontSize * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        // Y2 Labels
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
        

        public YAxesInfo()
        {
            // Y1 Axis
            Y1AxisTitleFontSize = 16f;
            Y1AxisIsEnabled = true;
            Y1AxisIsDrawn = true;
            Y1IsInverted = true;

            // Y2 Axis
            Y2LabelHeightInches = 0.25f;
            Y2ExtraTitleHeight = 2f;
            Y2TitleFontSize = 16f;
            Y2IsEnabled = false;
            Y2IsDrawn = false;
            Y2IsInverted = false;
        }

        public YAxesInfo(YAxesInfo master, string y1Name)
        {
            Master = master;
            Y1AxisTitle = y1Name;
        }

        public YAxesInfo(YAxesInfo master, string y1Name, string y2Name) : this(master, y1Name)
        {
            Y2Title = y2Name;
        }
    }
}
