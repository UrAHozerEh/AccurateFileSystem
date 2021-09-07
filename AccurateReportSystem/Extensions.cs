using AccurateFileSystem;
using Microsoft.Graphics.Canvas.Geometry;
using PdfSharp.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;

namespace AccurateReportSystem
{
    public static class Extensions
    {

        public static XColor ToPdfColor(this Color color)
        {
            return XColor.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static XBrush ToSolidPdfBrush(this Color color)
        {
            return new XSolidBrush(color.ToPdfColor());
        }

        public static XPen ToPdfPen(this Color color, double width = 1)
        {
            return new XPen(color.ToPdfColor(), width);
        }

        public static void BeginFigure(this CanvasPathBuilder path, (float X, float Y) value)
        {
            path.BeginFigure(value.X, value.Y);
        }

        public static void AddLine(this CanvasPathBuilder path, (float X, float Y) value)
        {
            path.AddLine(value.X, value.Y);
        }

        public static (float X, float Y) GetMiddlePoint(this Rect rect)
        {
            var x = (float)(rect.X + rect.Width / 2);
            var y = (float)(rect.Y + rect.Height / 2);
            return (x, y);
        }

        public static Matrix3x2 CreateTranslateMiddleTo(this Rect rect, (float X, float Y) middle)
        {
            var (boundsMiddleX, boundsMiddleY) = rect.GetMiddlePoint();
            var translateY = middle.Y - boundsMiddleY;
            var translateX = middle.X - boundsMiddleX;
            return Matrix3x2.CreateTranslation(translateX, translateY);
        }

        public static Matrix3x2 CreateTranslateMiddleTo(this Rect rect, Rect other)
        {
            var otherMiddle = other.GetMiddlePoint();
            return rect.CreateTranslateMiddleTo(otherMiddle);
        }

        public static Matrix3x2 CreateRotationAroundMiddle(this Rect rect, float rotationDegrees)
        {
            var (x, y) = rect.GetMiddlePoint();
            var rotationMatrix = Matrix3x2.CreateRotation((float)(rotationDegrees * Math.PI / 180), new Vector2(x, y));
            return rotationMatrix;
        }

        public static bool DoesIntersectPage(this ReconnectTestStationRead reconnect, PageInformation page)
        {
            var reconStart = reconnect.StartPoint.Footage;
            var reconEnd = reconnect.EndPoint.Footage;
            var pageStart = page.StartFootage;
            var pageEnd = page.EndFootage;

            if(reconStart >= pageStart && reconStart <= pageEnd)
                return true;
            if (pageStart >= reconStart && pageStart <= reconEnd)
                return true;
            return false;
        }

        public static (double Footage1, double Footage2) GetClosestFootages(this ReconnectTestStationRead recon1, ReconnectTestStationRead recon2)
        {
            var startStartDiff = Math.Abs(recon1.StartPoint.Footage - recon2.StartPoint.Footage);
            var startEndDiff = Math.Abs(recon1.StartPoint.Footage - recon2.EndPoint.Footage);
            var endStartDiff = Math.Abs(recon1.EndPoint.Footage - recon2.StartPoint.Footage);
            var endEndDiff = Math.Abs(recon1.EndPoint.Footage - recon2.EndPoint.Footage);

            if (startStartDiff < startEndDiff && startStartDiff < endStartDiff && startStartDiff < endEndDiff)
                return (recon1.StartPoint.Footage, recon2.StartPoint.Footage);
            else if (startEndDiff < endStartDiff && startEndDiff < endEndDiff)
                return (recon1.StartPoint.Footage, recon2.EndPoint.Footage);
            else if(endStartDiff < endEndDiff)
                return (recon1.EndPoint.Footage, recon2.StartPoint.Footage);
            return (recon1.EndPoint.Footage, recon2.EndPoint.Footage);
        }

        public static string GetDisplayName(this PGESeverity severity)
        {
            switch (severity)
            {
                case PGESeverity.NRI:
                    return "NRI";
                case PGESeverity.Minor:
                    return "Minor";
                case PGESeverity.Moderate:
                    return "Moderate";
                case PGESeverity.Severe:
                    return "Severe";
                default:
                    throw new ArgumentException();
            }
        }

        public static PGESeverity GetWorseOf(this PGESeverity cur, PGESeverity other)
        {
            if (cur == PGESeverity.Severe || other == PGESeverity.Severe)
                return PGESeverity.Severe;
            if (cur == PGESeverity.Moderate || other == PGESeverity.Moderate)
                return PGESeverity.Moderate;
            if (cur == PGESeverity.Minor || other == PGESeverity.Minor)
                return PGESeverity.Minor;
            return PGESeverity.NRI;
        }
    }
}
