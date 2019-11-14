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
using Windows.Data.Pdf;
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

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var makeGraphs = false;
            NewFiles = new List<File>();

            foreach (var file in files)
            {
                var factory = new FileFactory(file);
                var newFile = await factory.GetFile();
                if (newFile is AllegroCISFile && makeGraphs)
                {
                    var allegroFile = newFile as AllegroCISFile;
                    var report = new GraphicalReport();
                    var commentGraph = new Graph(report);
                    var graph1 = new Graph(report);
                    var graph2 = new Graph(report);
                    var graph3 = new Graph(report);
                    var on = new GraphSeries("On", allegroFile.GetDoubleData("On"))
                    {
                        LineColor = Colors.Green
                    };
                    var off = new GraphSeries("Off", allegroFile.GetDoubleData("Off"))
                    {
                        LineColor = Colors.Blue
                    };
                    var depth = new GraphSeries("Depth", allegroFile.GetDoubleData("Depth"))
                    {
                        LineColor = Colors.Orange,
                        PointColor = Colors.Orange,
                        IsY1Axis = false,
                        PointShape = GraphSeries.Shape.Circle,
                        GraphType = GraphSeries.Type.Point
                    };
                    var redLine = new SingleValueGraphSeries("850 Line", -0.85);
                    var commentSeries = new CommentSeries { Values = allegroFile.GetStringData("Comment"), PercentOfGraph = 1f, IsFlippedVertical = true };

                    commentGraph.CommentSeries = commentSeries;
                    commentGraph.LegendInfo.Name = "CIS Comments";
                    commentGraph.DrawTopBorder = false;

                    commentGraph.XAxisInfo.MajorGridline.IsEnabled = false;
                    commentGraph.YAxesInfo.MinorGridlines.IsEnabled = false;
                    commentGraph.YAxesInfo.MajorGridlines.IsEnabled = false;
                    commentGraph.YAxesInfo.Y1IsDrawn = false;

                    graph1.Series.Add(depth);
                    graph1.YAxesInfo.Y2IsDrawn = true;
                    /*
                    graph1.YAxesInfo.Y1MaximumValue = 150;
                    graph1.YAxesInfo.Y1MinimumValue = 0;
                    graph1.YAxesInfo.Y1IsInverted = false;
                    graph1.Gridlines[(int)GridlineName.MajorHorizontal].Offset = 15;
                    graph1.Gridlines[(int)GridlineName.MinorHorizontal].Offset = 5;
                    */
                    graph1.Series.Add(on);

                    graph1.Series.Add(off);
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

                    var bottomGlobalXAxis = new GlobalXAxis(report);

                    var topGlobalXAxis = new GlobalXAxis(report, true);

                    var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);
                    var commentGraphMeasurement = new SplitContainerMeasurement(commentGraph)
                    {
                        FixedInchSize = 1f
                    };
                    //var graph1Measurement = new SplitContainerMeasurement(graph1)
                    //{
                    //    RequestedPercent = 0.5
                    //};
                    var chart1 = new Chart(report, "Survey Direction");
                    var chart2 = new Chart(report, "MIR Info");
                    var mirSeries = new MirDirection(allegroFile.GetReconnects());
                    chart2.Series.Add(mirSeries);
                    //chart1.LegendInfo.NameFontSize = 18f;

                    var chart1Series = new SurveyDirectionSeries(allegroFile.GetDirectionData());
                    chart1.Series.Add(chart1Series);

                    splitContainer.AddSelfSizedContainer(topGlobalXAxis);
                    splitContainer.AddContainer(commentGraphMeasurement);
                    splitContainer.AddContainer(graph1);
                    splitContainer.AddSelfSizedContainer(chart2);
                    splitContainer.AddSelfSizedContainer(chart1);
                    //splitContainer.AddContainer(graph2);
                    //splitContainer.AddContainer(graph3);
                    splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
                    report.Container = splitContainer;
                    var images = report.GetImages(allegroFile.StartFootage, allegroFile.EndFootage);
                    for (int i = 0; i < images.Count; ++i)
                    {
                        var page = $"{i + 1}".PadLeft(3, '0');
                        var image = images[i];
                        var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"Test Page {page}" + ".png", CreationCollisionOption.ReplaceExisting);
                        using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                        }
                    }
                }
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
            /*
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".svy");
            var file = await picker.PickSingleFileAsync();
            var factory = new FileFactory(file);
            var newFile = await factory.GetFile();
            */
            FillTreeView();
        }

        private void FillTreeView()
        {
            var treeNodes = new Dictionary<string, TreeViewNode>();
            foreach (var file in NewFiles)
            {
                if (file is AllegroCISFile allegroFile && allegroFile.Header.ContainsKey("segment"))
                {
                    var segmentName = allegroFile.Header["segment"];
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
                var allegroFiles = NewFiles.Where(file => file is AllegroCISFile);
                foreach (AllegroCISFile file in allegroFiles)
                    ToggleFileOnMap(file);
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
            foreach(var (file, layer) in Layers)
            {
                if(layer.Visible)
                {
                    var rect = file.GetGpsArea();
                    area = area?.CombineAreas(rect) ?? rect;
                }
            }
            if(area != null)
            {
                _ = MapControl.TrySetViewBoundsAsync(area, new Thickness(10), MapAnimationKind.None);
            }
        }

        private void CombineButtonClick(object sender, RoutedEventArgs e)
        {
            var fileNodes = FileTreeView.SelectedNodes.Where(node => node.Content is AllegroCISFile).ToList();
            var files = new List<AllegroCISFile>();
            foreach (var node in fileNodes)
                files.Add(node.Content as AllegroCISFile);

        }

        private void HideButtonClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
