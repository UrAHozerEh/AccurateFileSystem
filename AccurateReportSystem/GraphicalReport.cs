using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
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
        public ReportMarginInfo MarginInfo { get; set; } = new ReportMarginInfo();
        public static int DEFAULT_DPI = 96;
        public static int DIGITS_TO_ROUND = 2;

        public List<CanvasRenderTarget> GetImages(double startFootage, double endFootage, double overlap = 100, float dpi = 150)
        {
            var pageArea = new Rect(0, 0, DEFAULT_DPI * 11, DEFAULT_DPI * 8.5);
            var drawArea = new Rect(MarginInfo.Left * DEFAULT_DPI, MarginInfo.Top * DEFAULT_DPI, (11 - MarginInfo.MarginWidth) * DEFAULT_DPI, (8.5 - MarginInfo.MarginHeight) * DEFAULT_DPI);
            var totalFootage = endFootage - startFootage;
            var pages = PageSetup.GetAllPages(startFootage, totalFootage, overlap);
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            var list = new List<CanvasRenderTarget>();
            foreach (var page in pages)
            {
                CanvasRenderTarget offscreen = new CanvasRenderTarget(device, (float)pageArea.Width, (float)pageArea.Height, dpi);
                using (CanvasDrawingSession session = offscreen.CreateDrawingSession())
                {
                    session.Clear(Colors.White);
                    session.TextRenderingParameters = new CanvasTextRenderingParameters(CanvasTextRenderingMode.NaturalSymmetric, CanvasTextGridFit.Default);
                    Container.Draw(page, session, drawArea);
                }
                list.Add(offscreen);
            }

            return list;
        }
    }

    public class ReportMarginInfo
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
        public double MarginWidth => Left + Right;
        public double MarginHeight => Top + Bottom;

        public ReportMarginInfo()
        {
            Top = 1;
            Left = 0.25;
            Right = 0.25;
            Bottom = 0.25;
        }

        public ReportMarginInfo(double allSides)
        {
            Top = Left = Right = Bottom = allSides;
        }

        public ReportMarginInfo(double top, double allOthers)
        {
            Top = top;
            Left = Right = Bottom = allOthers;
        }
    }
}
