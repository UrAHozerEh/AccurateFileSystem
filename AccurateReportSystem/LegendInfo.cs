using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class LegendInfo
    {
        //TODO: I should add reset functions for all these shits. If there is a master then set to null, else set to defaults.
        //TODO: I guess I should also add defaults as private statics or something if I do that.
        public LegendInfo Master { get; set; }
        public float LegendNameFontSize
        {
            get
            {
                return legendNameFontSize ?? Master.LegendNameFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    legendNameFontSize = null;
                else
                    legendNameFontSize = value;
            }
        }
        private float? legendNameFontSize = null;
        public Color LegendNameColor
        {
            get
            {
                return legendNameColor ?? Master.LegendNameColor;
            }
            set
            {
                legendNameColor = value;
            }
        }
        private Color? legendNameColor = null;
        public float LegendSeriesNameFontSize
        {
            get
            {
                return legendSeriesNameFontSize ?? Master.LegendSeriesNameFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    legendSeriesNameFontSize = null;
                else
                    legendSeriesNameFontSize = value;
            }
        }
        private float? legendSeriesNameFontSize = null;
        public Color LegendSeriesNameColor
        {
            get
            {
                return legendSeriesNameColor ?? Master.LegendSeriesNameColor;
            }
            set
            {
                legendSeriesNameColor = value;
            }
        }
        private Color? legendSeriesNameColor = null;
        public bool LegendSeriesNameUsesSeriesColor
        {
            get
            {
                return legendSeriesNameUsesSeriesColor ?? Master.LegendSeriesNameUsesSeriesColor;
            }
            set
            {
                legendSeriesNameUsesSeriesColor = value;
            }
        }
        private bool? legendSeriesNameUsesSeriesColor = null;
        public double LegendSeriesSymbolWidth
        {
            get
            {
                return legendSeriesSymbolWidth ?? Master.LegendSeriesSymbolWidth;
            }
            set
            {
                if (double.IsNaN(value))
                    legendSeriesSymbolWidth = null;
                else
                    legendSeriesSymbolWidth = value;
            }
        }
        private double? legendSeriesSymbolWidth = null;
        public CanvasVerticalAlignment LegendNameVerticalAlignment
        {
            get
            {
                return legendNameVerticalAlignment ?? Master.LegendNameVerticalAlignment;
            }
            set
            {
                legendNameVerticalAlignment = value;
            }
        }
        private CanvasVerticalAlignment? legendNameVerticalAlignment = null;
        public CanvasHorizontalAlignment LegendNameHorizontalAlignment
        {
            get
            {
                return legendNameHorizontalAlignment ?? Master.LegendNameHorizontalAlignment;
            }
            set
            {
                legendNameHorizontalAlignment = value;
            }
        }
        private CanvasHorizontalAlignment? legendNameHorizontalAlignment = null;
        public double LegendWidth
        {
            get
            {
                return legendWidth ?? Master.LegendWidth;
            }
            set
            {
                if (double.IsNaN(value))
                    legendWidth = null;
                else
                    legendWidth = value;
            }
        }
        private double? legendWidth = null;
        public double LegendWidthDIP => Math.Round(LegendWidth * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public string Name { get; set; }

        public LegendInfo()
        {
            LegendNameFontSize = 18f;
            LegendNameColor = Colors.Black;
            LegendSeriesNameFontSize = 12f;
            LegendSeriesNameColor = Colors.Black;
            LegendSeriesSymbolWidth = 50;
            LegendNameVerticalAlignment = CanvasVerticalAlignment.Center;
            LegendNameHorizontalAlignment = CanvasHorizontalAlignment.Center;
            LegendSeriesNameUsesSeriesColor = true;
            LegendWidth = 1.5;
        }

        public LegendInfo(LegendInfo master, string name)
        {
            Master = master;

        }
    }
}
