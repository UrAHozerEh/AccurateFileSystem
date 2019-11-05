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
        public float NameFontSize
        {
            get
            {
                return nameFontSize ?? Master.NameFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    nameFontSize = null;
                else
                    nameFontSize = value;
            }
        }
        private float? nameFontSize = null;
        public Color NameColor
        {
            get
            {
                return nameColor ?? Master.NameColor;
            }
            set
            {
                nameColor = value;
            }
        }
        private Color? nameColor = null;
        public float SeriesNameFontSize
        {
            get
            {
                return seriesNameFontSize ?? Master.SeriesNameFontSize;
            }
            set
            {
                if (float.IsNaN(value))
                    seriesNameFontSize = null;
                else
                    seriesNameFontSize = value;
            }
        }
        private float? seriesNameFontSize = null;
        public Color SeriesNameColor
        {
            get
            {
                return seriesNameColor ?? Master.SeriesNameColor;
            }
            set
            {
                seriesNameColor = value;
            }
        }
        private Color? seriesNameColor = null;
        public bool SeriesNameUsesSeriesColor
        {
            get
            {
                return seriesNameUsesSeriesColor ?? Master.SeriesNameUsesSeriesColor;
            }
            set
            {
                seriesNameUsesSeriesColor = value;
            }
        }
        private bool? seriesNameUsesSeriesColor = null;
        public double SeriesSymbolWidth
        {
            get
            {
                return seriesSymbolWidth ?? Master.SeriesSymbolWidth;
            }
            set
            {
                if (double.IsNaN(value))
                    seriesSymbolWidth = null;
                else
                    seriesSymbolWidth = value;
            }
        }
        private double? seriesSymbolWidth = null;
        public CanvasVerticalAlignment VerticalAlignment
        {
            get
            {
                return verticalAlignment ?? Master.VerticalAlignment;
            }
            set
            {
                verticalAlignment = value;
            }
        }
        private CanvasVerticalAlignment? verticalAlignment = null;
        public CanvasHorizontalAlignment HorizontalAlignment
        {
            get
            {
                return horizontalAlignment ?? Master.HorizontalAlignment;
            }
            set
            {
                horizontalAlignment = value;
            }
        }
        private CanvasHorizontalAlignment? horizontalAlignment = null;
        public double Width
        {
            get
            {
                return width ?? Master.Width;
            }
            set
            {
                if (double.IsNaN(value))
                    width = null;
                else
                    width = value;
            }
        }
        private double? width = null;
        public double WidthDIP => Math.Round(Width * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public string Name { get; set; }

        public LegendInfo()
        {
            NameFontSize = 18f;
            NameColor = Colors.Black;
            SeriesNameFontSize = 12f;
            SeriesNameColor = Colors.Black;
            SeriesSymbolWidth = 50;
            VerticalAlignment = CanvasVerticalAlignment.Center;
            HorizontalAlignment = CanvasHorizontalAlignment.Center;
            SeriesNameUsesSeriesColor = true;
            Width = 1.5;
            Name = "Master";
        }

        public LegendInfo(LegendInfo master, string name)
        {
            Master = master;
            Name = name;
        }
    }
}
