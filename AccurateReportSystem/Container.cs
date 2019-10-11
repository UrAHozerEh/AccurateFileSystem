using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace AccurateReportSystem
{
    public abstract class Container
    {
        public Container Parent { get; set; }
        public GraphicalReport Report { get; set; }
        public Container Child { get; set; }
        public Rect DrawArea { get; set; }
        public Container(Rect drawArea, Container parent, GraphicalReport report)
        {
            DrawArea = drawArea;
            Parent = parent;
            Report = report;
        }

        public abstract void Draw(PageInformation pageInformation, CanvasDrawingSession session);
    }
}
