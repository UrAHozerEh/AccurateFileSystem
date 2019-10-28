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
        public Container Child { get; set; }
        public Container()
        {
        }

        public abstract void Draw(PageInformation pageInformation, CanvasDrawingSession session, Rect drawArea);

        //TODO: Add function to get requested container size. probably return double.maxvalue for things that want as much space as possible (graph)
    }
}
