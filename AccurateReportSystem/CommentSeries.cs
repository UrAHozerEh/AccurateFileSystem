using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AccurateReportSystem
{
    public class CommentSeries
    {
        public List<(double footage, double value)> Values { get; set; }

        public GeometryInfo GetGeometry(PageInformation page, Rect drawArea)
        {
            var format = new CanvasTextFormat();
            format.HorizontalAlignment = CanvasHorizontalAlignment.Right;
            format.WordWrapping = CanvasWordWrapping.WholeWord;
        }
    }
}
