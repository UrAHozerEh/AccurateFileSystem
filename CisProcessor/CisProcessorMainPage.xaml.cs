using AccurateFileSystem;
using AccurateReportSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Colors = Windows.UI.Colors;
using Color = Windows.UI.Color;
using Page = Windows.UI.Xaml.Controls.Page;
using Windows.Storage;
using Microsoft.Graphics.Canvas;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Windows.UI.Popups;
using Windows.Storage.Pickers;
using AccurateFileSystem.EsriShapefile;
using AccurateFileSystem.Kmz;
using Windows.Devices.Geolocation;
using GraphSeries = AccurateReportSystem.GraphSeries;
using DocumentFormat.OpenXml.Drawing.Charts;
using Chart = AccurateReportSystem.Chart;
using Orientation = Windows.UI.Xaml.Controls.Orientation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CisProcessor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const double MaxDepth = 180;
        private const string _client = "Sempra";

        public MainPage()
        {
            this.InitializeComponent();
        }

        private static async Task<(string, bool)?> InputTextDialogAsync(string title, string testStationData, string firstComment, string lastComment)
        {
            var panel = new StackPanel()
            {
                Orientation = Orientation.Vertical
            };
            var mpRegex = new Regex("mp\\s?(\\d+\\.\\d+)");
            var startMpMatch = mpRegex.Match(firstComment);
            var endMpMatch = mpRegex.Match(lastComment);

            var startMpString = "START";
            var startMp = 0.0;
            var startHasMp = false;

            var endMpString = "END";
            var endMp = 0.0;
            var endHasMp = false;

            var isReversed = false;

            if (startMpMatch.Success)
            {
                startMpString = startMpMatch.Groups[1].Value;
                startHasMp = double.TryParse(startMpString, out startMp);
            }
            if (endMpMatch.Success)
            {
                endMpString = endMpMatch.Groups[1].Value;
                endHasMp = double.TryParse(endMpString, out endMp);
            }
            if (startHasMp && endHasMp)
            {
                if (endMp < startMp)
                {
                    isReversed = true;

                }
            }
            else if (endHasMp && endMp == 0)
            {
                isReversed = true;
            }

            if (isReversed)
            {
                (endMpString, startMpString) = (startMpString, endMpString);
            }

            title = title.Replace("START", startMpString);
            title = title.Replace("END", endMpString);

            var inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32,
                Text = title
            };

            var lineSplit = testStationData.Split('\n');
            var testStationList = new ListBox();
            if (testStationList.Items != null)
            {
                testStationList.Items.Add(new ListBoxItem { Content = firstComment });

                for (var i = 1; i < lineSplit.Length; ++i)
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
            }

            var isReverse = new CheckBox
            {
                Content = "Is Reverse?",
                IsChecked = isReversed
            };

            panel.Children.Add(testStationList);
            panel.Children.Add(inputTextBox);
            panel.Children.Add(isReverse);
            var dialog = new ContentDialog
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

        private async void Do5716ButtonClick(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var masterFolder = await folderPicker.PickSingleFolderAsync();
            if (masterFolder == null)
                return;
            var folders = await masterFolder.GetFoldersAsync();
            var outputFolder = await masterFolder.CreateFolderAsync("0000 Processed Data", CreationCollisionOption.OpenIfExists);
            var fileOrder = await masterFolder.CreateFolderAsync("0000 Files Orders", CreationCollisionOption.OpenIfExists);
            var finishedFileNames = await GetFilesNames(fileOrder);
            folders = folders.OrderBy(folder => folder.DisplayName).ToList().AsReadOnly();
            foreach (var folder in folders)
            {
                if (folder.Name.Equals(outputFolder.Name) || folder.Name.Equals(fileOrder.Name))
                    continue;
                await ParseLineFolder(folder, outputFolder, fileOrder);
            }
        }

        private static async Task<List<CsvPcm>> GetPcm(IStorageFolder folder)
        {
            var output = new List<CsvPcm>();
            if (folder == null)
                return output;
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                var fileFactory = new FileFactory(file);
                var curFile = await fileFactory.GetFile();
                if (curFile is OtherPcm curPCm)
                    output.Add(curPCm);
            }
            return output;
        }

        private static async Task<List<GeneralCsv>> GetDcvg(IStorageFolder folder)
        {
            var output = new List<GeneralCsv>();
            if (folder == null)
                return output;
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                var fileFactory = new FileFactory(file);
                var curFile = await fileFactory.GetFile();
                if (curFile is GeneralCsv curCsv)
                    output.Add(curCsv);
            }
            return output;
        }

        private static async Task<List<AllegroCISFile>> GetCis(IStorageFolder folder)
        {
            var output = new List<AllegroCISFile>();
            if (folder == null)
                return output;
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                var fileFactory = new FileFactory(file);
                var curFile = await fileFactory.GetFile();
                if (curFile is AllegroCISFile curCis)
                    output.Add(curCis);
            }
            return output;
        }

        private async Task ParseLineFolder(StorageFolder folder, StorageFolder outputFolder, StorageFolder filesOrder)
        {

            var folders = await folder.GetFoldersAsync();
            var cisFolders = folders.Where(f => f.Name != "PCM" && f.Name != "DCVG");
            var pcmFolder = folders.FirstOrDefault(f => f.Name == "PCM");
            var dcvgFolder = folders.FirstOrDefault(f => f.Name == "DCVG");
            var pcm = await GetPcm(pcmFolder);
            var dcvg = await GetDcvg(dcvgFolder);
            var lineName = folder.DisplayName;
            foreach (var cisFolder in cisFolders)
            {
                await ParseCisFolder(lineName, cisFolder, dcvg, pcm, outputFolder);
            }
        }

        private async Task ParseCisFolder(string lineName, StorageFolder cisFolder, List<GeneralCsv> dcvgData, List<CsvPcm> pcmData, StorageFolder outputFolder)
        {
            var report = new GraphicalReport();
            var onOffGraph = new Graph(report)
            {
                YAxesInfo =
                {
                    Y1Title = "Pipe-to-Soil Potential (Volts)",
                    Y2IsDrawn = true,
                    Y2MaximumValue = MaxDepth,
                    Y2Title = "Depth (inches)"
                }
            };

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;
            var combinedName = $"{lineName} {cisFolder.DisplayName}";

            var curOutputFolder = await outputFolder.CreateFolderAsync(combinedName, CreationCollisionOption.ReplaceExisting);

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = $"{_client} {combinedName}"
            };

            var combined = CombinedAllegroCisFile.CombineFiles(combinedName, await GetCis(cisFolder));
            if (combined.HasStartSkip)
            {
                combined.ShiftPoints(-combined.Points[1].Footage);
            }

            foreach (var pcm in pcmData)
                combined.AddPcmDepthData(pcm);

            var pcmValues = combined.AlignAmpReads(pcmData);
            var dcvgValues = GetAlignedDcvgData(dcvgData, combined);

            await CreateStandardFiles(combinedName, combined, curOutputFolder);
            var addedTabularData = new List<(string Name, List<(double Footage, double Value)>)>();
            var pcmFootageData = pcmValues.Select((value) => (value.Footage, value.Value)).ToList();
            addedTabularData.Add(("PCM (Amps)", pcmFootageData));
            await CreateExcelFile(combinedName + " Tabular Data Extended", new List<(string Name, string Data)>() { ("Tabular Data", combined.GetTabularData(addedTabularData)) }, curOutputFolder);

            var on = new GraphSeries("On", combined.GetDoubleData("On"))
            {
                LineColor = Colors.Blue,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Blue,
                ShapeRadius = 2,
                MaxDrawDistance = 19
            };
            var off = new GraphSeries("Off", combined.GetDoubleData("Off"))
            {
                LineColor = Colors.Green,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Green,
                ShapeRadius = 2,
                MaxDrawDistance = 19
            };
            var depth = new GraphSeries("Depth", combined.GetDoubleData("Depth"))
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = false,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point,
                ShapeRadius = 4
            };
            var commentSeries = new CommentSeries
            {
                Values = combined.GetCommentData(),
                PercentOfGraph = 0.5f,
                IsFlippedVertical = false,
                BorderType = BorderType.Pegs,
                BackdropOpacity = 0.75f
            };
            onOffGraph.Series.Add(on);
            onOffGraph.Series.Add(off);
            onOffGraph.Series.Add(depth);
            onOffGraph.CommentSeries = commentSeries;
            onOffGraph.DrawTopBorder = false;

            if (dcvgValues != null && dcvgValues.Count != 0)
            {
                var dcvgIndication = new PointWithLabelGraphSeries("DCVG Indication", -0.2, dcvgValues.Select((value) => (value.Footage, "")).ToList())
                {
                    ShapeRadius = 3,
                    PointColor = Colors.Red,
                    BackdropOpacity = 1f
                };
                onOffGraph.Series.Add(dcvgIndication);
            }

            if (pcmValues.Count != 0)
            {
                var ampSeries = new PointWithLabelGraphSeries("PCM (mA)", -0.4, pcmValues.Select((value) => (value.Footage, value.Value.ToString("F0"))).ToList())
                {
                    ShapeRadius = 3,
                    PointColor = Colors.Navy,
                    BackdropOpacity = 1f,
                    PointShape = GraphSeries.Shape.Square
                };
                onOffGraph.Series.Add(ampSeries);
            }

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            var surveyDirectionChart = new Chart(report, "Survey Direction With Survey Date")
            {
                LegendInfo =
                {
                    NameFontSize = 14f
                }
            };
            var surveyDirectionSeries = new SurveyDirectionWithDateSeries(combined.GetDirectionWithDateData());
            surveyDirectionChart.Series.Add(surveyDirectionSeries);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);
            splitContainer.AddSelfSizedContainer(surveyDirectionChart);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var surveyLength = combined.Points.Last().Footage;
            var pages = report.PageSetup.GetAllPages(0, surveyLength);

            for (var i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var image = report.GetImage(page);
                var imageFileName = $"{combinedName} Page {pageString}.png";
                if (pages.Count == 1)
                    imageFileName = $"{combinedName} Graph.png";
                var imageFile = await curOutputFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
            }
        }

        private static List<(double Footage, string Comment)> GetAlignedDcvgData(List<GeneralCsv> dcvgFiles, CombinedAllegroCisFile cisCombinedData)
        {
            var output = new List<(double Footage, string Comment)>();
            foreach (var dcvgFile in dcvgFiles)
            {
                var lonCol = dcvgFile.GetColumn("Longitude");
                var latCol = dcvgFile.GetColumn("Latitude");
                var commentCol = dcvgFile.GetColumn("Comments");
                for (var row = 0; row < dcvgFile.Data.GetLength(0); ++row)
                {
                    var lat = double.Parse(dcvgFile.Data[row, latCol]);
                    var lon = double.Parse(dcvgFile.Data[row, lonCol]);
                    var comment = dcvgFile.Data[row, commentCol];
                    var curGps = new BasicGeoposition()
                    {
                        Latitude = lat,
                        Longitude = lon
                    };
                    var (footage, _) = cisCombinedData.GetClosestFootage(curGps);
                    output.Add((footage, comment));
                }
            }
            return output;
        }

        private async void DoWorkButtonClick(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var masterFolder = await folderPicker.PickSingleFolderAsync();
            if (masterFolder == null)
                return;
            var folders = await masterFolder.GetFoldersAsync();
            var outputFolder = await masterFolder.CreateFolderAsync("0000 Processed Data", CreationCollisionOption.OpenIfExists);
            var fileOrder = await masterFolder.CreateFolderAsync("0000 Files Orders", CreationCollisionOption.OpenIfExists);
            var finishedFileNames = await GetFilesNames(fileOrder);
            folders = folders.OrderBy(folder => folder.DisplayName).ToList().AsReadOnly();
            foreach (var folder in folders)
            {
                if (folder == outputFolder || folder == fileOrder)
                    continue;
                var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
                const int maxGap = 3600;
                var cisFiles = new List<AllegroCISFile>();
                var docFiles = new List<CsvPcm>();
                var fileNames = new HashSet<string>();

                foreach (var storageFile in files)
                {
                    var fileFactory = new FileFactory(storageFile);
                    var file = await fileFactory.GetFile();
                    if (file is CsvPcm docFile)
                    {
                        docFiles.Add(docFile);
                    }
                    if (!(file is AllegroCISFile allegroFile))
                        continue;
                    if (!fileNames.Contains(allegroFile.Name))
                    {
                        cisFiles.Add(allegroFile);
                        fileNames.Add(allegroFile.Name);
                    }
                    else
                    {
                        if (allegroFile.Extension != ".csv") continue;
                        for (var i = 0; i < cisFiles.Count; ++i)
                        {
                            if (cisFiles[i].Name != allegroFile.Name) continue;
                            cisFiles.RemoveAt(i);
                            cisFiles.Add(allegroFile);
                            break;
                        }
                    }
                }

                if (cisFiles.Count == 0) continue;

                cisFiles.Sort((file1, file2) => string.Compare(file1.Name, file2.Name, StringComparison.Ordinal));

                var onOffFiles = new List<AllegroCISFile>();
                var staticFiles = new List<AllegroCISFile>();

                foreach (var file in cisFiles)
                {
                    if (file.IsOnOff)
                    {
                        onOffFiles.Add(file);
                    }
                    else
                    {
                        staticFiles.Add(file);
                    }
                }
                var type = $" On Off {(staticFiles.Count == 0 ? "" : "And Static ")}CIS";
                var combinedOnOffFiles = CombinedAllegroCisFile.CombineOrderedFiles(folder.DisplayName + type, onOffFiles, 5);
                var combinedStaticFiles = CombinedAllegroCisFile.CombineOrderedFiles(folder.DisplayName + " Static CIS", staticFiles, 5);
                combinedStaticFiles?.AddPcmDepthData(docFiles);
                combinedStaticFiles?.AddMaxDepthComment(MaxDepth);
                combinedOnOffFiles?.AddPcmDepthData(docFiles);
                combinedOnOffFiles?.AddMaxDepthComment(MaxDepth);
                var pcmReads = new List<(double Footage, double Read)>();
                if (docFiles.Count != 0)
                {
                    foreach (var docFile in docFiles)
                    {

                        foreach (var (gps, read, _) in docFile.AmpData)
                        {
                            if (read == 0) continue;
                            var (footage, dist) = combinedOnOffFiles.GetClosestFootage(gps);
                            pcmReads.Add((footage, read));
                        }
                    }
                }
                combinedOnOffFiles.FixContactSpikes();
                combinedOnOffFiles.FixGps();
                combinedOnOffFiles.StraightenGps();
                //combinedOnOffFiles.SetFootageFromGps();
                combinedOnOffFiles.RemoveComments("+");
                if (combinedOnOffFiles.HasStartSkip)
                {
                    combinedOnOffFiles.ShiftPoints(-combinedOnOffFiles.Points[1].Footage);
                }
                if (staticFiles.Count > 0 && onOffFiles.Count > 0)
                {
                    var onOffStart = combinedOnOffFiles.Points.First().Point.GPS;
                    var onOffEnd = combinedOnOffFiles.Points.Last().Point.GPS;
                    var staticStart = combinedStaticFiles.Points.First().Point.GPS;
                    var staticEnd = combinedStaticFiles.Points.Last().Point.GPS;
                    if (staticStart.Distance(onOffEnd) < staticStart.Distance(onOffStart))
                    {
                        combinedStaticFiles.Reverse();
                    }
                    combinedStaticFiles.AlignTo(combinedOnOffFiles);
                }
                var finishedFinalName = finishedFileNames.GetValueOrDefault(folder.DisplayName, null);
                if (combinedStaticFiles != null && combinedOnOffFiles != null)
                {
                    var (text, isReversed) = await MakeOnOffStaticGraphs(combinedOnOffFiles, combinedStaticFiles, outputFolder, pcmReads, finishedFinalName);
                    await CreateExcelFile($"{folder.DisplayName}+{text}+{(isReversed ? "T" : "F")}", new List<(string Name, string Data)>() { ("Order", combinedOnOffFiles.FileInfos.GetExcelData(0)) }, fileOrder);
                }
                else
                {
                    var file = combinedOnOffFiles ?? combinedStaticFiles;
                    var (text, isReversed) = await MakeOnOffGraphs(file, pcmReads, outputFolder, finishedFinalName);
                    await CreateExcelFile($"{folder.DisplayName}+{text}+{(isReversed ? "T" : "F")}", new List<(string Name, string Data)>() { ("Order", file.FileInfos.GetExcelData(0)) }, fileOrder);
                }
            }
        }

        private async Task<Dictionary<string, (string, bool)?>> GetFilesNames(StorageFolder folder)
        {
            var output = new Dictionary<string, (string, bool)?>();
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                if (!file.FileType.Contains("xlsx", StringComparison.OrdinalIgnoreCase))
                    continue;
                var curName = file.DisplayName;
                var curNameSplit = curName.Split('+');
                var isReverse = curNameSplit[2] == "T";
                output.Add(curNameSplit[0], (curNameSplit[1], isReverse));
            }
            return output;
        }

        private async Task<(string Text, bool IsReversed)> MakeOnOffStaticGraphs(CombinedAllegroCisFile onOffFile, CombinedAllegroCisFile staticFile, StorageFolder outputFolder, List<(double Footage, double Read)> pcmReads, (string Text, bool IsReversed)? exact = null)
        {
            var testStationInitial = onOffFile.GetTestStationData();
            var firstPoint = onOffFile.Points.First();
            var startComment = firstPoint.Footage + " -> " + firstPoint.Point.StrippedComment;
            var lastPoint = onOffFile.Points.Last();
            var endComment = lastPoint.Footage + " -> " + lastPoint.Point.StrippedComment;
            (string Text, bool IsReversed)? response;
            if (!exact.HasValue)
                response = await InputTextDialogAsync($"{_client} {onOffFile.Name.Trim()} MP START to MP END", testStationInitial, startComment, endComment);
            else
                response = (exact.Value.Text, exact.Value.IsReversed);//await InputTextDialogAsync(exact, testStationInitial, startComment, endComment);

            if (response == null)
                return (null, false);
            if (response.Value.Item2)
            {
                onOffFile.Reverse();
            }
            var report = new GraphicalReport();
            var commentGraph = new Graph(report);
            var graph1 = new Graph(report);

            var graph2 = new Graph(report);
            var graph3 = new Graph(report);
            //var mirFilterData = "Start Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tReason\n" + ((MirFilter.IsChecked ?? false) ? allegroFile.FilterMir(new List<string>() { "anode", "rectifier" }) : "");

            var on = new GraphSeries("On", onOffFile.GetDoubleData("On"))
            {
                LineColor = Colors.Blue
            };
            var off = new GraphSeries("Off", onOffFile.GetDoubleData("Off"))
            {
                LineColor = Colors.Green
            };
            var onMir = new GraphSeries("On MIR Compensated", onOffFile.GetDoubleData("On Compensated"))
            {
                LineColor = Colors.Purple
            };
            var offMir = new GraphSeries("Off MIR Compensated", onOffFile.GetDoubleData("Off Compensated"))
            {
                LineColor = Color.FromArgb(255, 57, 255, 20)
            };
            var staticData = new GraphSeries("Static", staticFile.GetDoubleData("On"))
            {
                LineColor = Colors.Magenta,
                MaxDrawDistance = 25
            };
            var polarizationData = new GraphSeries("Polarization", offMir.Difference(staticData))
            {
                LineColor = Colors.Crimson,
                MaxDrawDistance = 25
            };
            var depth = new GraphSeries("Depth", onOffFile.GetDoubleData("Depth"))
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
            var commentSeries = new CommentSeries { Values = onOffFile.GetCommentData(), PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs };

            commentGraph.CommentSeries = commentSeries;
            commentGraph.LegendInfo.Name = "CIS Comments";
            commentGraph.DrawTopBorder = false;

            commentGraph.XAxisInfo.MajorGridline.IsEnabled = false;
            commentGraph.YAxesInfo.MinorGridlines.IsEnabled = false;
            commentGraph.YAxesInfo.MajorGridlines.IsEnabled = false;
            commentGraph.YAxesInfo.Y1IsDrawn = false;
            //if (onOffFile.Type == FileType.OnOff)
            //{
            //    graph1.Series.Add(depth);
            //    graph1.YAxesInfo.Y2IsDrawn = true;
            //} // DEPTH
            graph1.CommentSeries = commentSeries;
            /*
            graph1.YAxesInfo.Y1MaximumValue = 150;
            graph1.YAxesInfo.Y1MinimumValue = 0;
            graph1.YAxesInfo.Y1IsInverted = false;
            graph1.Gridlines[(int)GridlineName.MajorHorizontal].Offset = 15;
            graph1.Gridlines[(int)GridlineName.MinorHorizontal].Offset = 5;
            */
            if (onOffFile.Type != FileType.OnOff)
            {
                graph1.YAxesInfo.Y1MinimumValue = -0.75;
                graph1.YAxesInfo.MajorGridlines.Offset = 0.125;
                graph1.YAxesInfo.MinorGridlines.Offset = 0.025;
            }
            graph1.YAxesInfo.Y1MinimumValue = -4;
            graph1.YAxesInfo.MajorGridlines.Offset = 0.5;
            graph1.YAxesInfo.MinorGridlines.Offset = 0.1;
            graph1.YAxesInfo.Y2MaximumValue = MaxDepth;

            graph1.Series.Add(on);
            if (onOffFile.Type == FileType.OnOff)
            {
                graph1.Series.Add(off);
                graph1.Series.Add(onMir);
                graph1.Series.Add(offMir);
                graph1.Series.Add(staticData);
                graph1.Series.Add(polarizationData);
            }
            if (onOffFile.Type != FileType.Native)
            {
                graph1.Series.Add(redLine);
            }
            else
                on.Name = "Static";
            //graph1.XAxisInfo.IsEnabled = false;
            if (pcmReads.Count != 0)
            {
                var pcmSeriesLabels = pcmReads.Select(values => (values.Footage, -0.2, values.Read.ToString("F0"))).ToList();
                var pcmSeries = new PointWithLabelGraphSeries("PCM (Milliamps)", pcmSeriesLabels)
                {
                    PointColor = Colors.Navy
                };
                graph1.Series.Add(pcmSeries);
            }
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
                Title = response.Value.Text
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            //var graph1Measurement = new SplitContainerMeasurement(graph1)
            //{
            //    RequestedPercent = 0.5
            //};
            var chart1 = new Chart(report, "Survey Direction and Survey Date")
            {
                LegendInfo =
                {
                    NameFontSize = 14f
                }
            };
            var chart2 = new Chart(report, "850 Data")
            {
                LegendInfo =
                {
                    SeriesNameFontSize = 8f,
                    NameFontSize = 16f
                }
            };
            ExceptionsChartSeries exceptions = new OnOff850ExceptionChartSeries(onOffFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo)
            {
                LegendLabelSplit = 0.5f
            };
            chart2.Series.Add(exceptions);


            var chart3 = new Chart(report, "Polarization Data")
            {
                LegendInfo =
                {
                    SeriesNameFontSize = 8f,
                    NameFontSize = 12f
                }
            };
            ExceptionsChartSeries polExceptions = new PolarizationExceptionChartSeries(polarizationData.Values, chart3.LegendInfo, chart3.YAxesInfo)
            {
                LegendLabelSplit = 0.5f
            };
            chart3.Series.Add(polExceptions);

            var mirSeries = new MirDirection(onOffFile.GetReconnects());

            //if (IsSempra.IsChecked ?? false)
            //{
            //    chart2.LegendInfo.Name = "Exception Data";
            //    chart2.LegendInfo.NameFontSize = 14f;
            //    exceptions = new Sempra850ExceptionChartSeries(allegroFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo)
            //    {
            //        LegendLabelSplit = 0.5f
            //    };
            //    chart1.YAxesInfo.Y2IsDrawn = false;
            //}

            //chart1.LegendInfo.NameFontSize = 18f;

            var chart1Series = new SurveyDirectionWithDateSeries(onOffFile.GetDirectionWithDateData());
            chart1.Series.Add(chart1Series);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(graph1);
            if (onOffFile.Type != FileType.Native)
            {
                splitContainer.AddSelfSizedContainer(chart2);
                splitContainer.AddSelfSizedContainer(chart3);
            }
            splitContainer.AddSelfSizedContainer(chart1);
            //splitContainer.AddContainer(graph2);
            //splitContainer.AddContainer(graph3);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(0, onOffFile.Points.Last().Footage);
            var curFileName = $"{response.Value.Item1}\\{topGlobalXAxis.Title}";
            var addedPcmValues = new List<(string, List<(double, double)>)>
            {
                ("PCM Values", pcmReads),
                ("Polarization", polarizationData.Values)
            };
            await CreateStandardFiles(curFileName, onOffFile, outputFolder, addedPcmValues);

            var shapefileFolder = await outputFolder.CreateFolderAsync("Shapefiles", CreationCollisionOption.OpenIfExists);
            var cisShapeFile = new ShapefileData($"{response.Value.Text}", onOffFile.GetShapeFile());
            await cisShapeFile.WriteToFolder(shapefileFolder);

            var passShapefileFolder = await outputFolder.CreateFolderAsync("Passing Shapefiles", CreationCollisionOption.OpenIfExists);
            var passingCisShapefileData = onOffFile.GetPassingShapeFile();
            if (!string.IsNullOrEmpty(passingCisShapefileData))
            {
                var passingCisShapeFile = new ShapefileData($"{response.Value.Text}", passingCisShapefileData);
                await passingCisShapeFile.WriteToFolder(passShapefileFolder);
            }

            var failingShapefileFolder = await outputFolder.CreateFolderAsync("Failing Shapefiles", CreationCollisionOption.OpenIfExists);
            var failingCisShapefileData = onOffFile.GetFailingShapeFile();
            if (!string.IsNullOrEmpty(failingCisShapefileData))
            {
                var failingCisShapeFile = new ShapefileData($"{response.Value.Text}", failingCisShapefileData);
                await failingCisShapeFile.WriteToFolder(failingShapefileFolder);
            }

            //if (MirFilter.IsChecked ?? false)
            //    await CreateExcelFile($"{curFileName} MIR Skips", new List<(string Name, string Data)>() { ("MIR Skips", mirFilterData) });
            var imageFiles = new List<StorageFile>();
            for (var i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{topGlobalXAxis.Title} Page {pageString}.png";
                if (pages.Count == 1)
                    fileName = $"{topGlobalXAxis.Title} Graph.png";
                var imageFile = await outputFolder.CreateFileAsync($"{response.Value.Item1}\\{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
            var dialog = new MessageDialog($"Finished making {topGlobalXAxis.Title}");
            return response.Value;
            //await dialog.ShowAsync();
        }

        private async Task<(string Text, bool IsReversed)> MakeOnOffGraphs(CombinedAllegroCisFile allegroFile, List<(double Footage, double Read)> pcmReads, StorageFolder outputFolder, (string Text, bool IsReversed)? exact = null)
        {
            var testStationInitial = allegroFile.GetTestStationData();
            var firstPoint = allegroFile.Points.First();
            var startComment = firstPoint.Footage + " -> " + firstPoint.Point.StrippedComment;
            var lastPoint = allegroFile.Points.Last();
            var endComment = lastPoint.Footage + " -> " + lastPoint.Point.StrippedComment;
            (string Text, bool IsReversed)? response;
            if (!exact.HasValue)
                response = await InputTextDialogAsync($"{_client} LS {allegroFile.Name.Replace("ls", "", StringComparison.OrdinalIgnoreCase).Replace("line", "", StringComparison.OrdinalIgnoreCase).Trim()} MP START to MP END", testStationInitial, startComment, endComment);
            else
                response = (exact.Value.Text, exact.Value.IsReversed);//await InputTextDialogAsync(exact, testStationInitial, startComment, endComment);

            if (response == null)
                return (null, false);
            if (response.Value.Item2)
            {
                allegroFile.Reverse();
            }
            var report = new GraphicalReport();
            var commentGraph = new Graph(report);
            var graph1 = new Graph(report);
            var graph2 = new Graph(report);
            var graph3 = new Graph(report);
            //var mirFilterData = "Start Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tReason\n" + ((MirFilter.IsChecked ?? false) ? allegroFile.FilterMir(new List<string>() { "anode", "rectifier" }) : "");

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
            var separateComment = false;
            if (separateComment)
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
            if (!separateComment)
                graph1.CommentSeries = commentSeries;

            if (allegroFile.Type != FileType.OnOff)
            {
                graph1.YAxesInfo.Y1MinimumValue = -0.75;
                graph1.YAxesInfo.MajorGridlines.Offset = 0.125;
                graph1.YAxesInfo.MinorGridlines.Offset = 0.025;
            }
            graph1.YAxesInfo.Y2MaximumValue = MaxDepth;
            var y1Min = -3;
            var cisValueMin = Math.Min(on.Values.Select(v => v.value).Min(), off.Values.Select(v => v.value).Min());
            var y1ActualMin = ((int)(cisValueMin / 0.5) - 1) * 0.5;
            graph1.YAxesInfo.Y1MinimumValue = Math.Min(y1Min, y1ActualMin);
            graph1.YAxesInfo.MajorGridlines.Offset = 0.5;
            graph1.YAxesInfo.MinorGridlines.Offset = 0.1;

            graph1.Series.Add(on);
            if (allegroFile.Type == FileType.OnOff)
            {
                graph1.Series.Add(off);
                graph1.Series.Add(onMir);
                graph1.Series.Add(offMir);
            }
            if (allegroFile.Type != FileType.Native)
            {
                graph1.Series.Add(redLine);
            }
            else
                on.Name = "Static";
            //graph1.XAxisInfo.IsEnabled = false;
            graph1.DrawTopBorder = false;
            if (pcmReads.Count != 0)
            {
                var pcmSeriesLabels = pcmReads.Select(values => (values.Footage, -0.2, values.Read.ToString("F0"))).ToList();
                var pcmSeries = new PointWithLabelGraphSeries("PCM (Milliamps)", pcmSeriesLabels)
                {
                    PointColor = Colors.Navy
                };
                graph1.Series.Add(pcmSeries);
            }

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
                Title = response.Value.Text
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
            //if (IsSempra.IsChecked ?? false)
            //{
            //    chart2.LegendInfo.Name = "Exception Data";
            //    chart2.LegendInfo.NameFontSize = 14f;
            //    exceptions = new Sempra850ExceptionChartSeries(allegroFile.GetCombinedMirData(), chart2.LegendInfo, chart2.YAxesInfo)
            //    {
            //        LegendLabelSplit = 0.5f
            //    };
            //    chart1.YAxesInfo.Y2IsDrawn = false;
            //}
            chart2.Series.Add(exceptions);
            //chart1.LegendInfo.NameFontSize = 18f;

            var chart1Series = new SurveyDirectionWithDateSeries(allegroFile.GetDirectionWithDateData());
            chart1.Series.Add(chart1Series);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            if (separateComment)
            {
                var commentGraphMeasurement = new SplitContainerMeasurement(commentGraph)
                {
                    FixedInchSize = 1f
                };
                splitContainer.AddContainer(commentGraphMeasurement);
            }
            splitContainer.AddContainer(graph1);
            if (allegroFile.Type != FileType.Native)
            {
                splitContainer.AddSelfSizedContainer(chart2);//On
            }
            splitContainer.AddSelfSizedContainer(chart1);
            //splitContainer.AddContainer(graph3);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var pages = report.PageSetup.GetAllPages(0, allegroFile.Points.Last().Footage);
            var curFileName = $"{response.Value.Item1}\\{topGlobalXAxis.Title}";
            var addedPcmValues = new List<(string, List<(double, double)>)>
            {
                ("PCM Values", pcmReads)
            };
            await CreateStandardFiles(curFileName, allegroFile, outputFolder, addedPcmValues);

            var shapefileFolder = await outputFolder.CreateFolderAsync("Shapefiles", CreationCollisionOption.OpenIfExists);
            var cisShapeFile = new ShapefileData($"{response.Value.Text}", allegroFile.GetShapeFile(pcmValues: pcmReads));
            await cisShapeFile.WriteToFolder(shapefileFolder);

            var passShapefileFolder = await outputFolder.CreateFolderAsync("Passing Shapefiles", CreationCollisionOption.OpenIfExists);
            var passingCisShapefileData = allegroFile.GetPassingShapeFile();
            if (!string.IsNullOrEmpty(passingCisShapefileData))
            {
                var passingCisShapeFile = new ShapefileData($"{response.Value.Text}", passingCisShapefileData);
                await passingCisShapeFile.WriteToFolder(passShapefileFolder);
            }

            var failingShapefileFolder = await outputFolder.CreateFolderAsync("Failing Shapefiles", CreationCollisionOption.OpenIfExists);
            var failingCisShapefileData = allegroFile.GetFailingShapeFile();
            if (!string.IsNullOrEmpty(failingCisShapefileData))
            {
                var failingCisShapeFile = new ShapefileData($"{response.Value.Text}", failingCisShapefileData);
                await failingCisShapeFile.WriteToFolder(failingShapefileFolder);
            }

            //if (MirFilter.IsChecked ?? false)
            //    await CreateExcelFile($"{curFileName} MIR Skips", new List<(string Name, string Data)>() { ("MIR Skips", mirFilterData) });
            var imageFiles = new List<StorageFile>();
            for (var i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{topGlobalXAxis.Title} Page {pageString}.png";
                if (pages.Count == 1)
                    fileName = $"{topGlobalXAxis.Title} Graph.png";
                var imageFile = await outputFolder.CreateFileAsync($"{response.Value.Item1}\\{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
            var dialog = new MessageDialog($"Finished making {topGlobalXAxis.Title}");
            return response.Value;
            //await dialog.ShowAsync();
        }

        private (List<(double Footage, string Value)> Values, List<(double Footage, string Value)> Dates) AlignDepolTabular(CombinedAllegroCisFile cisFile, CombinedAllegroCisFile depolFile)
        {
            var depolValues = new List<(double Footage, string Value)>();
            var depolDates = new List<(double Footage, string Value)>();
            foreach(var cisPointData in cisFile.Points)
            {
                var depolPointData = depolFile.GetClosesetPoint(cisPointData.Footage);
                depolValues.Add((cisPointData.Footage, depolPointData.Point.On.ToString("F4"));
                depolDates.Add((cisPointData.Footage, depolPointData.Point.OnTime.Value.ToShortDateString()));
            }

            return (depolValues, depolDates);
        }

        private async Task CreateStandardFiles(string fileName, CombinedAllegroCisFile allegroFile, StorageFolder outputFolder, List<(string, List<(double, double)>)> addedValues = null, CombinedAllegroCisFile depolFile = null)
        {
            var tabular = allegroFile.GetTabularData(addedValues: addedValues);
            if (depolFile != null)
            {
                var (depolVals, depolDates) = AlignDepolTabular(allegroFile, depolFile);
                var stringAddedValues = new List<(string Name, List<(double Footage, string Value)>)>();
                foreach (var (name, values) in addedValues)
                {
                    var curValues = new List<(double Footage, string Value)>();
                    foreach (var (foot, val) in values)
                    {
                        curValues.Add((foot, val.ToString($"F4")));
                    }
                    stringAddedValues.Add((name, curValues));
                }
                stringAddedValues.Add(("Depol (V)", depolVals));
                stringAddedValues.Add(("Depol Dates", depolDates));
                tabular = allegroFile.GetTabularData(addedValues: stringAddedValues);
            }
            await CreateExcelFile($"{fileName} Tabular Data", new List<(string Name, string Data)>() { ("Tabular Data", tabular) }, outputFolder);
            var dataMetrics = new DataMetrics(allegroFile.GetPoints());
            await CreateExcelFile($"{fileName} Data Metrics", dataMetrics.GetSheets(), outputFolder);
            var testStation = allegroFile.GetTestStationData();
            if (depolFile != null)
            {
                var depolTestStation = depolFile.GetTestStationData();
                await CreateExcelFile($"{fileName} Test Station Data", new List<(string Name, string Data)>() {
                    ("Test Station Data", testStation),
                    ("Depol Test Station Data", depolTestStation) 
                }, outputFolder);
            }
            else
            {
                await CreateExcelFile($"{fileName} Test Station Data", new List<(string Name, string Data)>() {
                    ("Test Station Data", testStation)
                }, outputFolder);
            }
            var cisSkips = allegroFile.GetSkipData();
            await CreateExcelFile($"{fileName} CIS Skip Data", new List<(string Name, string Data)>() { ("CIS Skip Data", cisSkips) }, outputFolder);
            var depthExceptions = allegroFile.GetDepthExceptions(36, double.MaxValue);
            await CreateExcelFile($"{fileName} Shallow Cover", new List<(string Name, string Data)>() { ("Shallow Cover", depthExceptions) }, outputFolder);
            var shapefile = allegroFile.GetShapeFile();
            await CreateExcelFile($"{fileName} Shapefile", new List<(string Name, string Data)>() { ("Shapefile", shapefile) }, outputFolder);
            await CreateExcelFile($"{fileName} Files Order", new List<(string Name, string Data)>() { ("Order", allegroFile.FileInfos.GetExcelData(0)) }, outputFolder);
            var cisKmlFile = new KmlFile($"{fileName} CIS Map", allegroFile.GetCisKmlData());
            await cisKmlFile.WriteToFile(outputFolder);
            var depthKmlFile = new KmlFile($"{fileName} Depth Map", allegroFile.GetDepthKmlData());
            await depthKmlFile.WriteToFile(outputFolder);
        }

        public async Task CreateExcelFile(string fileName, List<(string Name, string Data)> sheets, StorageFolder outputFolder)
        {
            var outputFile = await outputFolder.CreateFileAsync(fileName + ".xlsx", CreationCollisionOption.ReplaceExisting);
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
    }
}
