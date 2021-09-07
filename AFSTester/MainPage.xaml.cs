using AccurateFileSystem;
using AccurateReportSystem;
using DocumentFormat.OpenXml.Packaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
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
using Page = Windows.UI.Xaml.Controls.Page;
using Colors = Windows.UI.Colors;
using Color = Windows.UI.Color;
using PageSetup = AccurateReportSystem.PageSetup;
using DocumentFormat.OpenXml.Spreadsheet;
using ClosedXML.Excel;
using Windows.UI.Popups;
using Microsoft.Graphics.Canvas;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using AccurateFileSystem.Xml;
using AccurateFileSystem.EsriShapefile;
using AccurateFileSystem.Kmz;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AFSTester
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CanvasBitmap Logo { get; set; } = null;
        List<File> NewFiles;
        Dictionary<AllegroCISFile, MapLayer> Layers = new Dictionary<AllegroCISFile, MapLayer>();
        private Random Random = new Random(1984);
        TreeViewNode HiddenNode = new TreeViewNode() { Content = "Hidden Files" };
        List<(string Name, string Route, double Length, BasicGeoposition Start, BasicGeoposition End)> ShortList { get; set; }
        List<(string Name, string Route, double Length, BasicGeoposition Start, BasicGeoposition End)> LongList { get; set; }
        string FolderName;
        bool IsAerial = false;
        string ReportQ { get; set; } = "";

        public MainPage()
        {
            this.InitializeComponent();
            //MakeAtmosPcm();
            OutputFolderTextBox.Text = ApplicationData.Current.LocalFolder.Path;
            FileTreeView.RootNodes.Add(HiddenNode);
            MapControl.StyleSheet = IsAerial ? MapStyleSheet.Aerial() : MapStyleSheet.RoadDark();
            var xml = new XmlDocument();
            try
            {
                var xmlStringTask = Clipboard.GetContent().GetTextAsync().AsTask();
                xmlStringTask.Wait();
                var xmlString = xmlStringTask.Result;
                ShortList = null;
                LongList = null;
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
                        lateralList.Add((name, route, length, start, end));
                    }
                }

                ShortList = lateralList.Where(info => info.Length < 100).ToList();
                var shortListStrings = ShortList.Select(info => $"{info.Name}\t{info.Route}\t{info.Length}\t{info.Start.Latitude}\t{info.Start.Longitude}\t{info.End.Latitude}\t{info.End.Longitude}");
                var shortString = string.Join('\n', shortListStrings);
                LongList = lateralList.Where(info => info.Length >= 100).ToList();
                var longListStrings = LongList.Select(info => $"{info.Name}\t{info.Route}\t{info.Length}\t{info.Start.Latitude}\t{info.Start.Longitude}\t{info.End.Latitude}\t{info.End.Longitude}");
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

        private async void ImportButtonClick(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            FolderName = folder.DisplayName;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);

            NewFiles = new List<File>();
            GeneralXmlFile kml = null;
            foreach (var file in files)
            {
                var factory = new FileFactory(file);
                if (file.DisplayType == "TXT File")
                    continue;
                if (file.DisplayType == "Text Document")
                    continue;
                var newFile = await factory.GetFile();
                if (newFile != null)
                    NewFiles.Add(newFile);
                if (file.FileType.ToLower() == ".jpg" && file.Name.Contains("logo", StringComparison.OrdinalIgnoreCase))
                {
                    CanvasDevice device = CanvasDevice.GetSharedDevice();
                    using (var stream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        Logo = await CanvasBitmap.LoadAsync(device, stream);
                    }
                }
                //if (file.FileType.ToLower() == ".jpg")
                //    kml = await GeneralXmlFile.GetGeneralXml(file);
            }
            if (kml != null)

                NewFiles.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));
            for (int i = 0; i < NewFiles.Count; ++i)
            {
                var curFile = NewFiles[i];
                for (int j = i + 1; j < NewFiles.Count; ++j)
                {
                    var nextFile = NewFiles[j];
                    if (curFile.Name != nextFile.Name)
                        break;
                    if (curFile is AllegroCISFile curAllegro && nextFile is AllegroCISFile nextAllegro)
                    {
                        if (nextAllegro.Extension.ToLower() == ".bak")
                        {
                            NewFiles.RemoveAt(j);
                            --j;
                        }
                        else if (curAllegro.Points.Count >= nextAllegro.Points.Count)//curFile.IsEquivalent(nextFile))
                        {
                            NewFiles.RemoveAt(j);
                            --j;
                        }
                        else
                        {
                            NewFiles.RemoveAt(i);
                            --j;
                            --i;
                            curFile = nextFile;
                        }
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
            var longListDistance = new List<(double Dist, AllegroDataPoint Point)>();
            var testStationComments = new List<(AllegroDataPoint, BasicGeoposition)>();
            var allOtherComments = new List<(AllegroDataPoint, BasicGeoposition)>();

            for (int i = 0; i < NewFiles.Count; ++i)
            {
                var file = NewFiles[i];
                if (file is AllegroCISFile allegroFile)
                {
                    foreach (var (_, point) in allegroFile.Points)
                    {
                        if (point.TestStationReads.Count > 0)
                            testStationComments.Add((point, point.GPS));
                        else if (!string.IsNullOrWhiteSpace(point.OriginalComment))
                            allOtherComments.Add((point, point.GPS));
                    }
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
                    for (int j = 0; j < LongList.Count; ++j)
                    {
                        var (_, _, _, start, end) = LongList[j];
                        var (newShort, newPoint) = allegroFile.GetClosestPoint(start, end);
                        if (longListDistance.Count == j)
                        {
                            longListDistance.Add((newShort, newPoint));
                            continue;
                        }
                        var (curShort, _) = longListDistance[j];
                        if (newShort < curShort)
                            longListDistance[j] = (newShort, newPoint);
                    }
                }
            }
            var shortCloseStringList = shortListDistance.Select(info => $"{info.Dist}\t{RoundDist(info.Dist)}\t{info.Point.On}\t{info.Point.Off}\t{info.Point.GPS.Latitude}\t{info.Point.GPS.Longitude}\t{info.Point.OriginalComment}");
            var shortCloseString = string.Join('\n', shortCloseStringList);

            var longCloseStringList = longListDistance.Select(info => $"{info.Dist}\t{RoundDist(info.Dist)}\t{info.Point.On}\t{info.Point.Off}\t{info.Point.GPS.Latitude}\t{info.Point.GPS.Longitude}\t{info.Point.OriginalComment}");
            var longCloseString = string.Join('\n', longCloseStringList);

            var commentsStringList = testStationComments.Select(value => $"{value.Item1.MirOn:F3}\t{value.Item1.MirOff:F3}\t{value.Item1.OriginalComment}\t{value.Item2.Latitude:F8}\t{value.Item2.Longitude:F8}");
            var commentsOutput = string.Join("\n", commentsStringList);

            var allCommentsStringList = allOtherComments.Select(value => $"{value.Item1.MirOn:F3}\t{value.Item1.MirOff:F3}\t{value.Item1.OriginalComment}\t{value.Item2.Latitude:F8}\t{value.Item2.Longitude:F8}");
            var allCommentsOutput = string.Join("\n", allCommentsStringList);
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

        private async Task MakeGraphs(CombinedAllegroCISFile allegroFile, string exact = null)
        {
            var testStationInitial = allegroFile.GetTestStationData();
            var firstPoint = allegroFile.Points.First();
            var startComment = firstPoint.Footage + " -> " + firstPoint.Point.StrippedComment;
            var lastPoint = allegroFile.Points.Last();
            var endComment = lastPoint.Footage + " -> " + lastPoint.Point.StrippedComment;
            (string, bool)? response;
            if (exact == null)
                response = await InputTextDialogAsync($"PG&E LS {allegroFile.Name.Replace("ls", "", StringComparison.OrdinalIgnoreCase).Replace("line", "", StringComparison.OrdinalIgnoreCase).Trim()} MP START to MP END", testStationInitial, startComment, endComment);
            else
                response = (exact, false);//await InputTextDialogAsync(exact, testStationInitial, startComment, endComment);

            if (response == null)
                return;
            if (response.Value.Item2)
            {
                allegroFile.Reverse();
            }
            var report = new GraphicalReport()
            {
                Logo = Logo
            };
            var commentGraph = new Graph(report);
            var graph1 = new Graph(report);
            var graph2 = new Graph(report);
            var graph3 = new Graph(report);
            var mirFilterData = "Start Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tReason\n" + ((MirFilter.IsChecked ?? false) ? allegroFile.FilterMir(new List<string>() { "anode", "rectifier" }) : "");
            allegroFile.FixGps();
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
            var commentSeries = new CommentSeries { Values = allegroFile.GetCommentData(), PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs };
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
            if (allegroFile.Type == FileType.OnOff)
            {
                graph1.Series.Add(depth);
                graph1.YAxesInfo.Y2IsDrawn = true;
            }
            if (!seperateComment)
                graph1.CommentSeries = commentSeries;
            /*
            graph1.YAxesInfo.Y1MaximumValue = 150;
            graph1.YAxesInfo.Y1MinimumValue = 0;
            graph1.YAxesInfo.Y1IsInverted = false;
            graph1.Gridlines[(int)GridlineName.MajorHorizontal].Offset = 15;
            graph1.Gridlines[(int)GridlineName.MinorHorizontal].Offset = 5;
            */
            if (allegroFile.Type != FileType.OnOff)
            {
                graph1.YAxesInfo.Y1MinimumValue = -0.75;
                graph1.YAxesInfo.MajorGridlines.Offset = 0.125;
                graph1.YAxesInfo.MinorGridlines.Offset = 0.025;
            }

            graph1.Series.Add(on);
            if (allegroFile.Type == FileType.OnOff)
            {
                graph1.Series.Add(off);
                graph1.Series.Add(onMir);
                graph1.Series.Add(offMir);
            }
            if (allegroFile.Type != FileType.Native)
                graph1.Series.Add(redLine);
            else
                on.Name = "Static";
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
                Title = response.Value.Item1
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            //var graph1Measurement = new SplitContainerMeasurement(graph1)
            //{
            //    RequestedPercent = 0.5
            //};
            var chart1 = new Chart(report, "Survey Direction and Survey Date");
            chart1.LegendInfo.NameFontSize = 14f;
            var chart2 = new Chart(report, "850 Data");
            chart2.LegendInfo.SeriesNameFontSize = 8f;
            chart2.LegendInfo.NameFontSize = 16f;

            var mirSeries = new MirDirection(allegroFile.GetReconnects());
            ExceptionsChartSeries exceptions = new OnOff850ExceptionChartSeries(allegroFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo)
            {
                LegendLabelSplit = 0.5f
            };
            if (IsSempra.IsChecked ?? false)
            {
                chart2.LegendInfo.Name = "Exception Data";
                chart2.LegendInfo.NameFontSize = 14f;
                exceptions = new Sempra850ExceptionChartSeries(allegroFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo)
                {
                    LegendLabelSplit = 0.5f
                };
                chart1.YAxesInfo.Y2IsDrawn = false;
            }
            chart2.Series.Add(exceptions);
            //chart1.LegendInfo.NameFontSize = 18f;

            var chart1Series = new SurveyDirectionWithDateSeries(allegroFile.GetDirectionWithDateData());
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
            if (allegroFile.Type != FileType.Native)
                splitContainer.AddSelfSizedContainer(chart2);//On
            splitContainer.AddSelfSizedContainer(chart1);
            //splitContainer.AddContainer(graph2);
            //splitContainer.AddContainer(graph3);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(0, allegroFile.Points.Last().Footage);
            var curFileName = $"{response.Value.Item1}\\{topGlobalXAxis.Title}";
            await CreateStandardExcel(curFileName, allegroFile);
            if (MirFilter.IsChecked ?? false)
                await CreateExcelFile($"{curFileName} MIR Skips", new List<(string Name, string Data)>() { ("MIR Skips", mirFilterData) });
            var imageFiles = new List<StorageFile>();
            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{topGlobalXAxis.Title} Page {pageString}.png";
                if (pages.Count == 1)
                    fileName = $"{topGlobalXAxis.Title} Graph.png";
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{response.Value.Item1}\\{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
            var dialog = new MessageDialog($"Finished making {topGlobalXAxis.Title}");
            //await dialog.ShowAsync();
        }

        private async Task CreateStandardExcel(string fileName, CombinedAllegroCISFile allegroFile)
        {
            var tabular = allegroFile.GetTabularData();
            await CreateExcelFile($"{fileName} Tabular Data", new List<(string Name, string Data)>() { ("Tabular Data", tabular) });
            var dataMetrics = new DataMetrics(allegroFile.GetPoints());
            await CreateExcelFile($"{fileName} Data Metrics", dataMetrics.GetSheets());
            var testStation = allegroFile.GetTestStationData();
            await CreateExcelFile($"{fileName} Test Station Data", new List<(string Name, string Data)>() { ("Test Station Data", testStation) });
            var cisSkips = allegroFile.GetSkipData();
            await CreateExcelFile($"{fileName} Skip Data", new List<(string Name, string Data)>() { ("Skip Data", cisSkips) });
            var depthExceptions = allegroFile.GetDepthExceptions(36, double.MaxValue);
            await CreateExcelFile($"{fileName} Shallow Cover", new List<(string Name, string Data)>() { ("Shallow Cover", depthExceptions) });
            var shapefile = allegroFile.GetShapeFile();
            await CreateExcelFile($"{fileName} Shapefile", new List<(string Name, string Data)>() { ("Shapefile", shapefile) });
            await CreateExcelFile($"{fileName} Files Order", new List<(string Name, string Data)>() { ("Order", allegroFile.FileInfos.GetExcelData(0)) });
        }

        private async Task<(string, bool)?> InputTextDialogAsync(string title, string testStationData, string firstComment, string lastComment)
        {
            StackPanel panel = new StackPanel()
            {
                Orientation = Orientation.Vertical
            };
            var mpRegex = new Regex("mp\\s?(\\d+\\.\\d+)");
            var startMpMatch = mpRegex.Match(firstComment);
            var endMpMatch = mpRegex.Match(lastComment);

            if (startMpMatch.Success)
            {
                title = title.Replace("START", startMpMatch.Groups[1].Value);
            }
            if (endMpMatch.Success)
            {
                title = title.Replace("END", endMpMatch.Groups[1].Value);
            }
            TextBox inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32,
                Text = title
            };

            var lineSplit = testStationData.Split('\n');
            ListBox testStationList = new ListBox();
            testStationList.Items.Add(new ListBoxItem() { Content = firstComment });

            for (int i = 1; i < lineSplit.Length; ++i)
            {
                var curSplit = lineSplit[i].Split('\t');
                if (curSplit.Length < 7)
                    continue;
                var footage = curSplit[0];
                var line = curSplit[4];
                var item = new ListBoxItem()
                {
                    Content = footage + " -> " + line
                };
                if (lineSplit.Length < 5)
                {
                    testStationList.Items.Add(item);
                    continue;
                }
                if (i > 2 && i < lineSplit.Length - 3)
                    continue;
                testStationList.Items.Add(item);
                if (i == 2)
                {
                    item = new ListBoxItem()
                    {
                        Content = "..."
                    };
                    testStationList.Items.Add(item);
                }
            }
            testStationList.Items.Add(new ListBoxItem() { Content = lastComment });

            CheckBox isReverse = new CheckBox()
            {
                Content = "Is Reverse?"
            };

            panel.Children.Add(testStationList);
            panel.Children.Add(inputTextBox);
            panel.Children.Add(isReverse);
            ContentDialog dialog = new ContentDialog
            {
                Content = panel,
                Title = title,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                return (inputTextBox.Text, isReverse.IsChecked.Value);
            else
                return null;
        }

        public async Task CreateExcelFile(string fileName, List<(string Name, string Data)> sheets)
        {
            var outputFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName + ".xlsx", CreationCollisionOption.ReplaceExisting);
            using (var outStream = await outputFile.OpenStreamForWriteAsync())
            using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                var wbPart = spreadDoc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                wbPart.Workbook.AppendChild(new Sheets());
                var id = 1;
                foreach (var (name, data) in sheets)
                {
                    AddData(wbPart, data, id, name, new List<string>());
                    ++id;
                }
                wbPart.Workbook.Save();
            }
        }

        private double RandomShift(double curValue, double shift, double min, double max)
        {
            var change = Random.NextDouble() * shift - (shift / 2);
            if (curValue + change > max || curValue + change < min)
                return curValue - change;
            return curValue + change;
        }

        private (double HcaStartFootage, double HcaEndFootage) AddHcaComments(CombinedAllegroCISFile file, Hca hca)
        {
            var startGap = hca.GetStartFootageGap();
            file.ShiftPoints(startGap);
            BasicGeoposition hcaStartGps = hca.GetFirstNonSkipGps();
            BasicGeoposition hcaEndGps = hca.GetLastNonSkipGps();
            bool hasStartBuffer = hca.StartBuffer != null;
            bool hasEndBuffer = hca.EndBuffer != null;
            var (hcaStartPoint, hcaStartDistance) = file.GetClosestPoint(hcaStartGps);
            if (hasStartBuffer)
            {
                file.Points[file.HasStartSkip ? 1 : 0].Point.OriginalComment += " START OF BUFFER";
            }
            else
            {
                hcaStartPoint = file.Points[file.HasStartSkip ? 1 : 0];
            }


            hcaStartPoint.Point.OriginalComment += (hasStartBuffer ? " END OF BUFFER" : "") + " START OF HCA";
            hcaStartPoint.Point.GPS = hcaStartGps;

            var (hcaEndPoint, hcaEndDistance) = file.GetClosestPoint(hcaEndGps);
            if (hasEndBuffer)
            {
                file.Points[file.Points.Count - (file.HasEndSkip ? 2 : 1)].Point.OriginalComment += " END OF BUFFER";
            }
            else
            {
                hcaEndPoint = file.Points[file.Points.Count - (file.HasEndSkip ? 2 : 1)];
            }

            hcaEndPoint.Point.GPS = hcaEndGps;
            hcaEndPoint.Point.OriginalComment += " END OF HCA" + (hasEndBuffer ? " START OF BUFFER" : "");
            return (hcaStartPoint.Footage, hcaEndPoint.Footage);
        }

        private void AddMaxDepthComment(CombinedAllegroCISFile file, double maxDepth)
        {
            foreach (var point in file.Points)
            {
                if ((point.Point.Depth ?? 0) > maxDepth)
                {
                    var tempDepthString = $" Depth: {point.Point.Depth.Value} Inches";
                    point.Point.OriginalComment += tempDepthString;
                }
            }
        }

        private bool CheckDepthGaps(List<(double Footage, double Depth)> depthData, double startFootage, double endFootage, double maxGap)
        {
            var firstDepth = depthData.First();
            if (firstDepth.Footage - startFootage > maxGap)
                return true;
            for (int i = 1; i < depthData.Count; ++i)
            {
                var curDepth = depthData[i - 1];
                var nextDepth = depthData[i];
                if (nextDepth.Footage - curDepth.Footage > maxGap)
                    return true;
            }
            var lastDepth = depthData.Last();
            if (endFootage - lastDepth.Footage > maxGap)
                return true;

            return false;
        }

        private List<int> GetActualReadFootage(List<(double Start, double End, PGESeverity CisSeverity, PGESeverity DcvgSeverity, HcaRegion Region, int Overall, string Comments)> areas)
        {
            var output = Enumerable.Repeat(0, 5).ToList();
            foreach (var area in areas)
            {
                if (area.Comments.Contains("SKIP")) continue;
                var distance = (int)(area.End - area.Start);
                output[0] += distance;
                output[area.Overall] += distance;
            }
            return output;
        }


        private async Task MakeQuickIITGraphs(CombinedAllegroCISFile file, List<(double, double, BasicGeoposition)> dcvgData, List<(double Footage, double Current, double Depth, string Comment)> pcmInput, string folderName)
        {
            var maxDepth = 200;
            var curDepth = 50.0;
            var curOff = -1.0;
            var curOn = -1.1;
            var depthData = new List<(double, double)>();
            var onData = new List<(double, double)>();
            var offData = new List<(double, double)>();
            var commentData = new List<(double, string)>();
            var directionData = new List<(double, bool, string)>();
            var pcmCommentData = pcmInput.Select(val => (val.Footage, "PCM: " + val.Comment)).ToList();
            foreach (var point in file.Points)
            {
                if ((point.Point.Depth ?? 0) > maxDepth)
                {
                    var tempDepthString = $" Depth: {point.Point.Depth.Value} Inches";
                    point.Point.OriginalComment += tempDepthString;
                }
            }
            depthData = pcmInput.Where(val => val.Depth != 0).Select(val => (val.Footage, val.Depth)).ToList();
            offData = file.GetDoubleData("Off");
            onData = file.GetDoubleData("On");
            commentData = file.GetCommentData();
            commentData.AddRange(pcmCommentData);
            commentData.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            directionData = file.GetDirectionWithDateData();
            if (onData.Count == 1)
            {
                onData.Add((5, onData[0].Item2));
                offData.Add((5, offData[0].Item2));
            }
            var report = new GraphicalReport();
            report.LegendInfo.NameFontSize = 16f;
            if (file != null && file.Points.Last().Footage < 100)
            {
                report.PageSetup = new PageSetup(100, 10);
                report.XAxisInfo.MajorGridline.Offset = 10;
            }
            var onOffGraph = new Graph(report);
            var on = new GraphSeries("On", onData)
            {
                LineColor = Colors.Blue,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Blue,
                ShapeRadius = 2,
                MaxDrawDistance = 19
            };
            var off = new GraphSeries("Off", offData)
            {
                LineColor = Colors.Green,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Green,
                ShapeRadius = 2,
                MaxDrawDistance = 19
            };
            var depth = new GraphSeries("Depth", depthData)
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = false,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point,
                ShapeRadius = 4
            };
            var redLine = new SingleValueGraphSeries("850 Line", -0.85)
            {
                IsDrawnInLegend = false,
                Opcaity = 0.75f
            };
            var commentSeries = new CommentSeries { Values = commentData, PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs, BackdropOpacity = 0.75f };


            onOffGraph.Series.Add(depth);
            onOffGraph.YAxesInfo.Y2IsDrawn = true;
            onOffGraph.YAxesInfo.Y2MaximumValue = maxDepth;
            onOffGraph.CommentSeries = commentSeries;
            onOffGraph.Series.Add(on);
            onOffGraph.Series.Add(off);
            onOffGraph.Series.Add(redLine);
            onOffGraph.DrawTopBorder = false;

            if (file != null && file.Points.Last().Footage < 100)
            {
                onOffGraph.CommentSeries.PercentOfGraph = 0.25f;
            }

            var dcvgLabels = dcvgData.Select((value) => (value.Item1, value.Item2.ToString("F1") + "%")).ToList();

            var indicationLabel = "DCVG";
            var dcvgIndication = new PointWithLabelGraphSeries($"{indicationLabel} Indication", -0.2, dcvgLabels)
            {
                ShapeRadius = 3,
                PointColor = Colors.Red,
                BackdropOpacity = 1f
            };
            onOffGraph.Series.Add(dcvgIndication);

            var pcmLables = new List<(double, string)>();
            pcmLables = pcmInput.Where(val => val.Current != 0).Select(val => (val.Footage, (val.Current * 1000).ToString("F0"))).ToList();
            var pcmData = pcmLables.Select(value => (value.Item1, -0.4, value.Item2)).ToList();
            var pcm2 = new PointWithLabelGraphSeries("PCM (mA)", pcmData)
            {
                LineColor = Colors.Black,
                PointColor = Colors.Navy,
                IsY1Axis = true,
                PointShape = GraphSeries.Shape.Square,
                GraphType = GraphSeries.Type.Point
            };
            onOffGraph.Series.Add(pcm2);

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;
            //onOffGraph.YAxesInfo.Y2Title += " & PCM (dBmA)";

            var pcmComments = new Graph(report);

            var pcmCommentSeries = new CommentSeries { Values = pcmCommentData, PercentOfGraph = 1f, IsFlippedVertical = false, BorderType = BorderType.Pegs, BackdropOpacity = 0.75f };
            pcmComments.CommentSeries = pcmCommentSeries;
            pcmComments.XAxisInfo.Title = "PCM Comments";

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = file == null ? "Example Graph" : $"PGE IIT Data {folderName}"
                //Title = "PG&E X11134 HCA 1830 11-13-19"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            //var graph1Measurement = new SplitContainerMeasurement(graph1)
            //{
            //    RequestedPercent = 0.5
            //};
            var surveyDirectionChart = new Chart(report, "Survey Direction With Survey Date");
            surveyDirectionChart.LegendInfo.NameFontSize = 14f;

            var surveyDirectionSeries = new SurveyDirectionWithDateSeries(directionData);
            surveyDirectionChart.Series.Add(surveyDirectionSeries);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);
            //splitContainer.AddSelfSizedContainer(cis850DataChart);
            //splitContainer.AddSelfSizedContainer(cisClass);
            //splitContainer.AddSelfSizedContainer(dcvgClass);
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
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{folderName} Page {pageString}" + ".png", CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                }
            }
        }

        private void MakeReportLWorksheet(XLWorkbook book)
        {
            var worksheet = book.Worksheets.Add("Report L");

            #region Title Cell
            var titleCell = worksheet.Cell("A8");
            titleCell.Value = "HCA Indication Summary";
            titleCell.Style.Font.Italic = true;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontColor = XLColor.Blue;
            #endregion
            #region Date of Cell
            var dateOfCell = worksheet.Cell("A10");
            dateOfCell.Value = $"Date of Report: {DateTime.Now.ToShortDateString()}";
            #endregion

        }

        private void AddData(WorkbookPart workbook, string data, int worksheetId, string worksheetName, List<string> mergeCellStrings)
        {
            var lines = data.Split('\n');
            var worksheet = workbook.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheet.Worksheet = new Worksheet(sheetData);

            var sheet = new Sheet()
            {
                Id = workbook.GetIdOfPart(worksheet),
                SheetId = (UInt32)worksheetId,
                Name = worksheetName
            };

            UInt32 rowIndex = 1;
            foreach (var line in lines)
            {
                var row = new Row();
                row.RowIndex = rowIndex;
                var cells = line.Split('\t');
                foreach (var cell in cells)
                {
                    var cellValue = new CellValue(cell);
                    var inlineString = new InlineString();
                    var newCell = new Cell();
                    if (double.TryParse(cell, out _))
                        newCell.DataType = CellValues.Number;
                    else
                        newCell.DataType = CellValues.String;
                    newCell.CellValue = cellValue;
                    row.AppendChild(newCell);
                }
                sheetData.AppendChild(row);
                ++rowIndex;
            }
            workbook.Workbook.Sheets.AppendChild(sheet);

            var mergeCells = new MergeCells();
            //sheet.InsertAfterSelf(mergeCells);
            foreach (var mergeCellString in mergeCellStrings)
            {
                var mergeCell = new MergeCell() { Reference = new DocumentFormat.OpenXml.StringValue(mergeCellString) };
                mergeCells.Append(mergeCell);
            }
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
            FileTreeView.RootNodes.Clear();
            var cisTreeNodes = new Dictionary<string, TreeViewNode>();
            var depolTreeNodes = new Dictionary<string, TreeViewNode>();
            foreach (var file in NewFiles)
            {
                if (file is AllegroCISFile allegroFile && allegroFile.Header.ContainsKey("segment"))
                {
                    if (allegroFile.Type == FileType.OnOff)
                    {
                        AddFileToNodes(cisTreeNodes, allegroFile);
                    }
                    else if (allegroFile.Type == FileType.Native)
                    {
                        AddFileToNodes(depolTreeNodes, allegroFile);
                    }
                }
            }
            if (cisTreeNodes.Count != 0)
            {
                var cisTreeNode = new TreeViewNode() { Content = "CIS" };
                FileTreeView.RootNodes.Add(cisTreeNode);
                foreach (var treeNode in cisTreeNodes.Values)
                    cisTreeNode.Children.Add(treeNode);
            }

            if (depolTreeNodes.Count != 0)
            {
                var depolTreeNode = new TreeViewNode() { Content = "Depol" };
                FileTreeView.RootNodes.Add(depolTreeNode);
                foreach (var treeNode in depolTreeNodes.Values)
                    depolTreeNode.Children.Add(treeNode);
            }
        }

        private void AddFileToNodes(Dictionary<string, TreeViewNode> treeNodes, AllegroCISFile allegroFile)
        {
            var segmentName = Regex.Replace(Regex.Replace(allegroFile.Header["segment"], "\\s+", ""), "(?i)ls", "").Replace('.', '-').ToLower().Trim();
            if (!treeNodes.ContainsKey(segmentName))
            {
                var newSegmentNode = new TreeViewNode() { Content = segmentName };
                treeNodes.Add(segmentName, newSegmentNode);
            }
            var segmentNode = treeNodes[segmentName];
            segmentNode.Children.Add(new TreeViewNode() { Content = allegroFile });
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
                        //elements.Add(startIcon);
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
                        //elements.Add(endIcon);
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
                if (positions.Count == 0)
                    return;
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

        private void DoMapButtonClick(object sender, RoutedEventArgs e)
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

        private async void CombineButtonClick(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(CombineMaxGap.Text, out var maxGap))
                return;
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

            if (files.Count == 0)
            {
                return;
            }

            var test = CombinedAllegroCISFile.CombineFiles(files.First().Header["segment"].Trim(), files, maxGap);
            //test.FixContactSpikes();
            await MakeGraphs(test);
        }

        private async void MakeNustarGraphs(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var files = await folder.GetFilesAsync();
            StorageFile mainFile1 = null;
            StorageFile indexFile1 = null;
            StorageFile mainFile2 = null;
            StorageFile indexFile2 = null;
            AccurateFileSystem.Dbf.DbfFile dbf1 = null, dbf2 = null;
            AccurateFileSystem.EsriShapefile.MainFile shp1 = null, shp2 = null;
            GeneralXmlFile xml = null;
            foreach (var file in files)
            {
                if (file.FileType.ToLower() == ".kml")
                    xml = await GeneralXmlFile.GetGeneralXml(file);
                if (file.FileType.ToLower() == ".shp" && file.DisplayName.Contains('_'))
                    mainFile1 = file;
                else if (file.FileType.ToLower() == ".shp")
                    mainFile2 = file;
                if (file.FileType.ToLower() == ".shx" && file.DisplayName.Contains('_'))
                    indexFile1 = file;
                else if (file.FileType.ToLower() == ".shx")
                    indexFile2 = file;
                if (file.FileType.ToLower() == ".dbf" && file.DisplayName.Contains('_'))
                    dbf1 = await AccurateFileSystem.Dbf.DbfFile.GetDbfFile(file);
                else if (file.FileType.ToLower() == ".dbf")
                    dbf2 = await AccurateFileSystem.Dbf.DbfFile.GetDbfFile(file);
            }
            shp1 = await AccurateFileSystem.EsriShapefile.MainFile.GetMainFile(mainFile1, indexFile1);
            shp2 = await AccurateFileSystem.EsriShapefile.MainFile.GetMainFile(mainFile2, indexFile2);

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
                MaxDrawDistance = 100
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

            var start = on.Values.First().Item1;
            var end = on.Values.Last().Item1;
            var distance = end - start;
            if (distance < 500)
            {
                report.PageSetup.Overlap = 10;
                report.PageSetup.UnitsPerPage = 100;
                report.XAxisInfo.MajorGridline.Offset = 10;
                report.XAxisInfo.MinorGridline.IsEnabled = false;
            }

            var pages = report.PageSetup.GetAllPages(start, distance);

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
                var combinedFile = CombinedAllegroCISFile.CombineOrderedFiles("Combined", onOffFiles, 10);
                var combinedFootages = new List<(double, BasicGeoposition)>();
                foreach (var (foot, _, point, _, _) in combinedFile.Points)
                {
                    if (point.HasGPS)
                        combinedFootages.Add((foot, point.GPS));
                }
                var dcvgData = new List<(double, double, BasicGeoposition)>();
                double lastFoot = double.MaxValue - 10;
                BasicGeoposition lastGps = new BasicGeoposition();
                double regFoot, regDist, extrapFoot, extrapDist;
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
                                    (regFoot, regDist, extrapFoot, extrapDist, _) = combinedFootages.AlignPoint(lastGps);
                                    dcvgData.Add((regFoot, point.IndicationPercent, lastGps));
                                }
                                else
                                    throw new ArgumentException();
                            }
                            (regFoot, regDist, extrapFoot, extrapDist, _) = combinedFootages.AlignPoint(point.GPS);
                            dcvgData.Add((regFoot, point.IndicationPercent, point.GPS));
                        }
                        if (point.HasGPS)
                        {
                            lastFoot = foot;
                            lastGps = point.GPS;
                        }
                    }
                }
                var correction = 15.563025007672872650175335959592166719366374913056088;


                dcvgData.Sort((first, second) => first.Item1.CompareTo(second.Item1));
                //MakeIITGraphs(combinedFile, dcvgData, false, FolderName);
            }
            catch
            {
                //MakeIITGraphs(null, null, true, "Example Graph");
                //MakeNustarGraphs();
                return;
            }

        }

        private Dictionary<string, List<List<BasicGeoposition>>> GetKmlLineData(GeneralXmlFile file)
        {
            var placemarkData = new Dictionary<string, List<List<BasicGeoposition>>>();
            if (file == null) return placemarkData;
            var placemarks = file.GetObjects("Placemark");

            foreach (var placemark in placemarks)
            {
                var name = placemark.GetObjects("name")[0].Value;
                var coordsObjs = placemark.GetObjects("coordinates");
                foreach (var coordsObj in coordsObjs)
                {
                    var gps = new List<BasicGeoposition>();
                    var coords = coordsObj.Value.Trim().Split(' ');
                    foreach (var coord in coords)
                    {
                        var split = coord.Split(',');
                        var lon = double.Parse(split[0]);
                        var lat = double.Parse(split[1]);
                        gps.Add(new BasicGeoposition() { Latitude = lat, Longitude = lon });
                    }
                    if (!placemarkData.ContainsKey(name))
                        placemarkData.Add(name, new List<List<BasicGeoposition>>());
                    placemarkData[name].Add(gps);
                }
            }
            return placemarkData;
        }

        private List<(double, double, BasicGeoposition)> GetAlignedAcvgData(List<(BasicGeoposition, double)> acvgReads, List<(double, BasicGeoposition)> cisCombinedData, Hca hca)
        {
            var output = new List<(double, double, BasicGeoposition)>();
            foreach (var (readGps, read) in acvgReads)
            {
                var (_, _, acvgExtrapFoot, _, gps) = cisCombinedData.AlignPoint(readGps);
                var region = hca.GetClosestRegion(gps);
                if (region.ShouldSkip)
                    continue;
                output.Add((acvgExtrapFoot, read, readGps));
            }
            return output;
        }

        private List<(double, double, BasicGeoposition)> GetAlignedDcvgData(AllegroCISFile dcvgFile, List<(double, BasicGeoposition)> cisCombinedData, Hca hca)
        {
            BasicGeoposition lastGps = new BasicGeoposition();
            var output = new List<(double, double, BasicGeoposition)>();
            foreach (var (foot, point) in dcvgFile.Points)
            {
                if (point.HasGPS)
                {
                    lastGps = point.GPS;
                }
                if (point.HasIndication)
                {
                    var (_, _, extrapFoot, _, gps) = cisCombinedData.AlignPoint(lastGps);
                    var region = hca.GetClosestRegion(gps);
                    if (region.ShouldSkip)
                        continue;
                    if (lastGps.Equals(new BasicGeoposition()))
                        throw new ArgumentException();
                    output.Add((extrapFoot, point.IndicationPercent, point.GPS));
                }
            }

            return output;
        }

        private List<AllegroCISFile> GetUniqueFiles(List<AllegroCISFile> files)
        {
            files.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));
            for (int i = 0; i < files.Count; ++i)
            {
                var curFile = files[i];
                for (int j = i + 1; j < files.Count; ++j)
                {
                    var nextFile = files[j];
                    if (curFile.Name != nextFile.Name)
                        break;
                    if (true)//curFile.IsEquivalent(nextFile))
                    {
                        files.RemoveAt(j);
                        --j;
                    }
                }
            }
            return files;
        }

        private List<(BasicGeoposition Gps, double Read)> ParseAcvgReads(List<string> lines)
        {
            var correction = 15.563025007672872650175335959592166719366374913056088;
            var output = new List<(BasicGeoposition Gps, double Read)>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var splitLine = line.Split(',');
                var lat = double.Parse(splitLine[0]);
                var lon = double.Parse(splitLine[1]);
                var read = double.Parse(splitLine[2]) - correction;
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                output.Add((gps, read));
            }
            return output;
        }

        private void ToggleAerial(object sender, RoutedEventArgs e)
        {
            IsAerial = !IsAerial;
            MapControl.StyleSheet = IsAerial ? MapStyleSheet.Aerial() : MapStyleSheet.RoadDark();
        }

        private async void ConvertSvyToCsv(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            foreach (var file in files)
            {
                var factory = new FileFactory(file);
                var parsedFile = await factory.GetFile();
                if (!(parsedFile is AllegroCISFile))
                    continue;
                var allegroFile = parsedFile as AllegroCISFile;
                if (allegroFile.Extension == ".svy")
                {
                    var outputFile = await folder.CreateFileAsync(allegroFile.Name + ".csv", CreationCollisionOption.GenerateUniqueName);
                    await FileIO.WriteTextAsync(outputFile, allegroFile.ToStringCsv());
                }
            }
        }

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(CombineMaxGap.Text, out var maxGap))
                return;
            var alreadyDone = "";
            if (CombineFilter.IsChecked ?? false)
            {
                var finalFolder = ApplicationData.Current.LocalFolder;
                var finalFolders = await finalFolder.GetFoldersAsync();
                foreach (var folder in finalFolders)
                {
                    alreadyDone += folder.DisplayName.ToLower();
                }
            }
            foreach (var rootNode in FileTreeView.RootNodes)
            {
                if (rootNode.Equals(HiddenNode)) continue;
                if (!FileTreeView.SelectedNodes.Contains(rootNode) && FileTreeView.SelectedNodes.Count > 0)
                    continue;
                if (alreadyDone.Contains("ls " + rootNode.Content.ToString().Trim()))
                {
                    continue;
                }
                var fileNodes = rootNode.Children.Where(node => node.Content is AllegroCISFile).ToList();
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
                if (files.Count == 0)
                    continue;
                try
                {
                    var test = CombinedAllegroCISFile.CombineFiles(files.First().Header["segment"].Trim().ToUpper(), files, maxGap);
                    if (test == null)
                        continue;
                    //test.FixContactSpikes();
                    await MakeGraphs(test);
                }
                catch
                {
                    Debug.WriteLine($"Failed creating {rootNode.Content}");
                }
            }
        }

        private List<(double, double)> atmosPcmData = new List<(double, double)>
        {
            (50,990),
(100,942),
(150,994),
(200,1000),
(250,944),
(300,904),
(350,909),
(400,916),
(450,901),
(500,889),
(550,950),
(600,873),
(650,882),
(700,922),
(750,895),
(800,898),
(850,905),
(900,898),
(950,855),
(1000,885),
(1050,910),
(1100,878),
(1150,831),
(1200,844),
(1250,826),
(1300,836),
(1350,804),
(1400,813),
(1450,825),
(1500,801),
(1550,782),
(1600,825),
(1650,798),
(1700,794),
(1750,785),
(1800,778),
(1850,764),
(1900,770),
(1950,811),
(2000,760),
(2050,751),
(2100,770),
(2150,732),
(2200,741),
(2250,757),
(2300,730),
(2350,755),
(2400,788),
(2450,753),
(2500,740),
(2550,745),
(2600,740),
(2650,750),
(2700,739),
(2750,769),
(2800,766),
(2850,749),
(2900,762),
(2950,751),
(3000,752),
(3050,726),
(3100,747),
(3150,753),
(3200,761),
(3250,752),
(3300,755),
(3350,744),
(3400,744),
(3450,739),
(3500,743),
(3550,723),
(3600,702),
(3650,716),
(3700,738),
(3750,703),
(3800,704),
(3850,696),
(3900,675),
(3950,791),
(4000,665),
(4050,678),
(4100,659),
(4150,655),
(4200,648),
(4250,663),
(4300,635),
(4350,578),
(4400,610),
(4450,585),
(4500,576),
(4550,552),
(4600,530),
(4650,593),
(4700,547),
(4750,500),
(4800,492),
(4850,466),
(4900,465),
(4950,470),
(5000,458),
(5050,473),
(5100,434),
(5150,420),
(5200,416),
(5250,401),
(5300,394),
(5350,386),
(5400,389),
(5450,380),
(5500,378),
(5550,371),
(5600,357),
(5650,341),
(5700,351),
(5750,365),
(5800,341),
(5850,342),
(5900,303),
(5950,318),
(6000,308),
(6050,299),
(6100,283),
(6150,286),
(6200,276),
(6250,249),
(6300,261),
(6350,258),
(6400,253),
(6450,252),
(6500,253),
(6550,245),
(6600,251),
(6650,267),
(6700,251),
(6750,249),
(6800,262),
(6850,268),
(6900,236),
(6950,243),
(7000,227),
(7050,227),
(7100,220),
(7150,219),
(7200,257),
(7250,216),
(7300,211),
(7350,202),
(7400,201),
(7450,186),
(7500,194),
(7550,188),
(7600,188),
(7650,187),
(7700,180),
(7750,178),
(7800,188),
(7850,177),
(7900,173)
        };

        private async void MakeAtmosPcm()
        {
            var report = new GraphicalReport()
            {
                Logo = Logo
            };
            var graph1 = new Graph(report);
            graph1.YAxesInfo.Y1IsInverted = false;
            graph1.YAxesInfo.Y1MinimumValue = 0;
            graph1.YAxesInfo.Y1MaximumValue = 1100;
            graph1.LegendInfo.Name = "PCM Data";
            graph1.YAxesInfo.MinorGridlines.IsEnabled = false;
            graph1.YAxesInfo.MajorGridlines.Offset = 100;
            graph1.YAxesInfo.Y1LabelFormat = "F0";
            var on = new GraphSeries("", atmosPcmData)
            {
                LineColor = Colors.Blue
            };
            on.MaxDrawDistance = 100;
            graph1.Series.Add(on);
            graph1.YAxesInfo.Y1Title = "mA";
            graph1.DrawTopBorder = false;

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = "AtmosPCM_CPTP107214_081020"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);
            var chart1 = new Chart(report, "Survey Direction and Survey Date");
            chart1.LegendInfo.NameFontSize = 14f;

            var chart1Series = new SurveyDirectionWithDateSeries(new List<(double, bool, string)> { (50, false, "08/10/2020"), (7900, false, "08/10/2020") });
            chart1.Series.Add(chart1Series);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(graph1);
            splitContainer.AddSelfSizedContainer(chart1);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(0, 7900);
            var imageFiles = new List<StorageFile>();
            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{topGlobalXAxis.Title} Page {pageString}.png";
                if (pages.Count == 1)
                    fileName = $"{topGlobalXAxis.Title} Graph.png";
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"AtmosPCM_CPTP107214_081020\\{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
        }

        private async void ImportFilesOrder(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var changedFileNames = new List<string>();
            foreach (var excelFile in files)
            {
                if (excelFile.FileType.ToLower() == ".xlsx")
                {
                    var finalFileName = excelFile.Name.Replace("Files Order.xlsx", "").Trim();
                    var fileNames = new List<string>();
                    var maxOffset = 0;
                    var length = 0;
                    string firstFile = null;
                    int firstStartIndex = -1;

                    using (var outStream = await excelFile.OpenStreamForWriteAsync())
                    using (var spreadDoc = SpreadsheetDocument.Open(outStream, false))
                    {
                        var workbookPart = spreadDoc.WorkbookPart;
                        var worksheetPart = workbookPart.WorksheetParts.First();
                        var worksheet = worksheetPart.Worksheet;
                        var sheetData = (SheetData)worksheet.FirstChild;
                        foreach (Row row in sheetData.ChildElements)
                        {
                            var nameCell = (Cell)row.FirstChild;
                            if (nameCell.InnerText == "File Name" || string.IsNullOrWhiteSpace(nameCell.InnerText))
                                continue;

                            fileNames.Add(nameCell.InnerText);

                            var offsetCell = (Cell)row.ChildElements[2];
                            var offset = (int)double.Parse(offsetCell.InnerText);
                            maxOffset = Math.Max(offset + 100, maxOffset);

                            var startIndexCell = (Cell)row.ChildElements[6];
                            var startIndex = (int)double.Parse(startIndexCell.InnerText);

                            var lengthCell = (Cell)row.ChildElements[10];
                            var curLength = (int)double.Parse(lengthCell.InnerText);
                            length += offset + curLength;

                            if (firstFile == null)
                            {
                                firstFile = nameCell.InnerText;
                                firstStartIndex = startIndex;
                            }
                        }
                    }

                    var addedFiles = new List<AllegroCISFile>();
                    var addedFileNames = new HashSet<string>();
                    foreach (var curFile in NewFiles)
                    {
                        if (!fileNames.Contains(curFile.Name) || !(curFile is AllegroCISFile))
                            continue;
                        var allegroFile = curFile as AllegroCISFile;
                        if (!addedFileNames.Contains(allegroFile.Name))
                        {
                            addedFiles.Add(allegroFile);
                            addedFileNames.Add(allegroFile.Name);
                        }
                        else
                        {
                            if (allegroFile.Extension == ".csv")
                            {
                                for (int i = 0; i < addedFiles.Count; ++i)
                                {
                                    if (addedFiles[i].Name == allegroFile.Name)
                                    {
                                        addedFiles.RemoveAt(i);
                                        addedFiles.Add(allegroFile);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (addedFiles.Count == 0)
                        continue;
                    var test = CombinedAllegroCISFile.CombineFiles(addedFiles.First().Header["segment"].Trim().ToUpper(), addedFiles, maxOffset);
                    if (test == null)
                        continue;
                    if (test.FileInfos.Info.File.Name != firstFile || test.FileInfos.Info.Start != firstStartIndex)
                        test.Reverse();
                    if (test.FileInfos.Count != fileNames.Count || test.FileInfos.TotalFootage != length || test.FileInfos.Info.File.Name != firstFile || test.FileInfos.Info.Start != firstStartIndex)
                    {
                        Debug.WriteLine($"Missing data in '{finalFileName}'");
                        changedFileNames.Add(finalFileName);
                    }
                    //test.FixContactSpikes();
                    await MakeGraphs(test, finalFileName);
                }
            }
        }

        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {
            //MakeQuickIITGraphs(CombinedAllegroCISFile file, List < (double, double, BasicGeoposition) > dcvgData, List < (double Footage, double Current, double Depth, string Comment, BasicGeoposition Gps) > pcmInput, string folderName)
            //var folderPicker = new FolderPicker();
            //folderPicker.FileTypeFilter.Add(".");
            //var folder = await folderPicker.PickSingleFolderAsync();
            //if (folder == null)
            //    return;
            //var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            //var pcm = files.Where(file => file.Name.Contains("PCM")).First();

            //var pcmInfo = new List<(double Footage, double Current, double Depth, string Comment)>();
            //using (var stream = await pcm.OpenStreamForReadAsync())
            //using (var reader = new StreamReader(stream))
            //{
            //    while (!reader.EndOfStream)
            //    {
            //        var line = reader.ReadLine();
            //        var split = line.Split('\t');
            //        var footage = double.Parse(split[2]);
            //        var current = double.Parse(split[0] == "" ? "0" : split[0]);
            //        var depth = double.Parse(split[1] == "" ? "0" : split[1]);
            //        var comment = split[3].Replace("\"", "");
            //        pcmInfo.Add((footage, current, depth, comment));
            //    }
            //}
            //var onOffFiles = files.Where(file => file.Name.Contains("CIS"));
            //var dcvgFile = files.Where(file => file.Name.Contains("DCVG")).First();
            //var onOffs = new List<AllegroCISFile>();
            //foreach (var file in onOffFiles)
            //{
            //    var onOffFactory = new FileFactory(file);
            //    var onOff = await onOffFactory.GetFile() as AllegroCISFile;
            //    onOffs.Add(onOff);
            //}

            //var dcvgFactory = new FileFactory(dcvgFile);

            //var onOffCombined = CombinedAllegroCISFile.CombineFiles("Test", onOffs);
            ////onOffCombined.Reverse();
            //var dcvg = await dcvgFactory.GetFile() as AllegroCISFile;
            //var dcvgData = dcvg.Points.Select(point => (point.Value.Footage, point.Value.IndicationValue, point.Value.GPS)).Where(val => !double.IsNaN(val.IndicationValue)).ToList();
            //await MakeQuickIITGraphs(onOffCombined, dcvgData, pcmInfo, "057A");

            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var soilRes = files.First();

            var soilResData = new Dictionary<int, List<(BasicGeoposition Gps, double Read, double Footage, string Comment)>>
            {
                { 60, new List<(BasicGeoposition Gps, double Read, double Footage, string Comment)>() },
                { 120, new List<(BasicGeoposition Gps, double Read, double Footage, string Comment)>() }
            };
            using (var stream = await soilRes.OpenStreamForReadAsync())
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var split = line.Split(',');
                    var lat = double.Parse(split[0]);
                    var lon = double.Parse(split[1]);
                    var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                    var spacing = int.Parse(split[2]);
                    var res = double.Parse(split[3]);
                    var footage = double.Parse(split[4]);
                    var comment = split[5].Replace("\"", "");
                    soilResData[spacing].Add((gps, res, footage, comment));
                }
            }
            await MakeDoubleSoilResGraphs(soilResData, folder);
        }

        private async Task MakeDoubleSoilResGraphs(Dictionary<int, List<(BasicGeoposition Gps, double Read, double Footage, string Comment)>> soilResData, StorageFolder folder)
        {
            var report = new GraphicalReport()
            {
                Logo = Logo
            };
            report.XAxisInfo.OverlapOpacity = 0f;
            var mainGraph = new Graph(report);

            var sixtyRes = new GraphSeries("60 in Spacing", soilResData[60].Select(res => (res.Footage, res.Read / 1000)).ToList())
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = true,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point
            };
            var oneTwentyRes = new GraphSeries("120 in Spacing", soilResData[120].Select(res => (res.Footage, res.Read / 1000)).ToList())
            {
                LineColor = Colors.Black,
                PointColor = Colors.Green,
                IsY1Axis = true,
                PointShape = GraphSeries.Shape.Triangle,
                GraphType = GraphSeries.Type.Point
            };
            var commentSeries = new CommentSeries { Values = soilResData[60].Select(res => (res.Footage, res.Comment)).ToList(), PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs };

            mainGraph.Series.Add(sixtyRes);
            mainGraph.Series.Add(oneTwentyRes);
            mainGraph.DrawTopBorder = false;
            mainGraph.CommentSeries = commentSeries;
            mainGraph.YAxesInfo.Y2IsDrawn = false;
            mainGraph.YAxesInfo.Y1IsInverted = false;
            mainGraph.YAxesInfo.Y1MinimumValue = 0;
            mainGraph.YAxesInfo.Y1MaximumValue = 50;
            mainGraph.YAxesInfo.Y1LabelFormat = "F0";
            mainGraph.YAxesInfo.Y1LabelSuffix = "K";
            mainGraph.YAxesInfo.MinorGridlines.Offset = 5;
            mainGraph.YAxesInfo.MajorGridlines.Offset = 10;
            mainGraph.YAxesInfo.Y1Title = "Soil Resistivity (ohms-cm)";
            mainGraph.LegendInfo.Name = "Soil Res Data";

            report.XAxisInfo.IsEnabled = false;
            report.XAxisInfo.MajorGridline.Offset = 1000;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = "Tucson Water 42in Silverbell Soil Resistivity"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);
            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(mainGraph);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            report.PageSetup.UnitsPerPage = 10000;
            var pages = report.PageSetup.GetAllPages(0, soilResData[60].Last().Footage);
            var curFileName = $"{topGlobalXAxis.Title}";
            //var tabular = GetOnOffDepolPcmTabularData(allegroFile, depolFile, depolExceptions.PolarizationData, pcmReads);
            //await CreateExcelFile($"{curFileName} Combined Tabular Data", new List<(string Name, string Data)>() { ("Tabular Data", tabular) });

            var imageFiles = new List<StorageFile>();
            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{topGlobalXAxis.Title} Page {pageString}.png";
                if (pages.Count == 1)
                    fileName = $"{topGlobalXAxis.Title} Graph.png";
                var imageFile = await folder.CreateFileAsync($"{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
            var dialog = new MessageDialog($"Finished making {topGlobalXAxis.Title}");
            await dialog.ShowAsync();
        }

        private async void OnOffDepolButtonClick(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(CombineMaxGap.Text, out var maxGap))
                return;
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var onOffs = new List<AllegroCISFile>();
            var depols = new List<AllegroCISFile>();
            List<(BasicGeoposition Gps, double Signal, double Depth)> pcmReads = null;
            foreach (var file in files)
            {
                if (file.DisplayName == "PCM.csv")
                {
                    pcmReads = await GetPcmReadsFromCsv(file);
                    continue;
                }
                var factory = new FileFactory(file);
                var parsedFile = await factory.GetFile();
                if (!(parsedFile is AllegroCISFile))
                    continue;
                var allegroFile = parsedFile as AllegroCISFile;
                if (allegroFile.Type == FileType.OnOff)
                    onOffs.Add(allegroFile);
                else if (allegroFile.Type == FileType.Native)
                    depols.Add(allegroFile);
                else
                    continue;
            }
            onOffs = RemoveDuplicates(onOffs, ".csv");
            depols = RemoveDuplicates(depols, ".csv");
            var depolCombined = CombinedAllegroCISFile.CombineFiles("Depol", depols, maxGap);
            var onOffCombined = CombinedAllegroCISFile.CombineFiles("On Off", onOffs, maxGap);
            var onOffStartGps = onOffCombined.Points[0].Point.GPS;
            var depolStartGps = depolCombined.Points[0].Point.GPS;
            var depolEndGps = depolCombined.Points[depolCombined.Points.Count - 1].Point.GPS;
            var startDist = onOffStartGps.Distance(depolStartGps);
            var endDist = onOffStartGps.Distance(depolEndGps);
            if (endDist < startDist)
                depolCombined.Reverse();
            depolCombined.FixGps();
            onOffCombined.FixGps();
            depolCombined.AlignTo(onOffCombined);
            await CreateStandardExcel("Aligment Test\\Depol", depolCombined);
            await CreateStandardExcel("Aligment Test\\On Off", onOffCombined);
            var alignedPcmReads = AlignPcmReads(pcmReads, onOffCombined);
            alignedPcmReads.Sort((read1, read2) => read1.Footage.CompareTo(read2.Footage));
            for (int i = 0; i < alignedPcmReads.Count - 1; ++i)
            {
                var (curFootage, _, _) = alignedPcmReads[i];
                var (nextFootage, _, _) = alignedPcmReads[i + 1];
                if (curFootage == nextFootage)
                {
                    alignedPcmReads.RemoveAt(i + 1);
                    --i;
                }
            }
            await MakeOnOffDepolGraphs(onOffCombined, depolCombined, alignedPcmReads);
        }

        private async Task<List<(BasicGeoposition Gps, double Signal, double Depth)>> GetPcmReadsFromCsv(StorageFile file)
        {
            var output = new List<(BasicGeoposition Gps, double Signal, double Depth)>();

            var fileLines = await FileIO.ReadLinesAsync(file);

            foreach (var line in fileLines)
            {
                if (line.Contains("Latitude"))
                    continue;
                var lineSplit = line.Split(',');
                var lat = double.Parse(lineSplit[0]);
                var lon = double.Parse(lineSplit[1]);
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                var signal = double.Parse(lineSplit[2]);
                var depth = double.Parse(lineSplit[3]);
                output.Add((gps, signal, depth));
            }

            return output;
        }

        private List<(double Footage, double Signal, double Depth)> AlignPcmReads(List<(BasicGeoposition Gps, double Signal, double Depth)> pcm, CombinedAllegroCISFile file)
        {
            var output = new List<(double Footage, double Signal, double Depth)>();

            foreach (var (Gps, Signal, Depth) in pcm)
            {
                var (footage, distance) = file.GetClosestFootage(Gps);
                if (distance > 20)
                    continue;
                output.Add((footage, Signal, Depth));
            }

            return output;
        }

        private async Task MakeOnOffDepolGraphs(CombinedAllegroCISFile allegroFile, CombinedAllegroCISFile depolFile, List<(double Footage, double Signal, double Depth)> pcmReads = null, string exact = null)
        {
            var testStationInitial = allegroFile.GetTestStationData();
            var firstPoint = allegroFile.Points.First();
            var startComment = firstPoint.Footage + " -> " + firstPoint.Point.StrippedComment;
            var lastPoint = allegroFile.Points.Last();
            var endComment = lastPoint.Footage + " -> " + lastPoint.Point.StrippedComment;
            (string, bool)? response;
            if (exact == null)
                response = await InputTextDialogAsync($"PG&E LS {allegroFile.Name.Replace("ls", "", StringComparison.OrdinalIgnoreCase).Replace("line", "", StringComparison.OrdinalIgnoreCase).Trim()} MP START to MP END", testStationInitial, startComment, endComment);
            else
                response = (exact, false);//await InputTextDialogAsync(exact, testStationInitial, startComment, endComment);

            if (response == null)
                return;
            if (response.Value.Item2)
            {
                allegroFile.Reverse();
            }
            var report = new GraphicalReport()
            {
                Logo = Logo
            };
            var mainGraph = new Graph(report);

            var on = new GraphSeries("On", allegroFile.GetDoubleData("On"))
            {
                LineColor = Colors.Blue
            };
            var off = new GraphSeries("Off", allegroFile.GetDoubleData("Off"))
            {
                LineColor = Colors.Green
            };
            var depol = new GraphSeries("Depol", depolFile.GetDoubleData("On"))
            {
                LineColor = Colors.Chartreuse,
                MaxDrawDistance = 15
            };
            var redLine = new SingleValueGraphSeries("850mV Line", -0.85)
            {
                IsDrawnInLegend = false
            };
            var polarizationLine = new SingleValueGraphSeries("100mV Line", -0.1)
            {
                IsDrawnInLegend = false
            };
            var depth = new GraphSeries("Depth", pcmReads.Select(pcm => (pcm.Footage, pcm.Depth)).ToList())
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = false,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point
            };
            var commentSeries = new CommentSeries { Values = allegroFile.GetCommentData(new List<string>() { "No Whisker", "Whisker" }), PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs };

            mainGraph.Series.Add(on);
            mainGraph.Series.Add(off);
            mainGraph.Series.Add(depol);
            mainGraph.Series.Add(depth);
            mainGraph.Series.Add(redLine);
            mainGraph.Series.Add(polarizationLine);
            mainGraph.DrawTopBorder = false;
            mainGraph.CommentSeries = commentSeries;
            mainGraph.YAxesInfo.Y2IsDrawn = true;
            mainGraph.YAxesInfo.Y2Title = "Depth (inches)";
            mainGraph.YAxesInfo.Y2MaximumValue = 150;
            mainGraph.YAxesInfo.Y2MinimumValue = 0;

            var pcmSignalGraph = new Graph(report);
            pcmSignalGraph.LegendInfo.Name = "PCM Data";
            pcmSignalGraph.YAxesInfo.MajorGridlines.Offset = 1;
            pcmSignalGraph.YAxesInfo.MinorGridlines.Offset = 0.5;
            pcmSignalGraph.YAxesInfo.Y2IsDrawn = false;
            pcmSignalGraph.YAxesInfo.Y1Title = "Amps";
            pcmSignalGraph.YAxesInfo.Y1MaximumValue = 3.5;
            pcmSignalGraph.YAxesInfo.Y1MinimumValue = 0;
            pcmSignalGraph.YAxesInfo.Y1IsInverted = false;
            var pcmSeries = new GraphSeries("PCM", pcmReads.Select(pcm => (pcm.Footage, pcm.Signal)).ToList())
            {
                LineColor = Colors.Black,
                PointColor = Colors.Navy,
                PointShape = GraphSeries.Shape.Square,
                GraphType = GraphSeries.Type.Point,
                IsDrawnInLegend = false
            };
            pcmSignalGraph.Series.Add(pcmSeries);
            var pcmContainer = new SplitContainerMeasurement(pcmSignalGraph) { FixedInchSize = 1 };

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = response.Value.Item1
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            var cisDirectionChart = new Chart(report, "CIS Survey Direction and Date");
            cisDirectionChart.LegendInfo.NameFontSize = 14f;
            var cisDirectionSeries = new SurveyDirectionWithDateSeries(allegroFile.GetDirectionWithDateData());
            cisDirectionChart.Series.Add(cisDirectionSeries);

            var depolDirectionChart = new Chart(report, "Depol Survey Direction and Date");
            depolDirectionChart.LegendInfo.NameFontSize = 14f;
            var depolDirectionSeries = new SurveyDirectionWithDateSeries(depolFile.GetDirectionWithDateData());
            depolDirectionChart.Series.Add(depolDirectionSeries);

            var chart2 = new Chart(report, "850mV Data");
            chart2.LegendInfo.SeriesNameFontSize = 8f;
            chart2.LegendInfo.NameFontSize = 15f;
            ExceptionsChartSeries cisExceptions = new OnOff850ExceptionChartSeries(allegroFile.GetCombinedData(), chart2.LegendInfo, chart2.YAxesInfo)
            {
                LegendLabelSplit = 0.5f
            };
            chart2.Series.Add(cisExceptions);

            var chart3 = new Chart(report, "100mV Data");
            chart3.LegendInfo.SeriesNameFontSize = 8f;
            chart3.LegendInfo.NameFontSize = 15f;
            PolarizationChartSeries depolExceptions = new PolarizationChartSeries(allegroFile.GetCombinedData(), depolFile.GetCombinedData(), chart3.LegendInfo, chart3.YAxesInfo)
            {
                LegendLabelSplit = 0.5f
            };
            chart3.Series.Add(depolExceptions);

            var polarization = new GraphSeries("Polarization", depolExceptions.PolarizationData)
            {
                LineColor = Colors.Orchid
            };
            mainGraph.Series.Add(polarization);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(mainGraph);
            splitContainer.AddContainer(pcmContainer);
            splitContainer.AddSelfSizedContainer(chart2);
            splitContainer.AddSelfSizedContainer(chart3);
            splitContainer.AddSelfSizedContainer(cisDirectionChart);
            splitContainer.AddSelfSizedContainer(depolDirectionChart);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(0, allegroFile.Points.Last().Footage);
            var curFileName = $"{response.Value.Item1}\\{topGlobalXAxis.Title}";
            await CreateStandardExcel(curFileName + " CIS", allegroFile);
            await CreateStandardExcel(curFileName + " Depol", depolFile);
            var tabular = GetOnOffDepolPcmTabularData(allegroFile, depolFile, depolExceptions.PolarizationData, pcmReads);
            await CreateExcelFile($"{curFileName} Combined Tabular Data", new List<(string Name, string Data)>() { ("Tabular Data", tabular) });

            var imageFiles = new List<StorageFile>();
            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{topGlobalXAxis.Title} Page {pageString}.png";
                if (pages.Count == 1)
                    fileName = $"{topGlobalXAxis.Title} Graph.png";
                var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{response.Value.Item1}\\{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
            var dialog = new MessageDialog($"Finished making {topGlobalXAxis.Title}");
            //await dialog.ShowAsync();
        }

        private string GetOnOffDepolPcmTabularData(CombinedAllegroCISFile onOffFile, CombinedAllegroCISFile depolFile, List<(double Footage, double Polarization)> polarizationList, List<(double Footage, double Signal, double Depth)> pcmReads, int voltReadDecimals = 3, int gpsReadDecimals = 7, int pcmReadDecimals = 3)
        {
            polarizationList.Sort((pol1, pol2) => pol1.Footage.CompareTo(pol2.Footage));
            for (int i = 0; i < polarizationList.Count - 1; ++i)
            {
                if (polarizationList[i].Footage == polarizationList[i + 1].Footage)
                {
                    polarizationList.RemoveAt(i + 1);
                    --i;
                }
            }
            var polDictionary = polarizationList.ToDictionary(x => x.Footage, x => x.Polarization);
            Dictionary<double, (double Signal, double Depth)> pcmDictionary = pcmReads.ToDictionary(x => x.Footage, x => (x.Signal, x.Depth));
            var output = new StringBuilder();
            output.AppendLine("Footage\tOn\tOff\tDepol\tPolarization\tDepth\tPCM\tOn Off Date\tDepol Date\tLatitude\tLongitude\tRemarks");
            foreach (var curOnOffPoint in onOffFile.Points)
            {
                var curFootage = curOnOffPoint.Footage;
                var on = curOnOffPoint.Point.On.ToString("F" + voltReadDecimals);
                var offValue = curOnOffPoint.Point.Off;
                var off = offValue.ToString("F" + voltReadDecimals);
                var onOffDate = curOnOffPoint.Point.Times.First().ToShortDateString();
                var gps = curOnOffPoint.Point.GPS;
                var lat = gps.Latitude.ToString("F" + gpsReadDecimals);
                var lon = gps.Longitude.ToString("F" + gpsReadDecimals);
                var depol = "N/A";
                var pol = "N/A";
                var depolDate = "N/A";
                if (polDictionary.ContainsKey(curFootage))
                {
                    var depolPoint = depolFile.GetClosesetPoint(curFootage);
                    depolDate = depolPoint.Point.Times.First().ToShortDateString();
                    var polValue = polDictionary[curFootage];
                    var depolValue = offValue - polValue;
                    depol = depolValue.ToString("F" + voltReadDecimals);
                    pol = polValue.ToString("F" + voltReadDecimals);
                }

                var depth = "";
                var pcm = "";
                if (pcmDictionary.ContainsKey(curFootage))
                {
                    var (signalValue, depthValue) = pcmDictionary[curFootage];
                    depth = depthValue.ToString("F0");
                    pcm = signalValue.ToString("F" + pcmReadDecimals);
                }
                output.AppendLine($"{curFootage:F0}\t{on}\t{off}\t{depol}\t{pol}\t{depth}\t{pcm}\t{onOffDate}\t{depolDate}\t{lat}\t{lon}\t{curOnOffPoint.Point.OriginalComment}");
            }
            return output.ToString().TrimEnd('\n').TrimEnd('\r');
        }

        private List<AllegroCISFile> RemoveDuplicates(List<AllegroCISFile> files, string priority = null)
        {
            var output = new List<AllegroCISFile>();
            var found = new Dictionary<string, List<AllegroCISFile>>();

            foreach (var file in files)
            {
                var name = file.Name;
                var ext = file.Extension;

                if (found.ContainsKey(name))
                {
                    var others = found[name];

                    if (ext == priority)
                    {
                        found[name].Add(file);
                    }
                    else
                    {
                        foreach (var other in others)
                        {
                            if (!other.IsEquivalent(file))
                            {
                                found[name].Add(file);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    found.Add(name, new List<AllegroCISFile>() { file });
                }
            }

            foreach (var (_, foundFiles) in found)
            {
                if (foundFiles.Count == 1 || priority == null)
                {
                    output.Add(foundFiles.First());
                }
                else
                {
                    var prioFiles = foundFiles.Where(f => f.Extension == priority).ToList();
                    if (prioFiles.Count == 0)
                    {
                        output.Add(foundFiles.First());
                    }
                    else
                    {
                        output.Add(prioFiles.First());
                    }
                }
            }

            return output;
        }
    }
}
