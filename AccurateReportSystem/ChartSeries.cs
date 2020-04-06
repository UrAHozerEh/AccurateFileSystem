using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

namespace AccurateReportSystem
{
    public abstract class ChartSeries : Series
    {
        public float HeightInches { get; set; }
        public virtual float Height => (float)Math.Round(HeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        public abstract void Draw(PageInformation page, CanvasDrawingSession session, Rect drawArea, TransformInformation1d transform);
    }

    public class ArrowInfo
    {
        public float HeightInches { get; set; }
        public float Height => (float)Math.Round(HeightInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);
        public float WidthInches { get; set; }
        public float Width => (float)Math.Round(WidthInches * GraphicalReport.DEFAULT_DIP, GraphicalReport.DIGITS_TO_ROUND);

        public float FinWidthPercent;
        public float FinHeightPercent;
        public float TailPercent;
        private float FinXShift => (float)Math.Round(Width * FinWidthPercent, GraphicalReport.DIGITS_TO_ROUND);
        private float FinYShift => (float)Math.Round(Height * FinHeightPercent / 2, GraphicalReport.DIGITS_TO_ROUND);
        private float TailYShift => (float)Math.Round(TailPercent * FinYShift, GraphicalReport.DIGITS_TO_ROUND);

        public Color? LineColor;
        public float LineThickness;
        public Color? FillColor;

        public bool IsDotted;

        public Color FontColor;
        public float FontSize;

        public ArrowInfo(float width, float height, Color? lineColor, Color? fillColor, float finWidthPercent = 0.25f, float finHeightPercent = 0.95f, float tailPercent = 0.5f, float lineThickness = 1f, bool isDotted = false, float fontSize = 10f, Color? fontColor = null)
        {
            WidthInches = width;
            HeightInches = height;

            FinWidthPercent = finWidthPercent;
            FinHeightPercent = finHeightPercent;
            TailPercent = tailPercent;

            LineColor = lineColor;
            LineThickness = lineThickness;
            IsDotted = isDotted;

            FillColor = fillColor;

            FontSize = fontSize;
            FontColor = fontColor ?? Colors.Black;

        }

