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
        public Color LegendNameColor { get; set; } = Colors.Black;
        public float LegendSeriesNameFontSize { get; set; } = 12f;
        public Color LegendSeriesNameColor { get; set; } = Colors.Black;
        public double LegendSeriesLineLength { get; set; } = 50;
        public CanvasVerticalAlignment LegendNameVerticalAlignment { get; set; } = CanvasVerticalAlignment.Center;
        public CanvasHorizontalAlignment LegendNameHorizontalAlignment { get; set; } = CanvasHorizontalAlignment.Center;

        public LegendInfo()
        {
            LegendNameFontSize = 18f;
        }
    }
}
