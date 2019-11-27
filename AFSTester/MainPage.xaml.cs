using AccurateFileSystem;
using AccurateReportSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Data.Xml.Dom;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using File = AccurateFileSystem.File;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AFSTester
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        List<File> NewFiles;
        Dictionary<AllegroCISFile, MapLayer> Layers = new Dictionary<AllegroCISFile, MapLayer>();
        private Random Random = new Random();
        TreeViewNode HiddenNode = new TreeViewNode() { Content = "Hidden Files" };
        List<(string Name, string Route, double Length, BasicGeoposition Start, BasicGeoposition End)> ShortList;

        public MainPage()
        {
            this.InitializeComponent();
            FileTreeView.RootNodes.Add(HiddenNode);
            var xml = new XmlDocument();
            var xmlStringTask = Clipboard.GetContent().GetTextAsync().AsTask();
            xmlStringTask.Wait();
            var xmlString = xmlStringTask.Result;
            ShortList = null;
            try { xml.LoadXml(xmlString); } catch { return; }
            
            var curNode = xml.ChildNodes.First(n => n.NodeName == "kml");
            curNode = curNode.ChildNodes.First(n => n.NodeName == "Document");
            curNode = curNode.ChildNodes.First(n => n.NodeName == "Folder");
            var lateralList = new List<(string Name, string Route, double Length, BasicGeoposition Start, BasicGeoposition End)>();
            foreach (var node in curNode.ChildNodes)
            {
                if (node.NodeName == "Placemark")
                {
                    var name = node.ChildNodes.First(n => n.NodeName == "name").InnerText;
                    var description = node.ChildNodes.First(n => n.NodeName == "description").InnerText.Split('\n');
                    var length = double.NaN;
                    var isLat = false;
                    string route = null;
                    for (int i = 0; i < description.Length; ++i)
                    {
                        if (description[i] == "<td>Shape_Leng</td>")
                        {
                            var lenString = description[i + 2];
                            lenString = lenString.Substring(4, lenString.Length - 9);
                            length = double.Parse(lenString);
                        }
                        else if (description[i] == "<td>Main_Later</td>")
                        {
                            if (description[i + 2] == "<td>Lateral</td>")
                                isLat = true;
                            else if(description[i + 2] != "<td>Main</td>")
                                route = description[i + 2].Substring(4, description[i + 2].Length - 9);
                        }
                        else if (description[i] == "<td>SourceRout</td>")
                        {
                            if (description[i + 2] == "<td>Lateral</td>")
                                isLat = true;
                            else if (description[i + 2] != "<td>Main</td>")
                                route = description[i + 2].Substring(4, description[i + 2].Length - 9);
                        }
                    }
                    var miltiGeo = node.ChildNodes.First(n => n.NodeName == "MultiGeometry");
                    var lineString = miltiGeo.ChildNodes.First(n => n.NodeName == "LineString");
                    var coordsNode = lineString.ChildNodes.First(n => n.NodeName == "coordinates");
                    var coords = coordsNode.InnerText.Trim().Split(' ');
                    var start = GetGeoposition(coords[0].Split(','));
                    var end = GetGeoposition(coords[coords.Length - 1].Split(','));
                    if (isLat)
                    {
                        lateralList.Add((name, route, length, start, end));
                    }
                }
            }

            ShortList = lateralList.Where(info => info.Length < 100).ToList();
            var shortListStrings = ShortList.Select(info => $"{info.Name}\t{info.Route}\t{info.Length}\t{info.Start.Latitude}\t{info.Start.Longitude}\t{info.End.Latitude}\t{info.End.Longitude}");
            var shortString = string.Join('\n', shortListStrings);
            var longList = lateralList.Where(info => info.Length >= 100).ToList();
            var longListStrings = longList.Select(info => $"{info.Name}\t{info.Route}\t{info.Length}\t{info.Start.Latitude}\t{info.Start.Longitude}\t{info.End.Latitude}\t{info.End.Longitude}");
            var longString = string.Join('\n', longListStrings);
        }

        private BasicGeoposition GetGeoposition(string[] list)
        {
            var lon = double.Parse(list[0]);
            var lat = double.Parse(list[1]);
            return new BasicGeoposition()
            {
                Latitude = lat,
                Longitude = lon
            };
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var makeGraphs = false;
            NewFiles = new List<File>();

            foreach (var file in files)
            {
                var factory = new FileFactory(file);
                var newFile = await factory.GetFile();
                if (newFile != null)
                    NewFiles.Add(newFile);
            }
            NewFiles.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));
            for (int i = 0; i < NewFiles.Count; ++i)
            {
                var curFile = NewFiles[i];
                for (int j = i + 1; j < NewFiles.Count; ++j)
                {
                    var nextFile = NewFiles[j];
                    if (curFile.Name != nextFile.Name)
                        break;
                    if (curFile.IsEquivalent(nextFile))
                    {
                        NewFiles.RemoveAt(j);
                        --j;
                    }
                }
            }
            if (ShortList != null)
                DoStuff();
            /*
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".svy");
            var file = await picker.PickSingleFileAsync();
            var factory = new FileFactory(file);
            var newFile = await factory.GetFile();
            */
            FillTreeView();
        }

        private void DoStuff()
        {
            var shortListDistance = new List<(double Dist, AllegroDataPoint Point)>();
            for (int i = 0; i < NewFiles.Count; ++i)
            {
                var file = NewFiles[i];
                if (file is AllegroCISFile allegroFile)
                {
                    for (int j = 0; j < ShortList.Count; ++j)
                    {
                        var (_, _, _, start, end) = ShortList[j];
                        var (newShort, newPoint) = allegroFile.GetClosestPoint(start, end);
                        if (shortListDistance.Count == j)
                        {
                            shortListDistance.Add((newShort, newPoint));
                            continue;
                        }
                        var (curShort, _) = shortListDistance[j];
                        if (newShort < curShort)
                            shortListDistance[j] = (newShort, newPoint);
                    }
                }
            }
            var shortCloseStringList = shortListDistance.Select(info => $"{info.Dist}\t{RoundDist(info.Dist)}\t{info.Point.On}\t{info.Point.Off}\t{info.Point.GPS.Latitude}\t{info.Point.GPS.Longitude}\t{info.Point.OriginalComment}");
            var shortCloseString = string.Join('\n', shortCloseStringList);
        }

        private double RoundDist(double dist)
        {
            int times = (int)(dist / 10);
            var floor = times * 10;
            var ceil = (times + 1) * 10;
            var floorDiff = dist - floor;
            var ceilDiff = ceil - dist;
            return floorDiff < ceilDiff ? floor : ceil;
        }

        private async void MakeGraphs(CombinedAllegroCISFile allegroFile)
        {
            var report = new GraphicalReport();
            var commentGraph = new Graph(report);
            var graph1 = new Graph(report);
            var graph2 = new Graph(report);
            var graph3 = new Graph(report);
            var on = new GraphSeries("On", allegroFile.GetDoubleData("On"))
            {
                LineColor = Colors.Blue
            };
            var off = new GraphSeries("Off", allegroFile.GetDoubleData("Off"))
            {
                LineColor = Colors.Green
            };
            var onMir = new GraphSeries("On MIR Compensated", allegroFile.GetDoubleData("On Compensated"))
            {
                LineColor = Colors.Purple
            };
            var offMir = new GraphSeries("Off MIR Compensated", allegroFile.GetDoubleData("Off Compensated"))
            {
                LineColor = Color.FromArgb(255, 57, 255, 20)
            };
            var depth = new GraphSeries("Depth", allegroFile.GetDoubleData("Depth"))
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = false,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point
            };
            var redLine = new SingleValueGraphSeries("850 Line", -0.85)
            {
                IsDrawnInLegend = false
            };
            var commentSeries = new CommentSeries { Values = allegroFile.GetCommentData("Comment"), PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs };
            var seperateComment = false;
            if (seperateComment)
            {
                commentSeries.PercentOfGraph = 1f;
                commentSeries.IsFlippedVertical = true;
                commentSeries.BorderType = BorderType.Full;
            }

            commentGraph.CommentSeries = commentSeries;
            commentGraph.LegendInfo.Name = "CIS Comments";
            commentGraph.DrawTopBorder = false;

            commentGraph.XAxisInfo.MajorGridline.IsEnabled = false;
            commentGraph.YAxesInfo.MinorGridlines.IsEnabled = false;
            commentGraph.YAxesInfo.MajorGridlines.IsEnabled = false;
            commentGraph.YAxesInfo.Y1IsDrawn = false;

            graph1.Series.Add(depth);
            graph1.YAxesInfo.Y2IsDrawn = true;
            if (!seperateComment)
                graph1.CommentSeries = commentSeries;
            /*
            graph1.YAxesInfo.Y1MaximumValue = 150;
            graph1.YAxesInfo.Y1MinimumValue = 0;
            graph1.YAxesInfo.Y1IsInverted = false;
            graph1.Gridlines[(int)GridlineName.MajorHorizontal].Offset = 15;
            graph1.Gridlines[(int)GridlineName.MinorHorizontal].Offset = 5;
            */
            graph1.Series.Add(on);

            graph1.Series.Add(off);
            graph1.Series.Add(onMir);
            graph1.Series.Add(offMir);
            graph1.Series.Add(redLine);
            //graph1.XAxisInfo.IsEnabled = false;
            graph1.DrawBottomBorder = true;


            graph2.Series.Add(depth);
            graph2.YAxesInfo.Y2IsDrawn = true;
            graph2.YAxesInfo.Y1IsDrawn = false;
            //graph2.XAxisInfo.IsEnabled = false;

            graph3.Series.Add(on);
            graph3.Series.Add(off);
            //graph3.XAxisInfo.IsEnabled = false;
            graph3.DrawBottomBorder = false;

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                //Title = "PG&E LS 3002-01 MP 0 to MP 5.8688"
                //Title = "PG&E LS 3008-01 MP 6.58 to MP 8.01"
                //Title = "PG&E LS 191-1 MP 16.79 to MP 30.1000"
                //Title = "PG&E LS 191A MP 0 to MP 4.83"
                Title = "PG&E LS 3001-01 MP 3.1300 to MP 4.1671"
                //Title = "PG&E LS 3017-01 MP 0.4300 to MP 7.5160"
                //Title = "PG&E LS L131 MP 26.1018 to MP 27.0150"
                //Title = "PG&E DREG11309 MP 0.0000 to MP 0.0100"
                //Title = "PG&E DREG21620 MP 0.0000 to MP 0.0310"
                //Title = "PG&E DREG14570 MP 0.0000 to MP 0.1000"
                //Title = "PG&E DREG5332 MP 0.0000 to MP 0.0200"
                //Title = "PG&E DREG5397 MP 0.0000 to MP 0.0300"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            //var graph1Measurement = new SplitContainerMeasurement(graph1)
            //{
            //    RequestedPercent = 0.5
            //};
            var chart1 = new Chart(report, "Survey Direction");
            var chart2 = new Chart(report, "850 Data");
            var mirSeries = new MirDirection(allegroFile.GetReconnects());
            var exceptions = new ExceptionsChartSeries(allegroFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo);
            chart2.Series.Add(exceptions);
            //chart1.LegendInfo.NameFontSize = 18f;

            var chart1Series = new SurveyDirectionSeries(allegroFile.GetDirectionData());
            chart1.Series.Add(chart1Series);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            if (seperateComment)
            {
                var commentGraphMeasurement = new SplitContainerMeasurement(commentGraph)
                {
                    FixedInchSize = 1f
                };
                splitContainer.AddContainer(commentGraphMeasurement);
            }
            splitContainer.AddContainer(graph1);
            splitContainer.AddSelfSizedContainer(chart2);
            splitContainer.AddSelfSizedContainer(chart1);
            //splitContainer.AddContainer(graph2);
            //splitContainer.AddContainer(graph3);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(0, allegroFile.Points.Last().Footage);
            var tabular = allegroFile.GetTabularData();
            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var image = report.GetImage(page, 300);
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"Test Page {pageString}" + ".png", CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                }
            }
        }

        private void FillTreeView()
        {
            var treeNodes = new Dictionary<string, TreeViewNode>();
            foreach (var file in NewFiles)
            {
                if (file is AllegroCISFile allegroFile && allegroFile.Header.ContainsKey("segment"))
                {

                    var segmentName = Regex.Replace(Regex.Replace(allegroFile.Header["segment"], "\\s+", ""), "(?i)ls", "").Replace('.', '-');
                    if (!treeNodes.ContainsKey(segmentName))
                    {
                        var newSegmentNode = new TreeViewNode() { Content = segmentName };
                        treeNodes.Add(segmentName, newSegmentNode);
                    }
                    var segmentNode = treeNodes[segmentName];
                    segmentNode.Children.Add(new TreeViewNode() { Content = allegroFile });
                }
            }
            foreach (var treeNode in treeNodes.Values)
                FileTreeView.RootNodes.Add(treeNode);
        }

        private void FileTreeView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var files = FileTreeView.SelectedNodes.Where(node => node.Content is AllegroCISFile).ToList();
            var segments = FileTreeView.SelectedNodes.Where(node => node.Content is string).ToList();
            if (segments.Count > 1)
            {
                var toSegment = segments[0];
                for (int i = 1; i < segments.Count; ++i)
                {
                    var fromSegment = segments[i];
                    while (fromSegment.HasChildren)
                    {
                        var child = fromSegment.Children[0];
                        fromSegment.Children.RemoveAt(0);
                        toSegment.Children.Add(child);
                    }
                    FileTreeView.RootNodes.Remove(fromSegment);
                }
            }
            else if (files.Count > 0 && segments.Count == 1)
            {
                var toSegment = segments[0];
                for (int i = 1; i < files.Count; ++i)
                {
                    var file = files[i];
                    file.Parent.Children.Remove(file);
                    toSegment.Children.Add(file);
                }
            }
            FileTreeView.SelectedNodes.Clear();
        }

        private void HideFileOnMap(AllegroCISFile file)
        {
            if (Layers.ContainsKey(file))
                Layers[file].Visible = false;
        }

        private void ToggleFileOnMap(AllegroCISFile file)
        {
            if (file.Points.Count < 2)
                return;
            if (!Layers.ContainsKey(file))
            {
                Color color = Color.FromArgb(255, (byte)Random.Next(256), (byte)Random.Next(256), (byte)Random.Next(256));
                var elements = new List<MapElement>();
                var maxFootDiff = 10;

                for (int i = 0; i < file.Points.Count; ++i)
                {
                    var point = file.Points[i];
                    if (point.HasGPS)
                    {
                        var startLocation = new BasicGeoposition()
                        {
                            Latitude = point.GPS.Latitude,
                            Longitude = point.GPS.Longitude
                        };
                        Geopoint startPoint = new Geopoint(startLocation);
                        var startIcon = new MapIcon
                        {
                            Location = startPoint,
                            NormalizedAnchorPoint = new Point(0.5, 1.0),
                            ZIndex = 0,
                            Title = $"Start '{file.Name}'"
                        };
                        elements.Add(startIcon);
                        break;
                    }
                }

                for (int i = file.Points.Count - 1; i >= 0; --i)
                {
                    var point = file.Points[i];
                    if (point.HasGPS)
                    {
                        var endLocation = new BasicGeoposition()
                        {
                            Latitude = point.GPS.Latitude,
                            Longitude = point.GPS.Longitude
                        };
                        Geopoint endPoint = new Geopoint(endLocation);
                        var endIcon = new MapIcon
                        {
                            Location = endPoint,
                            NormalizedAnchorPoint = new Point(0.5, 1.0),
                            ZIndex = 0,
                            Title = $"End '{file.Name}'"
                        };
                        elements.Add(endIcon);
                        break;
                    }
                }

                double? lastFoot = null;
                var positions = new List<BasicGeoposition>();
                MapPolyline mapPolyline;

                for (int i = 0; i < file.Points.Count; ++i)
                {
                    var point = file.Points[i];
                    var curFoot = point.Footage;
                    if (!point.HasGPS)
                    {
                        lastFoot = curFoot;
                        continue;
                    }
                    var location = new BasicGeoposition()
                    {
                        Latitude = point.GPS.Latitude,
                        Longitude = point.GPS.Longitude
                    };
                    if (curFoot - (lastFoot ?? curFoot) > maxFootDiff)
                    {
                        mapPolyline = new MapPolyline
                        {
                            Path = new Geopath(positions),
                            StrokeColor = Colors.Black,
                            StrokeThickness = 4,
                            StrokeDashed = false,
                            ZIndex = 0
                        };
                        elements.Add(mapPolyline);
                        mapPolyline = new MapPolyline
                        {
                            Path = new Geopath(positions),
                            StrokeColor = color,
                            StrokeThickness = 3,
                            StrokeDashed = false,
                            ZIndex = 1
                        };
                        elements.Add(mapPolyline);
                        positions = new List<BasicGeoposition>
                        {
                            location
                        };
                    }
                    else
                    {
                        positions.Add(location);
                    }
                    lastFoot = curFoot;
                }

                mapPolyline = new MapPolyline
                {
                    Path = new Geopath(positions),
                    StrokeColor = Colors.Black,
                    StrokeThickness = 4,
                    StrokeDashed = false,
                    ZIndex = 0
                };
                elements.Add(mapPolyline);
                mapPolyline = new MapPolyline
                {
                    Path = new Geopath(positions),
                    StrokeColor = color,
                    StrokeThickness = 3,
                    StrokeDashed = false,
                    ZIndex = 1
                };
                elements.Add(mapPolyline);

                var newLayer = new MapElementsLayer()
                {
                    MapElements = elements
                };
                Layers.Add(file, newLayer);
                MapControl.Layers.Add(newLayer);
                return;
            }
            var layer = Layers[file];
            layer.Visible = !layer.Visible;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var files = FileTreeView.SelectedNodes.Where(node => node.Content is AllegroCISFile).ToList();
            if (files.Count > 0)
                foreach (var file in files)
                    ToggleFileOnMap(file.Content as AllegroCISFile);
            else
            {
                foreach (var folderNode in FileTreeView.RootNodes)
                {
                    if (folderNode == HiddenNode)
                        continue;
                    foreach (var fileNode in folderNode.Children)
                    {
                        if (fileNode.Content is AllegroCISFile file)
                            ToggleFileOnMap(file);
                    }
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            foreach (var layer in Layers.Values)
                layer.Visible = false;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            GeoboundingBox area = null;
            foreach (var (file, layer) in Layers)
            {
                if (layer.Visible)
                {
                    var rect = file.GetGpsArea();
                    area = area?.CombineAreas(rect) ?? rect;
                }
            }
            if (area != null)
            {
                _ = MapControl.TrySetViewBoundsAsync(area, new Thickness(10), MapAnimationKind.None);
            }
        }

        private void CombineButtonClick(object sender, RoutedEventArgs e)
        {
            var fileNodes = FileTreeView.SelectedNodes.Where(node => node.Content is AllegroCISFile).ToList();
            var files = new List<AllegroCISFile>();
            var fileNames = new HashSet<string>();
            foreach (var node in fileNodes)
            {
                var file = node.Content as AllegroCISFile;
                if (!fileNames.Contains(file.Name))
                {
                    files.Add(file);
                    fileNames.Add(file.Name);
                }
                else
                {
                    if (file.Extension == ".csv")
                    {
                        for (int i = 0; i < files.Count; ++i)
                        {
                            if (files[i].Name == file.Name)
                            {
                                files.RemoveAt(i);
                                files.Add(file);
                                break;
                            }
                        }
                    }
                }
            }

            var test = CombinedAllegroCISFile.CombineFiles("Testing", files);
            MakeGraphs(test);
        }

        private void HideButtonClick(object sender, RoutedEventArgs e)
        {
            var fileNodes = FileTreeView.SelectedNodes.Where(node => node.Content is AllegroCISFile).ToList();
            for (int i = 0; i < fileNodes.Count; ++i)
            {
                var file = fileNodes[i];
                file.Parent.Children.Remove(file);
                HiddenNode.Children.Add(file);
                HideFileOnMap(file.Content as AllegroCISFile);
            }
            FileTreeView.SelectedNodes.Clear();
        }
    }
}
