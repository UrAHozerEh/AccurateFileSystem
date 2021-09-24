using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class GridlineInfo
    {
        public GridlineInfo Master { get; set; }
        public bool IsEnabled
        {
            get => isEnabled ?? Master.IsEnabled;
            set => isEnabled = value;
        }
        private bool? isEnabled = null;
        public int Thickness
        {
            get => thickness ?? Master.Thickness;
            set => thickness = value;
        }
        public int? thickness = null;
        public Color Color
        {
            get => color ?? Master.Color;
            set => color = value;
        }
        private Color? color = null;
        public double Offset
        {
            get => offset ?? Master.Offset;
            set => offset = value;
        }
        private double? offset = null;

        public GridlineInfo(double offset, Color color)
        {
            Color = color;
            Offset = offset;

            IsEnabled = true;
            Thickness = 1;
        }

        public GridlineInfo(GridlineInfo master)
        {
            Master = master;
        }
    }
}
