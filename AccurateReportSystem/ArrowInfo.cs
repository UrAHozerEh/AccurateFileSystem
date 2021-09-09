using AccurateReportSystem.AccurateDrawingDevices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace AccurateReportSystem
{
    public class ArrowInfo
    {
        public float HeightInches { get; set; }
        public float Height => (float)Math.Round(HeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public float WidthInches { get; set; }
        public float Width => (float)Math.Round(WidthInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        public float FinWidthPercent { get; set; }
        public float FinHeightPercent { get; set; }
        public float TailPercent { get; set; }
        private float FinXShift => (float)Math.Round(Width * FinWidthPercent, GraphicalReport.DIGITS_TO_ROUND);
        private float FinYShift => (float)Math.Round(Height * FinHeightPercent / 2, GraphicalReport.DIGITS_TO_ROUND);
        private float TailYShift => (float)Math.Round(TailPercent * FinYShift, GraphicalReport.DIGITS_TO_ROUND);

        public Color? LineColor { get; set; }
        public float LineThickness { get; set; }
        public Color? FillColor { get; set; }

        public Color FontColor { get; set; }
        public float FontSize { get; set; }

        public ArrowInfo(float width, float height, Color? lineColor, Color? fillColor, float finWidthPercent = 0.25f, float finHeightPercent = 0.95f, float tailPercent = 0.5f, float lineThickness = 1f, float fontSize = 10f, Color? fontColor = null)
        {
            WidthInches = width;
            HeightInches = height;

            FinWidthPercent = finWidthPercent;
            FinHeightPercent = finHeightPercent;
            TailPercent = tailPercent;

            LineColor = lineColor;
            LineThickness = lineThickness;

            FillColor = fillColor;

            FontSize = fontSize;
            FontColor = fontColor ?? Colors.Black;
        }

        private void DrawPoints(AccurateDrawingDevice device, List<Point> points, Direction direction, string text)
        {
            if (FillColor != null)
            {
                device.FillLines(points, FillColor.Value);
            }
            if (LineColor != null)
            {
                device.DrawLines(points, LineColor.Value, LineThickness);
            }
            if (!string.IsNullOrWhiteSpace(text))
            {
                DrawText(device, direction, text, points);
            }
        }

        private void DrawText(AccurateDrawingDevice device, Direction direction, string text, List<Point> points)
        {
            var tip = points[0];
            var tipX = tip.X;
            var tipY = tip.Y;

            var topLeft = new Point();
            var bottomRight = new Point();
            var rotation = 0f;

            switch (direction)
            {
                case Direction.Right:
                    topLeft = new Point(tipX - Width, tipY - TailYShift);
                    bottomRight = new Point(tipX, tipY + TailYShift);
                    break;
                case Direction.Left:
                    topLeft = new Point(tipX, tipY - TailYShift);
                    bottomRight = new Point(tipX + Width, tipY + TailYShift);
                    break;
                case Direction.Up:
                    topLeft = new Point(tipX - TailYShift, tipY);
                    bottomRight = new Point(tipX + TailYShift, tipY + Width);
                    rotation = 270;
                    break;
                case Direction.Down:
                    topLeft = new Point(tipX - TailYShift, tipY - Width);
                    bottomRight = new Point(tipX + TailYShift, tipY);
                    rotation = 90;
                    break;
            }
            var textBox = new Rect(topLeft, bottomRight);

            var format = new AccurateTextFormat()
            {
                FontSize = FontSize
            };
            device.DrawText(text, format, FontColor, textBox, rotation);
        }

        public void DrawCenteredOn(AccurateDrawingDevice device, float x, float y, Direction direction, string text = null)
        {
            switch (direction)
            {
                case Direction.Left:
                    x -= Width / 2;
                    break;
                case Direction.Right:
                    x += Width / 2;
                    break;
                case Direction.Up:
                    y -= Width / 2;
                    x -= Height / 4;
                    break;
                case Direction.Down:
                    y += Width / 2;
                    x -= Height / 4;
                    break;
                default:
                    break;
            }
            var points = GetPoints(x, y, direction);
            DrawPoints(device, points, direction, text);
        }

        public void DrawCenteredOn(AccurateDrawingDevice device, float x, Rect drawArea, Direction direction, string text = null)
        {
            DrawCenteredOn(device, x, drawArea.GetMiddlePoint().Y, direction, text);
        }

        public void DrawLeftOf(AccurateDrawingDevice device, float x, float y, Direction direction, string text = null)
        {
            switch (direction)
            {
                case Direction.Left:
                    x -= Width;
                    break;
                case Direction.Up:
                    y -= Width / 2;
                    x -= Height / 2;
                    break;
                case Direction.Down:
                    y += Width / 2;
                    x -= Height / 2;
                    break;
                default:
                    break;
            }
            var points = GetPoints(x, y, direction);
            DrawPoints(device, points, direction, text);

        }

        public void DrawLeftOf(AccurateDrawingDevice device, float x, Rect drawArea, Direction direction, string text = null)
        {
            DrawLeftOf(device, x, drawArea.GetMiddlePoint().Y, direction, text);
        }

        public void DrawRightOf(AccurateDrawingDevice device, float x, float y, Direction direction, string text = null)
        {
            switch (direction)
            {
                case Direction.Right:
                    x += Width;
                    break;
                case Direction.Up:
                    y -= Width / 2;
                    x += Height / 2;
                    break;
                case Direction.Down:
                    y += Width / 2;
                    x += Height / 2;
                    break;
                default:
                    break;
            }
            var points = GetPoints(x, y, direction);
            DrawPoints(device, points, direction, text);
        }

        public void DrawRightOf(AccurateDrawingDevice device, float x, Rect drawArea, Direction direction, string text = null)
        {
            DrawRightOf(device, x, drawArea.GetMiddlePoint().Y, direction, text);
        }

        private List<Point> GetPoints(float x, float y, Direction direction)
        {
            switch (direction)
            {
                case Direction.Right:
                    return GetRightPoints(x, y);
                case Direction.Left:
                    return GetLeftPoints(x, y);
                case Direction.Up:
                    return GetUpPoints(x, y);
                case Direction.Down:
                    return GetDownPoints(x, y);
            }
            return null;
        }

        private List<Point> GetRightPoints(float x, float y)
        {
            var output = new List<Point>();
            output.Add(new Point(x, y));
            output.Add(new Point(x - FinXShift, y - FinYShift));
            output.Add(new Point(x - FinXShift, y - TailYShift));
            output.Add(new Point(x - Width, y - TailYShift));
            output.Add(new Point(x - Width, y + TailYShift));
            output.Add(new Point(x - FinXShift, y + TailYShift));
            output.Add(new Point(x - FinXShift, y + FinYShift));
            output.Add(new Point(x, y));
            return output;
        }

        private List<Point> GetLeftPoints(float x, float y)
        {
            var output = new List<Point>();
            output.Add(new Point(x, y));
            output.Add(new Point(x + FinXShift, y - FinYShift));
            output.Add(new Point(x + FinXShift, y - TailYShift));
            output.Add(new Point(x + Width, y - TailYShift));
            output.Add(new Point(x + Width, y + TailYShift));
            output.Add(new Point(x + FinXShift, y + TailYShift));
            output.Add(new Point(x + FinXShift, y + FinYShift));
            output.Add(new Point(x, y));
            return output;
        }
        private List<Point> GetUpPoints(float x, float y)
        {
            var output = new List<Point>();
            output.Add(new Point(x, y));
            output.Add(new Point(x - FinYShift, y - FinXShift));
            output.Add(new Point(x - TailYShift, y - FinXShift));
            output.Add(new Point(x - TailYShift, y - Width));
            output.Add(new Point(x + TailYShift, y - Width));
            output.Add(new Point(x + TailYShift, y - FinXShift));
            output.Add(new Point(x + FinYShift, y - FinXShift));
            output.Add(new Point(x, y));
            return output;
        }
        private List<Point> GetDownPoints(float x, float y)
        {
            var output = new List<Point>();
            output.Add(new Point(x, y));
            output.Add(new Point(x - FinYShift, y + FinXShift));
            output.Add(new Point(x - TailYShift, y + FinXShift));
            output.Add(new Point(x - TailYShift, y + Width));
            output.Add(new Point(x + TailYShift, y + Width));
            output.Add(new Point(x + TailYShift, y + FinXShift));
            output.Add(new Point(x + FinYShift, y + FinXShift));
            output.Add(new Point(x, y));
            return output;
        }

        //        if (!string.IsNullOrWhiteSpace(text))
        //        {
        //            using (var format = new CanvasTextFormat())
        //            {
        //                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
        //                format.WordWrapping = CanvasWordWrapping.WholeWord;
        //                format.FontSize = FontSize;
        //                format.FontFamily = "Arial";
        //                format.FontWeight = FontWeights.Thin;
        //                format.FontStyle = FontStyle.Normal;
        //                var textBoxHeight = TailYShift * 2;
        //                Rect textRect = new Rect(x - Width, y - TailYShift, Width, textBoxHeight);
        //                Matrix3x2 textRotation = rotationMatrix;
        //                if (rotation > 90 && rotation <= 270)
        //                {
        //                    textRect = new Rect(x, y - TailYShift, Width, textBoxHeight);
        //                    var tempRadians = (float)((rotation - 180) * Math.PI / 180);
        //                    textRotation = Matrix3x2.CreateRotation(tempRadians, new Vector2(x, y)); ;
        //                }
        //                using (var layout = new CanvasTextLayout(device, text, format, (float)textRect.Width, (float)textRect.Height))
        //                using (var geometry = CanvasGeometry.CreateText(layout))
        //                {
        //                    var bounds = layout.DrawBounds;
        //                    var translateMatrix = bounds.CreateTranslateMiddleTo(textRect);
        //                    using (var rotatedGeo = geometry.Transform(translateMatrix).Transform(textRotation))
        //                    {
        //                        device.FillGeometry(rotatedGeo, LineColor ?? Colors.Black);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        //public void DrawLeftOf(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        //{
        //    var radians = (float)(rotation * Math.PI / 180);
        //    var newX = x + (float)((Math.Cos(radians) - 1) * Width / 2);
        //    Draw(session, newX, y, rotation, text);
        //}

        //public void DrawLeftOf(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        //{
        //    DrawLeftOf(session, x, y, (float)direction, text);
        //}

        //public void DrawRightOf(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        //{
        //    var radians = (float)(rotation * Math.PI / 180);
        //    var newX = x + (float)((Math.Cos(radians) + 1) * Width / 2);
        //    Draw(session, newX, y, rotation, text);
        //}

        //public void DrawRightOf(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        //{
        //    DrawRightOf(session, x, y, (float)direction, text);
        //}

        //public void DrawOnTopOf(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        //{
        //    var radians = (float)(rotation * Math.PI / 180);
        //    var newX = x + (float)(Math.Cos(radians) * Width / 2);
        //    Draw(session, newX, y, rotation, text);
        //}

        //public void DrawOnTopOf(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        //{
        //    DrawOnTopOf(session, x, y, (float)direction, text);
        //}


        //public static void Draws(CanvasDrawingSession session, float x, float y, float width, float height, float rotation, Color lineColor, float lineThickness, Color? fillColor = null, bool isDotted = false, string text = null, float fontSize = 10f)
        //{

        //    var FinWidthPercent = 0.25f;
        //    var FinHeightPercent = 0.95f;
        //    var TailPercent = 0.5f;

        //    var FinXShift = (float)Math.Round(width * FinWidthPercent, GraphicalReport.DIGITS_TO_ROUND);
        //    var FinYShift = (float)Math.Round(height * FinHeightPercent / 2, GraphicalReport.DIGITS_TO_ROUND);
        //    var TailYShift = (float)Math.Round(TailPercent * FinYShift, GraphicalReport.DIGITS_TO_ROUND);

        //    var radians = (float)(rotation * Math.PI / 180);
        //    var rotationMatrix = Matrix3x2.CreateRotation(radians, new Vector2(x, y));
        //    using (var pathBuilder = new CanvasPathBuilder(session))
        //    {
        //        pathBuilder.BeginFigure(x, y);
        //        pathBuilder.AddLine(x - FinXShift, y - FinYShift);
        //        pathBuilder.AddLine(x - FinXShift, y - TailYShift);
        //        pathBuilder.AddLine(x - width, y - TailYShift);
        //        pathBuilder.AddLine(x - width, y + TailYShift);
        //        pathBuilder.AddLine(x - FinXShift, y + TailYShift);
        //        pathBuilder.AddLine(x - FinXShift, y + FinYShift);
        //        pathBuilder.AddLine(x, y);
        //        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        //        using (var geo = CanvasGeometry.CreatePath(pathBuilder))
        //        using (var transformedGeo = geo.Transform(rotationMatrix))
        //        using (var strokeStyle = new CanvasStrokeStyle())
        //        {
        //            strokeStyle.TransformBehavior = CanvasStrokeTransformBehavior.Fixed;
        //            strokeStyle.DashStyle = isDotted ? CanvasDashStyle.Dash : CanvasDashStyle.Solid;
        //            if (fillColor.HasValue)
        //                session.FillGeometry(transformedGeo, fillColor.Value);
        //            session.DrawGeometry(transformedGeo, lineColor, lineThickness, strokeStyle);
        //        }

        //        if (!string.IsNullOrWhiteSpace(text))
        //        {
        //            using (var format = new CanvasTextFormat())
        //            {
        //                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
        //                format.WordWrapping = CanvasWordWrapping.WholeWord;
        //                format.FontSize = fontSize;
        //                format.FontFamily = "Arial";
        //                format.FontWeight = FontWeights.Thin;
        //                format.FontStyle = FontStyle.Normal;
        //                var textHeight = TailYShift * 2;
        //                Rect textRect = new Rect(x - width, y - TailYShift, width, textHeight);
        //                Matrix3x2 textRotation = rotationMatrix;
        //                if (rotation > 90 && rotation <= 270)
        //                {
        //                    textRect = new Rect(x, y - TailYShift, width, textHeight);
        //                    var tempRadians = (float)((rotation - 180) * Math.PI / 180);
        //                    textRotation = Matrix3x2.CreateRotation(tempRadians, new Vector2(x, y)); ;
        //                }
        //                using (var layout = new CanvasTextLayout(session, text, format, (float)textRect.Width, (float)textRect.Height))
        //                using (var geometry = CanvasGeometry.CreateText(layout))
        //                {
        //                    var bounds = layout.DrawBounds;
        //                    var translateMatrix = bounds.CreateTranslateMiddleTo(textRect);
        //                    using (var rotatedGeo = geometry.Transform(translateMatrix).Transform(textRotation))
        //                    {
        //                        session.FillGeometry(rotatedGeo, lineColor);
        //                    }
        //                }

        //            }
        //        }
        //    }
        //}

        //public static void Draws(CanvasDrawingSession session, float x, float y, float width, float height, Direction direction, Color lineColor, float lineThickness, Color? fillColor = null, bool isDotted = false, string text = null, float fontSize = 8f)
        //{
        //    Draws(session, x, y, width, height, (float)direction, lineColor, lineThickness, fillColor, isDotted, text, fontSize);
        //}

        public enum Direction : int
        {
            Right = 0,
            Left = 180,
            Up = 270,
            Down = 90
        }
    }
}
