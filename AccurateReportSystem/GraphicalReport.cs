using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using PdfSharp.Pdf;
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
        public XAxisInfo XAxisInfo { get; set; } = new XAxisInfo();
        public YAxesInfo YAxesInfo { get; set; } = new YAxesInfo();
        public LegendInfo LegendInfo { get; set; } = new LegendInfo();
        public CanvasBitmap Logo { get; set; } = null;

        public static int DEFAULT_DIP = 96;
        public static int DIGITS_TO_ROUND = 2;

        public CanvasRenderTarget GetImage(PageInformation page, float dpi = 300)
        {
            var pageAreaWidth = Math.Round(DEFAULT_DIP * 11.0, DIGITS_TO_ROUND);
            var pageAreaHeight = Math.Round(DEFAULT_DIP * 8.5, DIGITS_TO_ROUND);
            var pageArea = new Rect(0, 0, pageAreaWidth, pageAreaHeight);
            var drawArea = new Rect(MarginInfo.LeftDip, MarginInfo.TopDip, pageAreaWidth - MarginInfo.MarginWidthDip, pageAreaHeight - MarginInfo.MarginHeightDip);

            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget offscreen = new CanvasRenderTarget(device, (float)pageArea.Width, (float)pageArea.Height, dpi);
            using (CanvasDrawingSession session = offscreen.CreateDrawingSession())
            {
                session.Clear(Colors.White);
                if (Logo != null)
                {
                    var scale = Logo.Size.Width / Logo.Size.Height;
                    session.DrawImage(Logo, new Rect((float)MarginInfo.LeftDip * 0.1, (float)MarginInfo.TopDip * 0.1, MarginInfo.TopDip * scale, MarginInfo.TopDip));
                }
                session.TextRenderingParameters = new CanvasTextRenderingParameters(CanvasTextRenderingMode.NaturalSymmetric, CanvasTextGridFit.Default);
                var drawingDevice = new AccurateDrawingDevice(session);
                Container.Draw(page, drawingDevice, drawArea);
            }
            return offscreen;
        }

        public PdfDocument GetPdf(List<PageInformation> pages)
        {
            var output = new PdfDocument();

            foreach(var page in pages)
            {
                var pdfPage = output.AddPage();

            }

            return output;
        }
    }

    public class ReportMarginInfo
    {
        public double Top { get; set; }
        public double TopDip => Math.Round(Top * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double Left { get; set; }
        public double LeftDip => Math.Round(Left * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double Right { get; set; }
        public double RightDip => Math.Round(Right * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double Bottom { get; set; }
        public double BottomDip => Math.Round(Bottom * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double MarginWidth => Left + Right;
        public double MarginWidthDip => Math.Round(MarginWidth * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public double MarginHeight => Top + Bottom;
        public double MarginHeightDip => Math.Round(MarginHeight * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

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
