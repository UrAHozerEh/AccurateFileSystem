using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
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
        private static int DEFAULT_DPI = 300;

        public async Task<List<byte[]>> GetImages(double startFootage, double endFootage)
        {
            Measure(DrawAreaSize);
            var image = new WriteableBitmap((int)DrawArea.Width, (int)DrawArea.Height);
            var totalFootage = endFootage - startFootage;
            var pages = PageSetup.GetAllPages(startFootage, totalFootage);
            CanvasDevice device = CanvasDevice.GetSharedDevice();

            foreach (var page in pages)
            {
                CanvasRenderTarget offscreen = new CanvasRenderTarget(device, (float)DrawArea.Width, (float)DrawArea.Height, 300);
                using (CanvasDrawingSession ds = offscreen.CreateDrawingSession())
                {
                    Container.Draw(page);
                    Arrange(DrawArea);
                    RenderTargetBitmap rtb = new RenderTargetBitmap();
                    await rtb.RenderAsync(this);
                    var pixelBuffer = await rtb.GetPixelsAsync();
                    DataReader dataReader = DataReader.FromBuffer(pixelBuffer);
                    byte[] bytes = new byte[pixelBuffer.Length];
                    dataReader.ReadBytes(bytes);
                    list.Add(bytes);
                }
            }


            return list;
        }
    }
}
