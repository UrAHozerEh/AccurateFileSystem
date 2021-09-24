using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem.AccurateDrawingDevices
{
    public class WindowsDrawingDevice : AccurateDrawingDevice
    {
        public CanvasDrawingSession Session { get; }

        public WindowsDrawingDevice(CanvasDrawingSession session)
        {
            Session = session;
        }

        public override void DrawRectangle(Rect rect, Color color, float opacity = 1)
        {
            throw new NotImplementedException();
        }

        public override void FillRectangle(Rect rect, Color color, float opacity = 1)
        {
            throw new NotImplementedException();
        }

        public override void DrawLine(double x1, double y1, double x2, double y2, Color color, double thickness = 1, float opacity = 1)
        {
            throw new NotImplementedException();
        }

        public override void DrawLines(List<Point> points, Color color, double thickness = 1, float opacity = 1)
        {
            throw new NotImplementedException();
        }

        public override void FillLines(List<Point> points, Color color, float opacity = 1)
        {
            throw new NotImplementedException();
        }

        public override Rect GetTextSize(string text, AccurateTextFormat textFormat)
        {
            throw new NotImplementedException();
        }

        public override void DrawTextCenteredOn(string text, AccurateTextFormat textFormat, Color color, float centerX, float centerY, float rotation = 0)
        {
            throw new NotImplementedException();
        }

        public override void DrawText(string text, AccurateTextFormat textFormat, Color color, Rect drawArea, float rotation = 0)
        {
            throw new NotImplementedException();
        }
    }
}
