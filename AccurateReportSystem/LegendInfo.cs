using AccurateReportSystem.AccurateDrawingDevices;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

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
        /// <summary>
        /// Width of the Legend in inches. Default value: 1
        /// </summary>
        public double WidthInches
        {
            get
            {
                return widthInches ?? Master.WidthInches;
            }
            set
            {
                if (double.IsNaN(value))
                    widthInches = null;
                else
                    widthInches = value;
            }
        }
        private double? widthInches = null;
        public double Width => Math.Round(WidthInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public string Name { get; set; }

        public LegendInfo()
        {
            NameFontSize = 20f;
            NameColor = Colors.Black;
            SeriesNameFontSize = 16f;
            SeriesNameColor = Colors.Black;
            SeriesSymbolWidth = 50;
            VerticalAlignment = CanvasVerticalAlignment.Center;
            HorizontalAlignment = CanvasHorizontalAlignment.Center;
            SeriesNameUsesSeriesColor = true;
            WidthInches = 1;
            Name = "Master";
        }

        public LegendInfo(LegendInfo master, string name)
        {
            Master = master;
            Name = name;
        }

        public void Draw(AccurateDrawingDevice device, IEnumerable<Series> series, Rect drawArea)
        {
            var height = 0f;
            var labelPercentWidth = float.NaN;
            foreach (var s in series)
            {
                if (s is ExceptionsChartSeries chartSeries)
                {
                    if (float.IsNaN(labelPercentWidth))
                        labelPercentWidth = chartSeries.LegendLabelSplit;
                    else
                        labelPercentWidth = Math.Min(chartSeries.LegendLabelSplit, labelPercentWidth);
                }
            }
            if (float.IsNaN(labelPercentWidth))
                labelPercentWidth = 1f;
            var width = (float)(drawArea.Width * labelPercentWidth);
            var filteredSeries = series.Where(s => s.IsDrawnInLegend).ToList();
            var nameFormat = new AccurateTextFormat()
            {
                FontWeight = AccurateFontWeight.Bold,
                FontSize = NameFontSize
            };
            var seriesFormat = new AccurateTextFormat()
            {
                FontWeight = AccurateFontWeight.Bold,
                FontSize = SeriesNameFontSize
            };

            var layout = device.GetTextSize(Name, nameFormat);
            height += (float)layout.Height;


            foreach (var curSeries in filteredSeries)
            {
                layout = device.GetTextSize(curSeries.Name, seriesFormat);
                height += (float)layout.Height;
            }

            var offset = 0f;
            if (VerticalAlignment == CanvasVerticalAlignment.Center)
                offset = (float)Math.Round(drawArea.Height / 2 - height / 2, GraphicalReport.DIGITS_TO_ROUND);
            else if (VerticalAlignment == CanvasVerticalAlignment.Bottom)
                offset = (float)(drawArea.Bottom - height);

            layout = device.GetTextSize(Name, nameFormat);
            offset += (float)layout.Height;
            device.DrawFormattedText(Name, nameFormat, NameColor, drawArea.X, drawArea.Y + offset, 0);

            foreach (var curSeries in filteredSeries)
            {
                layout = device.GetTextSize(curSeries.Name, seriesFormat);
                var color = SeriesNameUsesSeriesColor ? curSeries.LegendNameColor : SeriesNameColor;
                device.DrawFormattedText(curSeries.Name, seriesFormat, color, drawArea.X, drawArea.Y + offset, 0);
                offset += (float)layout.Height;
            }
        }
    }
}
