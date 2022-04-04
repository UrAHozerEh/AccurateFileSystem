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
    public class CommentSeries
    {
        public List<(double footage, string value)> Values { get; set; }
        public float TopEdgePadding { get; set; } = 10f; //TODO: Make this inches.
        public Color LineColor { get; set; } = Colors.Black;
        public Color TextColor { get; set; } = Colors.Black;
        public float LineBuffer { get; set; } = 2f;
        public double CommentBuffer { get; set; } = 5;
        public double EdgeBuffer { get; set; } = 3;
        public BorderType BorderType { get; set; } = BorderType.Full;
        public float PercentOfGraph { get; set; } = 0.5f;
        public bool IsFlippedVertical { get; set; } = false;
        public float BackdropOpacity { get; set; } = 0.5f;
        public Color BackdropColor { get; set; } = Colors.White;
        public StationStyle StationStyle { get; set; } = StationStyle.PlusFoot;
        public string StationSuffix { get; set; } = "] ";
        public string StationPrefix { get; set; } = "[";

        public (GeometryInfo, GeometryInfo, CanvasGeometry) GetGeometry(PageInformation page, Rect drawArea, CanvasDrawingSession session)
        {
            CanvasGeometry commentOutputGeo = null;// = CanvasGeometry.CreateRectangle(device, 0, 0, (float)drawArea.Height, (float)drawArea.Width);
            CanvasGeometry backdropOutputGeo = null;
            CanvasGeometry lineOutputGeo = null;
            Matrix3x2 translation;
            CommentBoundsInfo first = null, last = null;
            using (var format = new CanvasTextFormat())
            {
                format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                format.WordWrapping = CanvasWordWrapping.WholeWord;
                format.FontSize = 8f;
                format.FontFamily = "Arial";
                format.FontWeight = FontWeights.Thin;
                format.FontStyle = FontStyle.Normal;

                for (int i = 0; i < Values.Count; ++i)
                {
                    var (footage, comment) = Values[i];
                    if (footage < page.StartFootage)
                        continue;
                    if (footage > page.EndFootage)
                        break;
                    if (string.IsNullOrWhiteSpace(comment))
                        continue;
                    string prefix;
                    var footString = footage.ToString("F0");
                    switch (StationStyle)
                    {
                        case StationStyle.PlusFoot:
                            footString = footString.PadLeft(3, '0');
                            prefix = $"{StationPrefix}{footString.Insert(footString.Length - 2, "+")}{StationSuffix}";
                            break;
                        case StationStyle.Footage:
                            prefix = $"{StationPrefix}{footString}{StationSuffix}";
                            break;
                        default:
                            prefix = "";
                            break;
                    }
                    var layout = new CanvasTextLayout(session, prefix + comment, format, (float)(drawArea.Height * PercentOfGraph) - TopEdgePadding - LineBuffer, 0);
                    Rect textBounds = layout.DrawBounds;
                    var height = textBounds.Height;
                    var width = layout.LayoutBounds.Width;
                    var textMiddle = (float)(textBounds.Top + (textBounds.Height / 2));
                    var feetToPixels = drawArea.Width / page.Width;
                    var footageInPixels = (footage - page.StartFootage) * feetToPixels + drawArea.X;
                    var finalFootageInPixels = (float)(drawArea.Width - footageInPixels);
                    var distanceFromMiddleToFootage = footageInPixels + textMiddle;
                    var textTranslate = drawArea.Width - distanceFromMiddleToFootage;
                    textBounds.Y += textTranslate;
                    textBounds.X += IsFlippedVertical ? (float)(drawArea.Bottom - TopEdgePadding - width) : (float)(TopEdgePadding + drawArea.Top);
                    var curBounds = new CommentBoundsInfo
                    {
                        Layout = layout,
                        Bounds = textBounds,
                        FootageInPixels = finalFootageInPixels
                    };
                    if (first == null)
                    {
                        first = last = curBounds;
                    }
                    else
                    {
                        last.Next = curBounds;
                        curBounds.Prev = last;
                        last = curBounds;
                    }
                }
            }
            if (first == null)
                return (null, null, null);
            first.CheckBounds(drawArea, false, CommentBuffer, EdgeBuffer);
            last.CheckBounds(drawArea, true, CommentBuffer, EdgeBuffer);

            while (first != null)
            {
                var newBounds = first.Bounds;
                var oldBounds = first.Layout.DrawBounds;
                translation = Matrix3x2.CreateTranslation((float)(newBounds.X - oldBounds.X), (float)(newBounds.Y - oldBounds.Y));
                if (commentOutputGeo == null)
                {
                    //TODO: Probably need to dispose of this stuff.
                    commentOutputGeo = CanvasGeometry.CreateText(first.Layout).Transform(translation);
                }
                else
                {

                    using (var geometry = CanvasGeometry.CreateText(first.Layout).Transform(translation))
                    {
                        commentOutputGeo = commentOutputGeo.CombineWith(geometry, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                    }
                }

                using (var path = new CanvasPathBuilder(session))
                {
                    var yStart = (float)(IsFlippedVertical ? drawArea.Bottom : drawArea.Top);
                    var linePadding = TopEdgePadding - LineBuffer;
                    var lineTopEdgePadding = IsFlippedVertical ? (float)drawArea.Bottom - linePadding : linePadding + (float)drawArea.Top;
                    var lineTopEdgePaddingWithExtra = lineTopEdgePadding + (IsFlippedVertical ? -10f : 10f);
                    var lineFull = IsFlippedVertical ? ((float)(lineTopEdgePadding - newBounds.Width - (LineBuffer * 2))) : ((float)(lineTopEdgePadding + newBounds.Width + (LineBuffer * 2)));
                    var lineLengthFromMiddle = (float)first.Bounds.Height / 2f + 1.5f;

                    switch (BorderType)
                    {
                        case BorderType.Line:
                            path.BeginFigure(yStart, first.FootageInPixels);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle);
                            path.EndFigure(CanvasFigureLoop.Open);
                            break;
                        case BorderType.Pegs:
                            path.BeginFigure(yStart, first.FootageInPixels);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePaddingWithExtra, first.CommentMiddle + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePaddingWithExtra, first.CommentMiddle - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle);
                            path.EndFigure(CanvasFigureLoop.Open);
                            break;
                        case BorderType.Full:
                            path.BeginFigure(yStart, first.FootageInPixels);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle + lineLengthFromMiddle);
                            path.AddLine(lineFull, first.CommentMiddle + lineLengthFromMiddle);
                            path.AddLine(lineFull, first.CommentMiddle - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, first.CommentMiddle);
                            path.EndFigure(CanvasFigureLoop.Open);
                            break;
                        default:
                            break;
                    }
                    var backdropRect = new Rect(lineTopEdgePadding, first.CommentMiddle - lineLengthFromMiddle, newBounds.Width + (LineBuffer * 2), lineLengthFromMiddle * 2);

                    if (lineOutputGeo == null)
                    {
                        lineOutputGeo = CanvasGeometry.CreatePath(path);
                        backdropOutputGeo = CanvasGeometry.CreateRectangle(session, backdropRect);
                    }
                    else
                    {
                        using (var pathGeo = CanvasGeometry.CreatePath(path))
                            lineOutputGeo = lineOutputGeo.CombineWith(pathGeo, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                        using (var backdrop = CanvasGeometry.CreateRectangle(session, backdropRect))
                            backdropOutputGeo = backdropOutputGeo.CombineWith(backdrop, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                    }
                }

                var old = first;
                first = first.Next;
                old.Dispose();
            }

            var rotationPoint = new Vector2(0, (float)drawArea.Width);
            var rotation = Matrix3x2.CreateRotation((float)(90 * Math.PI / 180), rotationPoint);
            translation = Matrix3x2.CreateTranslation(0, 0 - (float)drawArea.Width);
            commentOutputGeo = commentOutputGeo.Transform(rotation).Transform(translation);
            backdropOutputGeo = backdropOutputGeo.Transform(rotation).Transform(translation);
            lineOutputGeo = lineOutputGeo.Transform(rotation).Transform(translation);
            var commentOutput = new GeometryInfo
            {
                Color = TextColor,
                Geometry = commentOutputGeo
            };

            var lineOutput = new GeometryInfo
            {
                Color = LineColor,
                Geometry = lineOutputGeo
            };
            return (commentOutput, lineOutput, backdropOutputGeo);
        }

        private class CommentBoundsInfo : IDisposable
        {
            public CanvasTextLayout Layout { get; set; }
            public Rect Bounds { get; set; }
            public CommentBoundsInfo Next { get; set; }
            public CommentBoundsInfo Prev { get; set; }
            public float FootageInPixels { get; set; }
            public float CommentMiddle => (float)(Bounds.Y + (Bounds.Height / 2));

            public void CheckBounds(Rect drawArea, bool isReverse, double buffer, double edgeBuffer)
            {
                if (isReverse)
                {
                    FixAboveDrawArea(drawArea, edgeBuffer);
                    if (Prev != null)
                    {
                        FixPrevious(buffer);
                        Prev.CheckBounds(drawArea, isReverse, buffer, edgeBuffer);
                    }
                }
                else
                {
                    FixBelowDrawArea(drawArea, edgeBuffer);
                    if (Next != null)
                    {
                        FixNext(buffer);
                        Next.CheckBounds(drawArea, isReverse, buffer, edgeBuffer);
                    }
                }
            }

            public bool IsValid(Rect drawArea, double buffer, double edgeBuffer)
            {
                if (Bounds.Top - edgeBuffer < 0 - drawArea.X)
                    return false;
                if (Bounds.Bottom + edgeBuffer > drawArea.Width - drawArea.X)
                    return false;
                return true;
            }

            public bool FixAboveDrawArea(Rect drawArea, double edgeBuffer)
            {
                var min = 0 - drawArea.X;
                var topWithBuffer = Bounds.Top - edgeBuffer;
                if (topWithBuffer < min)
                {
                    var newY = Math.Ceiling(Bounds.Y + min - topWithBuffer);
                    Bounds = new Rect(new Point(Bounds.X, newY), new Size(Bounds.Width, Bounds.Height));
                    return true;
                }
                return false;
            }

            public bool FixBelowDrawArea(Rect drawArea, double edgeBuffer)
            {
                var max = drawArea.Width - drawArea.X;
                var bottomWithBuffer = Bounds.Bottom + edgeBuffer;
                if (bottomWithBuffer > max)
                {
                    var newY = Math.Floor(Bounds.Y + max - bottomWithBuffer);
                    Bounds = new Rect(new Point(Bounds.X, newY), new Size(Bounds.Width, Bounds.Height));
                    return true;
                }
                return false;
            }

            public bool FixNext(double buffer)
            {
                var difference = Bounds.Top - buffer - Next.Bounds.Bottom;
                if (difference < 0)
                {
                    var newY = Math.Floor(Next.Bounds.Y + difference);
                    Next.Bounds = new Rect(new Point(Next.Bounds.X, newY), new Size(Next.Bounds.Width, Next.Bounds.Height));
                    return true;
                }
                return false;
            }

            public bool FixPrevious(double buffer)
            {
                var difference = Bounds.Bottom + buffer - Prev.Bounds.Top;
                if (difference > 0)
                {
                    var newY = Math.Ceiling(Prev.Bounds.Y + difference);
                    Prev.Bounds = new Rect(new Point(Prev.Bounds.X, newY), new Size(Prev.Bounds.Width, Prev.Bounds.Height));
                    return true;
                }
                return false;
            }

            public void Dispose()
            {
                Layout.Dispose();
                Next = null;
                Prev = null;
            }
        }
    }

    public enum BorderType
    {
        Line, Pegs, Full
    }

    public enum StationStyle
    {
        PlusFoot, None, Footage
    }
    /*
    private void temp()
    {
        using (var path = new CanvasPathBuilder(session))
        {
            var height = textBounds.Height;
            var textMiddle = new Vector2((float)textBounds.X, (float)(textBounds.Top + (textBounds.Height / 2)));
            var feetToPixels = drawArea.Width / page.Width;

            var footageInPixels = footage * feetToPixels + drawArea.X;
            var distanceFromMiddleToFootage = footageInPixels + textMiddle.Y;

            var textTranslate = drawArea.Width - distanceFromMiddleToFootage;
            //TODO: Make extention methods that help with float / double things.
            translation = Matrix3x2.CreateTranslation((float)(TopEdgePadding + drawArea.Y), (float)textTranslate);

            //TODO: Add getter and setter for line settings.
            var lineTopEdgePadding = (float)TopEdgePadding - 2f + (float)drawArea.Y;
            var lineTopEdgePaddingWithExtra = lineTopEdgePadding + 10f;
            var finalFootageInPixels = (float)(drawArea.Width - footageInPixels);
            var lineLengthFromMiddle = (float)height / 1.5f;// * 1.5f;

            //TODO: Begin figure float double
            path.BeginFigure((float)drawArea.Y, finalFootageInPixels);
            path.AddLine(lineTopEdgePadding, finalFootageInPixels);
            path.AddLine(lineTopEdgePadding, finalFootageInPixels + lineLengthFromMiddle);
            //path.AddLine(lineTopEdgePaddingWithExtra, finalFootageInPixels + lineLengthFromMiddle);
            //path.AddLine(lineTopEdgePadding, finalFootageInPixels + lineLengthFromMiddle);
            path.AddLine(lineTopEdgePadding, finalFootageInPixels - lineLengthFromMiddle);
            //path.AddLine(lineTopEdgePaddingWithExtra, finalFootageInPixels - lineLengthFromMiddle);
            //path.AddLine(lineTopEdgePadding, finalFootageInPixels - lineLengthFromMiddle);
            path.AddLine(lineTopEdgePadding, finalFootageInPixels);
            path.EndFigure(CanvasFigureLoop.Open);

            if (commentOutputGeo == null)
            {
                //TODO: Probably need to dispose of this stuff.
                commentOutputGeo = CanvasGeometry.CreateText(layout).Transform(translation);
            }
            else
            {

                using (var geometry = CanvasGeometry.CreateText(layout))
                {
                    commentOutputGeo = commentOutputGeo.CombineWith(geometry, translation, CanvasGeometryCombine.Union);
                }
            }
            if (lineOutputGeo == null)
            {
                lineOutputGeo = CanvasGeometry.CreatePath(path);
            }
            else
            {
                using (var pathGeo = CanvasGeometry.CreatePath(path))
                {
                    lineOutputGeo = lineOutputGeo.CombineWith(pathGeo, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                }
            }
        }
    }*/
}