        public void Draw(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        {
            Draw(session, x, y, (float)direction, text);
        }

        public void Draw(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        {
            var radians = (float)(rotation * Math.PI / 180);
            var rotationMatrix = Matrix3x2.CreateRotation(radians, new Vector2(x, y));
            using (var pathBuilder = new CanvasPathBuilder(session))
            {
                pathBuilder.BeginFigure(x, y);
                pathBuilder.AddLine(x - FinXShift, y - FinYShift);
                pathBuilder.AddLine(x - FinXShift, y - TailYShift);
                pathBuilder.AddLine(x - Width, y - TailYShift);
                pathBuilder.AddLine(x - Width, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + FinYShift);
                pathBuilder.AddLine(x, y);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);

                using (var geo = CanvasGeometry.CreatePath(pathBuilder))
                using (var transformedGeo = geo.Transform(rotationMatrix))
                using (var strokeStyle = new CanvasStrokeStyle())
                {
                    strokeStyle.TransformBehavior = CanvasStrokeTransformBehavior.Fixed;
                    strokeStyle.DashStyle = IsDotted ? CanvasDashStyle.Dash : CanvasDashStyle.Solid;

                    if (FillColor.HasValue)
                        session.FillGeometry(transformedGeo, FillColor.Value);
                    if (LineColor.HasValue)
                        session.DrawGeometry(transformedGeo, LineColor.Value, LineThickness, strokeStyle);
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    using (var format = new CanvasTextFormat())
                    {
                        format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                        format.WordWrapping = CanvasWordWrapping.WholeWord;
                        format.FontSize = FontSize;
                        format.FontFamily = "Arial";
                        format.FontWeight = FontWeights.Thin;
                        format.FontStyle = FontStyle.Normal;
                        var textBoxHeight = TailYShift * 2;
                        Rect textRect = new Rect(x - Width, y - TailYShift, Width, textBoxHeight);
                        Matrix3x2 textRotation = rotationMatrix;
                        if (rotation > 90 && rotation <= 270)
                        {
                            textRect = new Rect(x, y - TailYShift, Width, textBoxHeight);
                            var tempRadians = (float)((rotation - 180) * Math.PI / 180);
                            textRotation = Matrix3x2.CreateRotation(tempRadians, new Vector2(x, y)); ;
                        }
                        using (var layout = new CanvasTextLayout(session, text, format, (float)textRect.Width, (float)textRect.Height))
                        using (var geometry = CanvasGeometry.CreateText(layout))
                        {
                            var bounds = layout.DrawBounds;
                            var translateMatrix = bounds.CreateTranslateMiddleTo(textRect);
                            using (var rotatedGeo = geometry.Transform(translateMatrix).Transform(textRotation))
                            {
                                session.FillGeometry(rotatedGeo, LineColor ?? Colors.Black);
                            }
                        }
                    }
                }
            }
        }

        public void DrawLeftOf(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        {
            var radians = (float)(rotation * Math.PI / 180);
            var newX = x + (float)((Math.Cos(radians) - 1) * Width / 2);
            Draw(session, newX, y, rotation, text);
        }

        public void DrawLeftOf(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        {
            DrawLeftOf(session, x, y, (float)direction, text);
        }

        public void DrawRightOf(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        {
            var radians = (float)(rotation * Math.PI / 180);
            var newX = x + (float)((Math.Cos(radians) + 1) * Width / 2);
            Draw(session, newX, y, rotation, text);
        }

        public void DrawRightOf(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        {
            DrawRightOf(session, x, y, (float)direction, text);
        }

        public void DrawOnTopOf(CanvasDrawingSession session, float x, float y, float rotation, string text = null)
        {
            var radians = (float)(rotation * Math.PI / 180);
            var newX = x + (float)(Math.Cos(radians) * Width / 2);
            Draw(session, newX, y, rotation, text);
        }

        public void DrawOnTopOf(CanvasDrawingSession session, float x, float y, Direction direction, string text = null)
        {
            DrawOnTopOf(session, x, y, (float)direction, text);
        }


        public static void Draws(CanvasDrawingSession session, float x, float y, float width, float height, float rotation, Color lineColor, float lineThickness, Color? fillColor = null, bool isDotted = false, string text = null, float fontSize = 10f)
        {

            var FinWidthPercent = 0.25f;
            var FinHeightPercent = 0.95f;
            var TailPercent = 0.5f;

            var FinXShift = (float)Math.Round(width * FinWidthPercent, GraphicalReport.DIGITS_TO_ROUND);
            var FinYShift = (float)Math.Round(height * FinHeightPercent / 2, GraphicalReport.DIGITS_TO_ROUND);
            var TailYShift = (float)Math.Round(TailPercent * FinYShift, GraphicalReport.DIGITS_TO_ROUND);

            var radians = (float)(rotation * Math.PI / 180);
            var rotationMatrix = Matrix3x2.CreateRotation(radians, new Vector2(x, y));
            using (var pathBuilder = new CanvasPathBuilder(session))
            {
                pathBuilder.BeginFigure(x, y);
                pathBuilder.AddLine(x - FinXShift, y - FinYShift);
                pathBuilder.AddLine(x - FinXShift, y - TailYShift);
                pathBuilder.AddLine(x - width, y - TailYShift);
                pathBuilder.AddLine(x - width, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + TailYShift);
                pathBuilder.AddLine(x - FinXShift, y + FinYShift);
                pathBuilder.AddLine(x, y);
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);

                using (var geo = CanvasGeometry.CreatePath(pathBuilder))
                using (var transformedGeo = geo.Transform(rotationMatrix))
                using (var strokeStyle = new CanvasStrokeStyle())
                {
                    strokeStyle.TransformBehavior = CanvasStrokeTransformBehavior.Fixed;
                    strokeStyle.DashStyle = isDotted ? CanvasDashStyle.Dash : CanvasDashStyle.Solid;
                    if (fillColor.HasValue)
                        session.FillGeometry(transformedGeo, fillColor.Value);
                    session.DrawGeometry(transformedGeo, lineColor, lineThickness, strokeStyle);
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    using (var format = new CanvasTextFormat())
                    {
                        format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                        format.WordWrapping = CanvasWordWrapping.WholeWord;
                        format.FontSize = fontSize;
                        format.FontFamily = "Arial";
                        format.FontWeight = FontWeights.Thin;
                        format.FontStyle = FontStyle.Normal;
                        var textHeight = TailYShift * 2;
                        Rect textRect = new Rect(x - width, y - TailYShift, width, textHeight);
                        Matrix3x2 textRotation = rotationMatrix;
                        if (rotation > 90 && rotation <= 270)
                        {
                            textRect = new Rect(x, y - TailYShift, width, textHeight);
                            var tempRadians = (float)((rotation - 180) * Math.PI / 180);
                            textRotation = Matrix3x2.CreateRotation(tempRadians, new Vector2(x, y)); ;
                        }
                        using (var layout = new CanvasTextLayout(session, text, format, (float)textRect.Width, (float)textRect.Height))
                        using (var geometry = CanvasGeometry.CreateText(layout))
                        {
                            var bounds = layout.DrawBounds;
                            var translateMatrix = bounds.CreateTranslateMiddleTo(textRect);
                            using (var rotatedGeo = geometry.Transform(translateMatrix).Transform(textRotation))
                            {
                                session.FillGeometry(rotatedGeo, lineColor);
                            }
                        }

                    }
                }
            }
        }

        public static void Draws(CanvasDrawingSession session, float x, float y, float width, float height, Direction direction, Color lineColor, float lineThickness, Color? fillColor = null, bool isDotted = false, string text = null, float fontSize = 8f)
        {
            Draws(session, x, y, width, height, (float)direction, lineColor, lineThickness, fillColor, isDotted, text, fontSize);
        }

        public enum Direction : int
        {
            Right = 0,
            Left = 180,
            Up = 270,
            Down = 90
        }
    }
}
