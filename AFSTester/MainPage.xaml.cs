using AccurateFileSystem;
using AccurateReportSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
        private Random Random = new Random(1984);
        TreeViewNode HiddenNode = new TreeViewNode() { Content = "Hidden Files" };
        List<(string Name, string Route, double Length, BasicGeoposition Start, BasicGeoposition End)> ShortList;
        string FolderName;

        public MainPage()
        {
            this.InitializeComponent();
            MapControl.StyleSheet = MapStyleSheet.Aerial();
            FileTreeView.RootNodes.Add(HiddenNode);
            var xml = new XmlDocument();
            try
            {
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
                                else if (description[i + 2] != "<td>Main</td>")
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
            catch { return; }
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
            FolderName = folder.DisplayName;
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
                    if (true)//curFile.IsEquivalent(nextFile))
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
            graph1.DrawTopBorder = false;


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
            var exceptions = new OnOff850ExceptionChartSeties(allegroFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo);
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

        private double RandomShift(double curValue, double shift, double min, double max)
        {
            var change = Random.NextDouble() * shift - (shift / 2);
            if (curValue + change > max || curValue + change < min)
                return curValue - change;
            return curValue + change;
        }

        private async void MakeIITGraphs(CombinedAllegroCISFile file, List<(double, double)> dcvgData)
        {
            var curDepth = 50.0;
            var curOff = -1.0;
            var curOn = -1.1;
            var depthData = new List<(double, double)>();
            var onData = new List<(double, double)>();
            var offData = new List<(double, double)>();
            var commentData = new List<(double, string)>();
            var directionData = new List<(double, bool)>();
            if (file != null)
            {
                depthData = file.GetDoubleData("Depth");
                offData = file.GetDoubleData("Off");
                onData = file.GetDoubleData("On");
                commentData = file.GetCommentData("Comment");
                directionData = file.GetDirectionData();
            }
            else
            {
                dcvgData = new List<(double, double)>();
                for (double footage = 0; footage <= 4000; footage += 10)
                {
                    var direction = false;
                    if (footage >= 1300)
                        direction = true;
                    if (footage >= 1800)
                        direction = false;
                    directionData.Add((footage, false));
                    curDepth = RandomShift(curDepth, 5, 10, 100);
                    if (footage % 50 == 0)
                        depthData.Add((footage, curDepth));

                    if (footage == 1000)
                        curOff = -0.8;
                    if (footage == 2000)
                        curOff = -0.6;
                    if (footage == 3000)
                        curOff = -0.4;

                    if (footage < 1000)
                        curOff = RandomShift(curOff, 0.05, -1.1, -0.851);
                    else if (footage < 2000)
                        curOff = RandomShift(curOff, 0.05, -0.849, -0.701);
                    else if (footage < 3000)
                        curOff = RandomShift(curOff, 0.05, -0.699, -0.501);
                    else if (footage < 4000)
                        curOff = RandomShift(curOff, 0.05, -0.499, -0.301);
                    curOn = curOff - 0.1;


                    onData.Add((footage, curOn));
                    offData.Add((footage, curOff));

                    if (footage % 1000 == 200)
                    {
                        dcvgData.Add((footage, 12.0));
                        commentData.Add((footage, "NRI DCVG"));
                    }
                    else if (footage % 1000 == 400)
                    {
                        dcvgData.Add((footage, 20.0));
                        commentData.Add((footage, "Minor DCVG"));
                    }
                    else if (footage % 1000 == 600)
                    {
                        dcvgData.Add((footage, 50.0));
                        commentData.Add((footage, "Moderate DCVG"));
                    }
                    else if (footage % 1000 == 800)
                    {
                        dcvgData.Add((footage, 75.0));
                        commentData.Add((footage, "Severe DCVG"));
                    }
                }
                commentData.Add((0, "Start NRI CIS Area"));
                commentData.Add((1000, "Start Minor CIS Area"));
                commentData.Add((2000, "Start Moderate CIS Area"));
                commentData.Add((3000, "Start Severe CIS Area"));

                commentData.Sort((c1, c2) => c1.Item1.CompareTo(c2.Item1));
            }
            var report = new GraphicalReport();
            report.LegendInfo.NameFontSize = 16f;
            if (file != null && file.Points.Last().Footage < 100)
            {
                report.PageSetup = new PageSetup(100, 0);
                report.XAxisInfo.MajorGridline.Offset = 10;
            }
            var onOffGraph = new Graph(report);
            var on = new GraphSeries("On", onData)
            {
                LineColor = Colors.Blue,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Blue,
                ShapeRadius = 2
            };
            var off = new GraphSeries("Off", offData)
            {
                LineColor = Colors.Green,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Green,
                ShapeRadius = 2
            };
            var depth = new GraphSeries("Depth", depthData)
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = false,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point
            };
            var redLine = new SingleValueGraphSeries("850 Line", -0.85)
            {
                IsDrawnInLegend = false,
                Opcaity = 0.75f
            };
            var commentSeries = new CommentSeries { Values = commentData, PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs, BackdropOpacity = 0.75f };


            onOffGraph.Series.Add(depth);
            onOffGraph.YAxesInfo.Y2IsDrawn = true;
            onOffGraph.CommentSeries = commentSeries;
            onOffGraph.Series.Add(on);
            onOffGraph.Series.Add(off);
            onOffGraph.Series.Add(redLine);
            onOffGraph.DrawTopBorder = false;

            // ACVG
            var dcvgLabels = dcvgData.Select((value) => (value.Item1, value.Item2.ToString("F1") /*+ "%"*/)).ToList();

            // ACVG
            var dcvgIndication = new PointWithLabelGraphSeries("ACVG Indication", -0.2, dcvgLabels)
            {
                ShapeRadius = 3,
                PointColor = Colors.Red,
                BackdropOpacity = 1f
            };

            onOffGraph.Series.Add(dcvgIndication);

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = $"PGE IIT Survey {FolderName}"
                //Title = "PG&E X11134 HCA 1830 11-13-19"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            //var graph1Measurement = new SplitContainerMeasurement(graph1)
            //{
            //    RequestedPercent = 0.5
            //};
            var surveyDirectionChart = new Chart(report, "Survey Direction");
            var cisClass = new Chart(report, "CIS Severity");
            var cisIndication = new PGECISIndicationChartSeries(onData, offData, cisClass);
            cisClass.Series.Add(cisIndication);

            var dcvgClass = new Chart(report, "DCVG Severity");
            var dcvgIndicationSeries = new PgeDcvgIndicationChartSeries(dcvgData, dcvgClass);
            dcvgClass.Series.Add(dcvgIndicationSeries);

            var ecdaClassChart = new Chart(report, "ECDA Clas.");
            var ecdaClassSeries = new PGEDirectExaminationPriorityChartSeries(ecdaClassChart, cisIndication, dcvgIndicationSeries);
            ecdaClassChart.Series.Add(ecdaClassSeries);

            var surveyDirectionSeries = new SurveyDirectionSeries(directionData);
            surveyDirectionChart.Series.Add(surveyDirectionSeries);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);
            //splitContainer.AddSelfSizedContainer(cis850DataChart);
            //splitContainer.AddSelfSizedContainer(cisClass);
            //splitContainer.AddSelfSizedContainer(dcvgClass);
            splitContainer.AddSelfSizedContainer(ecdaClassChart);
            splitContainer.AddSelfSizedContainer(surveyDirectionChart);
            //splitContainer.AddContainer(graph2);
            //splitContainer.AddContainer(graph3);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var surveyLength = onData.Last().Item1;
            var pages = report.PageSetup.GetAllPages(0, surveyLength);
            //
            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var image = report.GetImage(page, 300);
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{FolderName} Page {pageString}" + ".png", CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                }
            }

            var areas = ecdaClassSeries.GetReportQ();
            var output = new StringBuilder();
            var extrapolatedDepth = new List<(double, double)>();
            double curFoot;
            double curExtrapolatedFoot = 0;
            for (int i = 0; i < depthData.Count; ++i)
            {
                (curFoot, curDepth) = depthData[i];
                while (curExtrapolatedFoot <= curFoot)
                {
                    extrapolatedDepth.Add((curExtrapolatedFoot, curDepth));
                    ++curExtrapolatedFoot;
                }
                if (i == depthData.Count - 1)
                {
                    while (curExtrapolatedFoot <= surveyLength)
                    {
                        extrapolatedDepth.Add((curExtrapolatedFoot, curDepth));
                        ++curExtrapolatedFoot;
                    }
                }
                else
                {
                    var (nextFoot, nextDepth) = depthData[i + 1];
                    var factor = (nextDepth - curDepth) / (nextFoot - curFoot);
                    while (curExtrapolatedFoot < nextFoot)
                    {
                        var curExtrapolatedDepth = factor * (curExtrapolatedFoot - curFoot) + curDepth;
                        extrapolatedDepth.Add((curExtrapolatedFoot, curExtrapolatedDepth));
                        ++curExtrapolatedFoot;
                    }
                }
            }
            var skipReport = new StringBuilder();
            foreach (var (startFoot, endFoot, cisSeverity, dcvgSeverity, priority, reason) in areas)
            {
                var depthInArea = extrapolatedDepth.Where(value => value.Item1 >= startFoot && value.Item1 <= endFoot);
                var minDepth = depthInArea.Count() != 0 ? depthInArea.Min(value => value.Item2) : -1;
                output.Append("\t\t\t\t");
                output.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t\t{Math.Max(endFoot - startFoot, 1).ToString("F0")}\t{minDepth.ToString("F0")}\t");
                var startGps = file.GetClosesetGps(startFoot);
                output.Append($"{startGps.Latitude.ToString("F5")}\t{startGps.Longitude.ToString("F5")}\t");
                var endGps = file.GetClosesetGps(endFoot);
                output.Append($"{endGps.Latitude.ToString("F5")}\t{endGps.Longitude.ToString("F5")}\t");
                if (reason == "SKIP.")
                {
                    output.AppendLine($"NT\tNT\tNT\tSkipped");
                    skipReport.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{Math.Max(endFoot - startFoot, 1).ToString("F0")}\t");
                    skipReport.Append($"{startGps.Latitude.ToString("F5")}\t{startGps.Longitude.ToString("F5")}\t");
                    skipReport.AppendLine($"{endGps.Latitude.ToString("F5")}\t{endGps.Longitude.ToString("F5")}");
                }
                else
                    output.AppendLine($"{cisSeverity.GetDisplayName()}\t{dcvgSeverity.GetDisplayName()}\t{PriorityDisplayName(priority)}\t{reason}");
            }
            var reportQ = output.ToString();
            var skipReportString = skipReport.ToString();
            var testStation = file.GetTestStationData();
            var depthException = new StringBuilder();
            for(int i = 0; i < extrapolatedDepth.Count; ++i)
            {
                (curFoot, curDepth) = extrapolatedDepth[i];
                if (curDepth < 36)
                {
                    var start = i;
                    var curIndex = i;
                    var minDepth = curDepth;
                    while(curDepth < 36 && curIndex != extrapolatedDepth.Count)
                    {
                        (curFoot, curDepth) = extrapolatedDepth[curIndex];
                        if (curDepth < minDepth)
                            minDepth = curDepth;
                        ++curIndex;
                    }
                    --curIndex;
                    var (startFoot, _) = extrapolatedDepth[start];
                    var (endFoot, _) = extrapolatedDepth[curIndex];
                    depthException.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{Math.Max(endFoot - startFoot, 1).ToString("F0")}\t{minDepth.ToString("F0")}\t");
                    var startGps = file.GetClosesetGps(startFoot);
                    depthException.Append($"{startGps.Latitude.ToString("F5")}\t{startGps.Longitude.ToString("F5")}\t");
                    var endGps = file.GetClosesetGps(endFoot);
                    depthException.AppendLine($"{endGps.Latitude.ToString("F5")}\t{endGps.Longitude.ToString("F5")}");

                    i = curIndex + 1;
                }
            }
            var depthString = depthException.ToString();
        }

        private string PriorityDisplayName(int priority)
        {
            switch (priority)
            {
                case 1:
                    return "Priority I";
                case 2:
                    return "Priority II";
                case 3:
                    return "Priority III";
                case 4:
                    return "Priority IV";
                default:
                    throw new ArgumentException();
            }
        }

        private string ToStationing(double footage)
        {
            int hundred = (int)footage / 100;
            int tens = (int)footage % 100;
            return hundred.ToString().PadLeft(1, '0') + "+" + tens.ToString().PadLeft(2, '0');
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

        private async void MakeNustarGraphs()
        {
            var mainOnOffData = new List<(double, double)>
            {
                (0,0.9),
(10,-0.7),
(20,-1.7),
(30,-0.7),
(40,5.7),
(50,381.3),
(60,11),
(70,-7.7),
(82,-0.9),
(92,-2.3),
(102,0.2),
(112,3),
(122,3.7),
(132,3.7),
(142,-2.3),
(152,-0.7),
(162,2.1),
(172,-0.4),
(182,-0.9),
(192,10.6),
(202,0),
(212,-7.8),
(222,-3.7),
(232,0.7),
(242,-2.3),
(252,1.9),
(262,-2.8),
(272,-1.6),
(282,0.7),
(292,-0.2),
(302,0),
(312,0.9),
(322,-0.2),
(332,-1.6),
(342,-0.7),
(352,-0.4),
(362,-1.9),
(372,-0.7),
(382,0.2),
(392,0),
(402,0.9),
(412,-1.7),
(422,3.7),
(432,-0.4),
(442,0),
(452,0.2),
(462,0.9),
(472,0.7),
(482,3.7),
(492,0.9),
(502,3.7),
(512,-49.4),
(522,1.6),
(532,6.8),
(542,-0.7),
(552,-4),
(562,0),
(572,3),
(582,17.7),
(592,0.9),
(602,0.9),
(612,-2.1),
(622,5.2),
(632,6.1),
(642,0.4),
(652,3.8),
(662,0.9),
(672,1.6),
(682,4.9),
(692,5.9),
(702,0),
(712,5.9),
(722,-9.7),
(732,2.6),
(742,0),
(752,-42.6),
(762,-2.3),
(772,3.5),
(782,-5.9),
(792,8.7),
(802,0.7),
(812,3.1),
(822,6.8),
(832,0.2),
(842,1),
(852,3.1),
(862,0),
(872,23.3),
(882,0),
(892,-12.2),
(902,14.3),
(912,-2.8),
(922,-0.4),
(932,-4.9),
(942,0.4),
(952,1.9),
(962,3),
(972,0.7),
(982,30.6),
(992,-85.6),
(1002,7),
(1012,0),
(1022,-2.1),
(1032,41.9),
(1042,3.1),
(1052,-1.9),
(1062,5),
(1072,-0.4),
(1082,7.8),
(1092,-1.6),
(1112,3.5),
(1122,3.7),
(1132,12.5),
(1142,32.5),
(1152,-29.6),
(1162,-12.7),
(1172,-1.7),
(1182,0.4),
(1192,67),
(1202,-2.3),
(1212,-0.4),
(1222,-13.2),
(1232,-13.2),
(1242,-1.6),
(1252,-6.4),
(1262,-6.1),
(1272,4.5),
(1282,0),
(1292,-5),
(1302,0.4),
(1312,11),
(1322,10.3),
(1332,-1.2),
            };
            var mainComments = new List<(double, string)>
            {
                (10,"Left: -2.6mV, Right: -5.6mV"),
(70,"Power line crossing, Asphalt Road"),
(142,"Left: 0.7mV, Right: -3.7mV"),
(202,"Side Drain Reading , Culvert drainage"),
(212,"Left: -579mV, Right: -5.9mV"),
(242,"Left: 0.7mV, Right: 4.5mV"),
(332,"Left: -0.4mV, Right: -0.4mV"),
(412,"Left: 0.4mV, Right: 1.6mV"),
(512,"Left: 1.7mV, Right: 6.1mV"),
(552,"Left: 9.1mV, Right: -0.4mV"),
(572,"Left: 6.6mV, Right: 5mV"),
(612,"Left: 1.9mV, Right: 0.7mV"),
(732,"Left: 3.5mV, Right: -0.4mV"),
(762,"Left: 24.4mV, Right: 30.1mV"),
(782,"Left: 10.6mV, Right: -2.8mV"),
(822,"Fence"),
(892,"Left: 5.7mV, Right: 1mV"),
(902,"Dirt Road, Dyke tank rd"),
(912,"Left: 8.7mV, Right: 4.9mV"),
(982,"Side Drain Reading , Emergency valve"),
(992,"Side Drain Reading , Tank farm Left: -94.6mV, Right: -22.4mV"),
(1022,"Left: -0.4mV, Right: -6.1mV"),
(1052,"Left: 1mV, Right: -4mV"),
(1152,"Left: 7.7mV, Right: -10.6mV"),
(1202,"Left: -0.4mV, Right: -17.7mV"),
(1232,"Left: -12.9mV, Right: -7.5mV"),
(1292,"Left: -2.3mV, Right: -1.9mV"),
(1332,"Cultivated Field, End of main line Left: 5.7mV, Right: -6.4mV"),
            };
            var mainHotAnoms = new List<(double, double, string)>
            {
                (10,-0.7,""),
(142,-2.3,""),
(212,-7.8,""),
(242,-2.3,""),
(332,-1.6,""),
(412,-1.7,""),
(512,-49.4,""),
(552,-4,""),
(572,3,""),
(612,-2.1,""),
(732,2.6,""),
(762,-2.3,""),
(782,-5.9,""),
(892,-12.2,""),
(912,-2.8,""),
(992,-85.6,""),
(1022,-2.1,""),
(1052,-1.9,""),
(1152,-29.6,""),
(1202,-2.3,""),
(1232,-13.2,""),
(1292,-5,""),
(1332,-1.2,""),
            };
            var mainACVG = new List<(double, double, string)>
            {
                (220,28,"28"),
(317,5,"5"),
(347,9,"9"),
(367,17,"17"),
(427,18,"18"),
(510,20,"20"),
(552,18,"18"),
(592,16,"16"),
(631,20,"20"),
(672,17,"17"),
(711,22,"22"),
(782,15,"15"),
(830,20,"20"),
(872,18,"18"),
(1022,24,"24"),

            };
            await CreateNustarGraph("Main Line", "Main Line", mainOnOffData, mainComments, mainHotAnoms, mainACVG);

        }

        private async Task CreateNustarGraph(string fileName, string title, List<(double, double)> onData, List<(double, string)> comments, List<(double, double, string)> hotspotAnoms, List<(double, double, string)> acvgAnoms)
        {
            var report = new GraphicalReport();
            report.LegendInfo.NameFontSize = 16f;
            var onOffGraph = new Graph(report);
            onOffGraph.YAxesInfo.Y2Title = "ACVG Value (dbµV)";
            onOffGraph.YAxesInfo.Y1Title = "Potential (Millivolts)";
            onOffGraph.LegendInfo.Name = "Survey Data";
            onOffGraph.YAxesInfo.Y1LabelFormat = "F0";
            var on = new GraphSeries("Hotspot Values", onData)
            {
                LineColor = Colors.Green,
                PointShape = GraphSeries.Shape.None,
            };
            var acvgIndication = new PointWithLabelGraphSeries("ACVG Anomaly", acvgAnoms)
            {
                ShapeRadius = 4,
                PointColor = Colors.Maroon,
                BackdropOpacity = 1f,
                IsY1Axis = false
            };
            var hotspotIndication = new PointWithLabelGraphSeries("Hotspot Anomaly", hotspotAnoms)
            {
                ShapeRadius = 3,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Goldenrod,
                BackdropOpacity = 1f
            };
            var commentSeries = new CommentSeries { Values = comments, PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs, BackdropOpacity = 0.75f };

            var yAxesInfo = report.YAxesInfo;
            yAxesInfo.Y2IsDrawn = true;
            yAxesInfo.Y2IsInverted = false;
            yAxesInfo.Y2MaximumValue = 40;
            yAxesInfo.MajorGridlines.Offset = 100;
            yAxesInfo.MinorGridlines.Offset = 50;

            yAxesInfo.Y1MinimumValue = -500;
            yAxesInfo.Y1MaximumValue = 500;
            yAxesInfo.Y1IsInverted = false;

            onOffGraph.CommentSeries = commentSeries;
            onOffGraph.Series.Add(on);
            onOffGraph.DrawTopBorder = true;
            onOffGraph.DrawBottomBorder = true;
            onOffGraph.Series.Add(hotspotIndication);
            onOffGraph.Series.Add(acvgIndication);

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = $"NuStar 4'' {title} - Rosario, New Mexico"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);
            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(on.Values.First().Item1, on.Values.Last().Item1);

            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var image = report.GetImage(page, 300);
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{fileName} Page {pageString}" + ".png", CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                }
            }
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

        private void IITClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var fileNodesList = FileTreeView.SelectedNodes.Where(node => node.Content is AllegroCISFile);
                var onOffFiles = new List<AllegroCISFile>();
                var dcvgFiles = new List<AllegroCISFile>();
                foreach (var node in fileNodesList)
                {
                    var file = node.Content as AllegroCISFile;
                    if (file.Type == FileType.DCVG)
                        dcvgFiles.Add(file);
                    else
                        onOffFiles.Add(file);
                }
                var combinedFile = CombinedAllegroCISFile.CombineFiles("Combined", onOffFiles);
                var combinedFootages = new List<(double, BasicGeoposition)>();
                foreach (var (foot, _, point, _, _) in combinedFile.Points)
                {
                    if (point.HasGPS)
                        combinedFootages.Add((foot, point.GPS));
                }
                var dcvgData = new List<(double, double)>();
                double lastFoot = double.MaxValue - 10;
                BasicGeoposition lastGps = new BasicGeoposition();
                double footage;
                foreach (var file in dcvgFiles)
                {
                    foreach (var (foot, point) in file.Points)
                    {
                        if (point.HasIndication)
                        {
                            if (!point.HasGPS)
                            {
                                var dist = foot - lastFoot;
                                if (dist <= 10)
                                {
                                    (footage, _) = combinedFootages.AlignPoint(lastGps, false);
                                    dcvgData.Add((footage, point.IndicationPercent));
                                }
                                else
                                    throw new ArgumentException();
                            }
                            (footage, _) = combinedFootages.AlignPoint(point.GPS, false);
                            dcvgData.Add((footage, point.IndicationPercent));
                        }
                        if (point.HasGPS)
                        {
                            lastFoot = foot;
                            lastGps = point.GPS;
                        }
                    }
                }
                var correction = 15.563025007672872650175335959592166719366374913056088;

                var curHardGps = new BasicGeoposition()
                {
                    Latitude = 38.468252981,
                    Longitude = -122.745688108
                };
                var curHardFoot = combinedFootages.AlignPoint(curHardGps, false);
                dcvgData.Add((curHardFoot.Footage, 29 - correction));

                curHardGps = new BasicGeoposition()
                {
                    Latitude = 38.328751459,
                    Longitude = -122.737339361
                };
                curHardFoot = combinedFootages.AlignPoint(curHardGps, false);
                //dcvgData.Add((curHardFoot.Footage, 67 - correction));


                dcvgData.Sort((first, second) => first.Item1.CompareTo(second.Item1));
                MakeIITGraphs(combinedFile, dcvgData);
            }
            catch
            {
                //MakeIITGraphs(null);
                MakeNustarGraphs();
                return;
            }

        }
    }
}
