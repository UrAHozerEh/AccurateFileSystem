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
using Windows.Management.Policies;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CisProcessor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const double ShallowCover = 36; //Usually 36

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
                return (inputTextBox.Text.Trim(), isReverse.IsChecked.Value);
            else
                return null;
        }

        private async void DoWorkButtonClick(object sender, RoutedEventArgs e)
        {
            try
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
                CisSettings cisSettings = null;
                foreach (var folder in folders)
                {
                    if (folder.DisplayName == outputFolder.DisplayName || folder.DisplayName == fileOrder.DisplayName)
                        continue;
                    var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
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
                            continue;
                        }
                        if (file is CisSettings settings)
                        {
                            cisSettings = settings;
                            continue;
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
                    if (cisSettings == null)
                    {
                        cisSettings = new CisSettings();
                    }
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
                    var type = $" On Off {(staticFiles.Count == 0 ? "" : "And Depol ")}CIS";
                    var combinedOnOffFiles = CombinedAllegroCisFile.CombineOrderedFiles(folder.DisplayName + type, onOffFiles, 5);
                    var combinedStaticFiles = CombinedAllegroCisFile.CombineOrderedFiles(folder.DisplayName + " Depol CIS", staticFiles, 5);
                    combinedStaticFiles?.AddPcmDepthData(docFiles);
                    combinedStaticFiles?.AddMaxDepthComment(cisSettings.DepthGraphMaxValue);
                    combinedOnOffFiles?.AddPcmDepthData(docFiles);
                    combinedOnOffFiles?.AddMaxDepthComment(cisSettings.DepthGraphMaxValue);
                    var pcmReads = new List<(double Footage, double Read)>();
                    if (docFiles.Count != 0)
                    {
                        foreach (var docFile in docFiles)
                        {

                            foreach (var (gps, read, _) in docFile.AmpData)
                            {
                                if (read == 0) continue;
                                if (combinedOnOffFiles != null)
                                {
                                    var (footage, dist) = combinedOnOffFiles.GetClosestFootage(gps);
                                    pcmReads.Add((footage, read));
                                }
                                else
                                {
                                    var (footage, dist) = combinedStaticFiles.GetClosestFootage(gps);
                                    pcmReads.Add((footage, read));
                                }
                            }
                        }
                    }
                    combinedOnOffFiles?.FixContactSpikes();
                    combinedStaticFiles?.FixContactSpikes();
                    combinedStaticFiles?.FixGps();
                    if (cisSettings.StraightenGps)
                        combinedStaticFiles?.StraightenGps(cisSettings.StraightenGpsCommentsDistance.Value);
                    if (cisSettings.SetFootageFromGps)
                        combinedStaticFiles?.SetFootageFromGps();
                    combinedOnOffFiles?.FixGps();
                    if (cisSettings.StraightenGps)
                        combinedOnOffFiles?.StraightenGps(cisSettings.StraightenGpsCommentsDistance.Value);
                    if (cisSettings.SetFootageFromGps)
                        combinedOnOffFiles.SetFootageFromGps();
                    combinedOnOffFiles?.RemoveComments("+");
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
                        var (text, isReversed) = await MakeOnOffStaticGraphs(combinedOnOffFiles, combinedStaticFiles, outputFolder, pcmReads, cisSettings, finishedFinalName);
                        await CreateExcelFile($"{folder.DisplayName}+{text}+{(isReversed ? "T" : "F")}", new List<(string Name, string Data)>() { ("Order", combinedOnOffFiles.FileInfos.GetExcelData(0)) }, fileOrder);
                        await CreateExcelFile($"{folder.DisplayName} Static+{text}+{(isReversed ? "T" : "F")}", new List<(string Name, string Data)>() { ("Order", combinedStaticFiles.FileInfos.GetExcelData(0)) }, fileOrder);
                    }
                    else
                    {
                        var file = combinedOnOffFiles ?? combinedStaticFiles;
                        var (text, isReversed) = await MakeOnOffStaticGraphs(combinedOnOffFiles, null, outputFolder, pcmReads, cisSettings, finishedFinalName);
                        await CreateExcelFile($"{folder.DisplayName}+{text}+{(isReversed ? "T" : "F")}", new List<(string Name, string Data)>() { ("Order", file.FileInfos.GetExcelData(0)) }, fileOrder);
                    }
                }
                var dialog = new MessageDialog("Done");
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new MessageDialog("Error: " + ex.Message);
                await dialog.ShowAsync();
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

        private async Task<(string Text, bool IsReversed)> MakeOnOffStaticGraphs(CombinedAllegroCisFile onOffFile, CombinedAllegroCisFile staticFile, StorageFolder masterOutputFolder, List<(double Footage, double Read)> pcmReads, CisSettings cisSettings, (string Text, bool IsReversed)? exact = null)
        {
            var testStationInitial = onOffFile.GetTestStationData();
            var firstPoint = onOffFile.Points.First();
            var startComment = firstPoint.Footage + " -> " + firstPoint.Point.StrippedComment;
            var lastPoint = onOffFile.Points.Last();
            var endComment = lastPoint.Footage + " -> " + lastPoint.Point.StrippedComment;
            (string Text, bool IsReversed)? response;
            if (!exact.HasValue)
                response = await InputTextDialogAsync($"{onOffFile.Name.Trim()} MP START to MP END", testStationInitial, startComment, endComment);
            else
                response = (exact.Value.Text, exact.Value.IsReversed);//await InputTextDialogAsync(exact, testStationInitial, startComment, endComment);

            if (response == null)
                return (null, false);
            if (response.Value.Item2)
            {
                onOffFile.Reverse();
            }
            var report = new GraphicalReport();
            if (cisSettings.HasFeetPerPage)
                report.PageSetup.FootagePerPage = cisSettings.FeetPerPage.Value;
            else
            {
                var lastFootage = lastPoint.Footage;
                if (staticFile != null)
                {
                    lastFootage = Math.Max(lastFootage, staticFile.Points.Last().Footage);
                }
                report.PageSetup.FootagePerPage = lastFootage;
            }
            report.PageSetup.Overlap = cisSettings.FeetOverlap;
            var commentGraph = new Graph(report);
            var graph1 = new Graph(report);

            var graph2 = new Graph(report);
            var graph3 = new Graph(report);
            //var mirFilterData = "Start Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tReason\n" + ((MirFilter.IsChecked ?? false) ? allegroFile.FilterMir(new List<string>() { "anode", "rectifier" }) : "");

            var on = new GraphSeries("On", onOffFile.GetDoubleData("On"))
            {
                LineColor = Colors.Blue,
                MaxDrawDistance = cisSettings.CisGap
            };
            var off = new GraphSeries("Off", onOffFile.GetDoubleData("Off"))
            {
                LineColor = Colors.Green,
                MaxDrawDistance = cisSettings.CisGap
            };
            var onMir = new GraphSeries("On MIR Compensated", onOffFile.GetDoubleData("On Compensated"))
            {
                LineColor = Colors.Purple,
                MaxDrawDistance = cisSettings.CisGap
            };
            var offMir = new GraphSeries("Off MIR Compensated", onOffFile.GetDoubleData("Off Compensated"))
            {
                LineColor = Color.FromArgb(255, 57, 255, 20),
                MaxDrawDistance = cisSettings.CisGap
            };
            var depth = new GraphSeries("Depth", onOffFile.GetDoubleData("Depth"))
            {
                LineColor = Colors.Black,
                PointColor = Colors.Orange,
                IsY1Axis = false,
                PointShape = GraphSeries.Shape.Circle,
                GraphType = GraphSeries.Type.Point
            };
            var commentSeries = new CommentSeries { Values = onOffFile.GetCommentData(ignoreStartEndSkips: true), PercentOfGraph = 0.5f, IsFlippedVertical = false, BorderType = BorderType.Pegs };

            commentGraph.CommentSeries = commentSeries;
            commentGraph.LegendInfo.Name = "CIS Comments";
            commentGraph.DrawTopBorder = false;

            commentGraph.XAxisInfo.MajorGridline.IsEnabled = false;
            commentGraph.YAxesInfo.MinorGridlines.IsEnabled = false;
            commentGraph.YAxesInfo.MajorGridlines.IsEnabled = false;
            commentGraph.YAxesInfo.Y1IsDrawn = false;

            graph1.CommentSeries = commentSeries;
            graph1.YAxesInfo.Y1MinimumValue = cisSettings.CisGraphMinValue;
            graph1.YAxesInfo.Y1MaximumValue = cisSettings.CisGraphMaxValue;
            graph1.YAxesInfo.MajorGridlines.Offset = cisSettings.CisGraphMajorGridStep;
            graph1.YAxesInfo.MinorGridlines.Offset = cisSettings.CisGraphMinorGridStep;
            graph1.YAxesInfo.Y2MaximumValue = cisSettings.DepthGraphMaxValue;
            graph1.YAxesInfo.Y1IsInverted = cisSettings.InvertGraph;

            graph1.Series.Add(on);
            if (cisSettings.UseMir)
                graph1.Series.Add(onMir);
            if (onOffFile.Type == FileType.OnOff)
            {
                graph1.Series.Add(off);
                if (cisSettings.UseMir)
                    graph1.Series.Add(offMir);
            }
            List<(double Footage, double Value)> polData = null;
            if (staticFile != null)
            {
                if (response.Value.Item2)
                {
                    staticFile?.Reverse();
                }
                var staticData = new GraphSeries("Static", staticFile.GetDoubleData("On"))
                {
                    LineColor = Colors.Magenta,
                    MaxDrawDistance = cisSettings.CisGap
                };
                polData = (cisSettings.UseMir ? offMir : off).Difference(staticData, cisSettings.CisGap);
                var polarizationData = new GraphSeries("Polarization", polData)
                {
                    LineColor = Colors.Crimson,
                    MaxDrawDistance = cisSettings.CisGap
                };
                graph1.Series.Add(staticData);
                graph1.Series.Add(polarizationData);
            }
            if (depth.Values.Count != 0)
            {
                graph1.Series.Add(depth);
                graph1.YAxesInfo.Y2IsDrawn = true;
            } // DEPTH
            if (cisSettings.HasMinOffValue)
            {
                var redLine = new SingleValueGraphSeries($"{cisSettings.MinOffValue:F3} Line", cisSettings.MinOffValue.Value)
                {
                    IsDrawnInLegend = false
                };
                graph1.Series.Add(redLine);
            }
            if (cisSettings.HasMaxOffValue)
            {
                var redLine = new SingleValueGraphSeries($"{cisSettings.MaxOffValue:F3} Line", cisSettings.MaxOffValue.Value)
                {
                    IsDrawnInLegend = false
                };
                graph1.Series.Add(redLine);
            }
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
            var chart2 = new Chart(report, $"CIS Exception Data")
            {
                LegendInfo =
                {
                    SeriesNameFontSize = 8f,
                    NameFontSize = 14f
                }
            };
            if (cisSettings.HasCisExceptions)
            {
                ExceptionsChartSeries exceptions = new OnOffPassingRangeExceptionChartSeries(cisSettings.UseMir ? onOffFile.GetCombinedMirData() : onOffFile.GetCombinedData(), chart2.LegendInfo, chart2.YAxesInfo)
                {
                    LegendLabelSplit = 0.5f,
                    MinimumValue = cisSettings.MinOffValue,
                    MaximumValue = cisSettings.MaxOffValue,
                    MaxDistance = cisSettings.CisGap
                };
                chart2.Series.Add(exceptions);
            }

            var chart3 = new Chart(report, "Polarization Data")
            {
                LegendInfo =
                {
                    SeriesNameFontSize = 8f,
                    NameFontSize = 12f
                }
            };
            if (staticFile != null)
            {

                ExceptionsChartSeries polExceptions = new PolarizationExceptionChartSeries(polData, chart3.LegendInfo, chart3.YAxesInfo, cisSettings.MinDepolValue)
                {
                    LegendLabelSplit = 0.5f,
                    MaxDistance = cisSettings.CisGap
                };
                chart3.Series.Add(polExceptions);
            }

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
                if (cisSettings.HasCisExceptions)
                    splitContainer.AddSelfSizedContainer(chart2);
                if (staticFile != null)
                    splitContainer.AddSelfSizedContainer(chart3);
            }
            splitContainer.AddSelfSizedContainer(chart1);
            //splitContainer.AddContainer(graph2);
            //splitContainer.AddContainer(graph3);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var lastFoot = onOffFile.Points.Last().Footage;
            if (staticFile != null && staticFile.Points.Last().Footage > lastFoot)
            {
                lastFoot = staticFile.Points.Last().Footage;
            }
            var pages = report.PageSetup.GetAllPages(0, lastFoot);
            var curFileName = $"{topGlobalXAxis.Title}";
            var addedPcmValues = new List<(string, List<(double, double)>)>
            {
                ("PCM Values", pcmReads),
            };
            if (polData != null)
            {
                addedPcmValues.Add(("Polarizaion", polData));
            }
            var curOutputName = response.Value.Text;
            var foundHyphenGap = curOutputName.IndexOf(" - ");
            if (foundHyphenGap > 0)
            {
                curOutputName = curOutputName.Substring(0, foundHyphenGap).Trim();
            }
            var outputFolder = await masterOutputFolder.CreateFolderAsync(curOutputName, CreationCollisionOption.OpenIfExists);

            await CreateStandardFiles(curFileName, onOffFile, outputFolder, cisSettings, addedPcmValues, staticFile);

            var shapefileFolder = await masterOutputFolder.CreateFolderAsync("Shapefiles", CreationCollisionOption.OpenIfExists);
            var cisShapeFile = new ShapefileData($"{response.Value.Text}", onOffFile.GetShapeFile());
            await cisShapeFile.WriteToFolder(shapefileFolder);

            var passShapefileFolder = await masterOutputFolder.CreateFolderAsync("Passing Shapefiles", CreationCollisionOption.OpenIfExists);
            var passingCisShapefileData = onOffFile.GetPassingShapeFile();
            if (!string.IsNullOrEmpty(passingCisShapefileData))
            {
                var passingCisShapeFile = new ShapefileData($"{response.Value.Text}", passingCisShapefileData);
                await passingCisShapeFile.WriteToFolder(passShapefileFolder);
            }

            var failingShapefileFolder = await masterOutputFolder.CreateFolderAsync("Failing Shapefiles", CreationCollisionOption.OpenIfExists);
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
                var imageFile = await outputFolder.CreateFileAsync($"{fileName}", CreationCollisionOption.ReplaceExisting);
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
            foreach (var cisPointData in cisFile.Points)
            {
                var depolPointData = depolFile.GetClosesetPoint(cisPointData.Footage);
                if (depolPointData == null)
                    continue;
                depolValues.Add((cisPointData.Footage, depolPointData.Point.On.ToString("F4")));
                if (depolPointData.Point.HasTime)
                    depolDates.Add((cisPointData.Footage, depolPointData.Point.Times[0].ToShortDateString()));
            }

            return (depolValues, depolDates);
        }

        private async Task CreateStandardFiles(string fileName, CombinedAllegroCisFile allegroFile, StorageFolder outputFolder, CisSettings cisSettings, List<(string, List<(double, double)>)> addedValues = null, CombinedAllegroCisFile depolFile = null)
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
                stringAddedValues.Add(("Depol Dates", depolDates));
                tabular = allegroFile.GetTabularData(addedValues: stringAddedValues);
            }
            await CreateExcelFile($"{fileName} Tabular Data", new List<(string Name, string Data)>() { ("Tabular Data", tabular) }, outputFolder);
            var dataMetrics = new DataMetrics(allegroFile.GetPoints(), cisSettings.UseMir);
            if (depolFile != null)
            {
                await CreateExcelFile($"{fileName} Depol Tabular Data", new List<(string Name, string Data)>() { ("Depol Tabular Data", depolFile.GetTabularData()) }, outputFolder);
                if (addedValues.Any(value => value.Item1 == "Polarization"))
                {
                    var polData = addedValues.First(val => val.Item1 == "Polarization");
                    dataMetrics = new DataMetrics(allegroFile.GetPoints(), cisSettings.UseMir, polData.Item2);
                }
            }

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
            //var cisSkips = allegroFile.GetSkipData();
            //await CreateExcelFile($"{fileName} CIS Skip Data", new List<(string Name, string Data)>() { ("CIS Skip Data", cisSkips) }, outputFolder);
            var depthExceptions = allegroFile.GetDepthExceptions(ShallowCover, double.MaxValue);
            await CreateExcelFile($"{fileName} Shallow Cover", new List<(string Name, string Data)>() { ("Shallow Cover", depthExceptions) }, outputFolder);
            var shapefile = allegroFile.GetShapeFile();
            await CreateExcelFile($"{fileName} Shapefile", new List<(string Name, string Data)>() { ("Shapefile", shapefile) }, outputFolder);
            await CreateExcelFile($"{fileName} Files Order", new List<(string Name, string Data)>() { ("Order", allegroFile.FileInfos.GetExcelData(0)) }, outputFolder);
            var cisKmlFile = new KmlFile($"{fileName} CIS Map", allegroFile.GetCisKmlData());
            await cisKmlFile.WriteToFile(outputFolder);
            if (depolFile != null)
            {
                var depolKmlFile = new KmlFile($"{fileName} Depol Map", depolFile.GetCisKmlData());
                await depolKmlFile.WriteToFile(outputFolder);
            }
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
