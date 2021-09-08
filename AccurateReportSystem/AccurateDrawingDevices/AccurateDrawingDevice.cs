using Microsoft.Graphics.Canvas;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem.AccurateDrawingDevices
{
    public abstract class AccurateDrawingDevice
    {
        public abstract void DrawRectangle(Rect rect, Color color, float opacity = 1);
        public void DrawRectangle(double x, double y, double width, double height, Color color, float opacity = 1)
        {
            var rect = new Rect(x, y, width, height);
            DrawRectangle(rect, color, opacity);
        }

        public abstract void FillRectangle(Rect rect, Color color, float opacity = 1);
        public void FillRectangle(double x, double y, double width, double height, Color color, float opacity = 1)
        {
            var rect = new Rect(x, y, width, height);
            FillRectangle(rect, color, opacity);
        }

        public abstract void DrawLine(double x1, double y1, double x2, double y2, Color color, double thickness = 1, float opacity = 1);

        public abstract void DrawLines(List<Point> points, Color color, double thickness = 1, float opacity = 1);
        public abstract void FillLines(List<Point> points, Color color, float opacity = 1);

        public abstract Rect GetTextSize(string text, AccurateTextFormat textFormat);
        public abstract void DrawFormattedText(string text, AccurateTextFormat textFormat, Color color, float centerX, float centerY, float rotation = 0);
        public abstract void DrawFormattedText(string text, AccurateTextFormat textFormat, Color color, Rect drawArea, float rotation = 0);
        public void DrawFormattedText(string text, AccurateTextFormat textFormat, Color color, double centerX, double centerY, float rotation = 0)
        {
            DrawFormattedText(text, textFormat, color, (float)centerX, (float)centerY, rotation);
        }
    }
}
