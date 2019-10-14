using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace AccurateReportSystem
{
    public class GraphicalReport : Windows.UI.Xaml.Controls.Canvas
    {
        public PageSetup PageSetup { get; set; } = new PageSetup();
        public Container Container { get; set; }
        public Rect DrawArea { get; set; } = new Rect(0, 0, 11 * DEFAULT_DPI, 8.5 * DEFAULT_DPI);
        public Size DrawAreaSize => new Size(DrawArea.Width, DrawArea.Height);
        private static int DEFAULT_DPI = 96;

        public List<CanvasRenderTarget> GetImages(double startFootage, double endFootage)
        {
            var totalFootage = endFootage - startFootage;
            var pages = PageSetup.GetAllPages(startFootage, totalFootage);
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            var list = new List<CanvasRenderTarget>();
            foreach (var page in pages)
            {
                CanvasRenderTarget offscreen = new CanvasRenderTarget(device, (float)DrawArea.Width, (float)DrawArea.Height, 150);
                using (CanvasDrawingSession session = offscreen.CreateDrawingSession())
                {
                    session.Clear(Colors.White);
                    Container.Draw(page, session);
                }
                list.Add(offscreen);
            }

            return list;
        }
    }
}
