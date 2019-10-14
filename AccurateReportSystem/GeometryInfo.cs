using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace AccurateReportSystem
{
    public class GeometryInfo
    {
        public CanvasGeometry Geometry { get; set; }
        public Color Color { get; set; }

        public ICanvasBrush GetCanvasBrush(ICanvasResourceCreator creator)
        {
            return new CanvasSolidColorBrush(creator, Color);
        }
    }
}
