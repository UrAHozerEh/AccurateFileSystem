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
        public double TopEdgePadding { get; set; } = 10;

        public GeometryInfo GetGeometry(PageInformation page, Rect drawArea)
        {
            var device = CanvasDevice.GetSharedDevice();
            CanvasGeometry outputGeo = null;// = CanvasGeometry.CreateRectangle(device, 0, 0, (float)drawArea.Height, (float)drawArea.Width);
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
                    using (var layout = new CanvasTextLayout(device, comment, format, (float)drawArea.Height / 2, 0))
                    {
                        var textBounds = layout.DrawBounds;

                        using (var path = new CanvasPathBuilder(device))
                        {
                            var height = textBounds.Height;
                            var textMiddle = new Vector2((float)textBounds.X, (float)(textBounds.Top + (textBounds.Height / 2)));
                            var feetToPixels = drawArea.Width / page.Width;

                            var footageInPixels = footage * feetToPixels;
                            var distanceFromMiddleToFootage = footageInPixels + textMiddle.Y;

                            var textTranslate = drawArea.Width - distanceFromMiddleToFootage;
                            //TODO: Make extention methods that help with float / double things.
                            translation = Matrix3x2.CreateTranslation((float)TopEdgePadding, (float)textTranslate);

                            var lineTopEdgePadding = (float)TopEdgePadding * 0.75f;
                            var finalFootageInPixels = (float)(drawArea.Width - footageInPixels);
                            var lineLengthFromMiddle = (float)height / 2f * 1.5f;

                            path.BeginFigure(0, finalFootageInPixels);
                            path.AddLine(lineTopEdgePadding, finalFootageInPixels);
                            path.AddLine(lineTopEdgePadding, finalFootageInPixels + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding * 1.5f, finalFootageInPixels + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, finalFootageInPixels + lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, finalFootageInPixels - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding * 1.5f, finalFootageInPixels - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, finalFootageInPixels - lineLengthFromMiddle);
                            path.AddLine(lineTopEdgePadding, finalFootageInPixels);
                            path.EndFigure(CanvasFigureLoop.Open);

                            if (outputGeo == null)
                            {
                                outputGeo = CanvasGeometry.CreateText(layout).Transform(translation);
                            }
                            else
                            {
                                using (var geometry = CanvasGeometry.CreateText(layout))
                                    outputGeo = outputGeo.CombineWith(geometry, translation, CanvasGeometryCombine.Union);
                            }

                            using (var pathGeo = CanvasGeometry.CreatePath(path))
                            {
                                outputGeo = outputGeo.CombineWith(pathGeo, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                            }
                        }
                    }
                }
            }
            var rotationPoint = new Vector2(0, (float)drawArea.Width);
            var rotation = Matrix3x2.CreateRotation((float)(90 * Math.PI / 180), rotationPoint);
            translation = Matrix3x2.CreateTranslation(0, 0 - (float)drawArea.Width);
            outputGeo = outputGeo.Transform(rotation).Transform(translation);
            var output = new GeometryInfo
            {
                Color = Colors.HotPink,
                Geometry = outputGeo
            };
            return output;
        }
    }
}
