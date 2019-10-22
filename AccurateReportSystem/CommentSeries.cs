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
        public double TopEdgePadding { get; set; } = 20; //TODO: Make this inches.
        public Color LineColor { get; set; } = Colors.Black;
        public Color TextColor { get; set; } = Colors.Black;


        public (GeometryInfo, GeometryInfo) GetGeometry(PageInformation page, Rect drawArea, CanvasDrawingSession session)
        {
            CanvasGeometry commentOutputGeo = null;// = CanvasGeometry.CreateRectangle(device, 0, 0, (float)drawArea.Height, (float)drawArea.Width);
            CanvasGeometry lineOutputGeo = null;
            Matrix3x2 translation;
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
                    using (var layout = new CanvasTextLayout(session, comment, format, (float)drawArea.Height / 2, 0))
                    {
                        var textBounds = layout.DrawBounds;

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
                    }
                }
            }
            var rotationPoint = new Vector2(0, (float)drawArea.Width);
            var rotation = Matrix3x2.CreateRotation((float)(90 * Math.PI / 180), rotationPoint);
            translation = Matrix3x2.CreateTranslation(0, 0 - (float)drawArea.Width);
            commentOutputGeo = commentOutputGeo.Transform(rotation).Transform(translation);
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
            return (commentOutput, lineOutput);
        }
    }
}
