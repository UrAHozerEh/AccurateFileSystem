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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CisProcessor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
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
                var temp = endMpString;
                endMpString = startMpString;
                startMpString = temp;
            }

            title = title.Replace("START", startMpString);
            title = title.Replace("END", endMpString);

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
                Content = "Is Reverse?",
                IsChecked = isReversed
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
                var maxGap = 3000;
                var cisFiles = new List<AllegroCISFile>();
                var fileNames = new HashSet<string>();
                foreach (var storageFile in files)
                {
                    var fileFactory = new FileFactory(storageFile);
                    var file = await fileFactory.GetFile();
                    if (!(file is AllegroCISFile))
                        continue;
                    var allegroFile = file as AllegroCISFile;
                    if (!fileNames.Contains(allegroFile.Name))
                    {
                        cisFiles.Add(allegroFile);
                        fileNames.Add(allegroFile.Name);
                    }
                    else
                    {
                        if (allegroFile.Extension == ".csv")
                        {
                            for (int i = 0; i < cisFiles.Count; ++i)
                            {
                                if (cisFiles[i].Name == allegroFile.Name)
                                {
                                    cisFiles.RemoveAt(i);
                                    cisFiles.Add(allegroFile);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (cisFiles.Count == 0)
                {
                    continue;
                }
                cisFiles.Sort((file1, file2) => file1.Name.CompareTo(file2.Name));

                var combinedFiles = CombinedAllegroCISFile.CombineFiles(folder.DisplayName, cisFiles, maxGap);
                var finishedFinalName = finishedFileNames.GetValueOrDefault(folder.DisplayName, null);
                combinedFiles.FixContactSpikes();
                combinedFiles.FixGps();
                var cisKmlFile = new KmlFile($"{finishedFinalName.Value.Item1} CIS Map Pre Straighten", combinedFiles.GetCisKmlData());
                await cisKmlFile.WriteToFile(outputFolder);
                combinedFiles.StraightenGps();
                combinedFiles.RemoveComments("+");
                var name = await MakeGraphs(combinedFiles, outputFolder, finishedFinalName);
                await CreateExcelFile($"{folder.DisplayName}+{name.Text}+{(name.IsReversed ? "T" : "F")}", new List<(string Name, string Data)>() { ("Order", combinedFiles.FileInfos.GetExcelData(0)) }, fileOrder);
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

        private async Task<(string Text, bool IsReversed)> MakeGraphs(CombinedAllegroCISFile allegroFile, StorageFolder outputFolder, (string Text, bool IsReversed)? exact = null)
        {
            var testStationInitial = allegroFile.GetTestStationData();
            var firstPoint = allegroFile.Points.First();
            var startComment = firstPoint.Footage + " -> " + firstPoint.Point.StrippedComment;
            var lastPoint = allegroFile.Points.Last();
            var endComment = lastPoint.Footage + " -> " + lastPoint.Point.StrippedComment;
            (string Text, bool IsReversed)? response;
            if (!exact.HasValue)
                response = await InputTextDialogAsync($"PG&E LS {allegroFile.Name.Replace("ls", "", StringComparison.OrdinalIgnoreCase).Replace("line", "", StringComparison.OrdinalIgnoreCase).Trim()} MP START to MP END", testStationInitial, startComment, endComment);
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
            await CreateStandardFiles(curFileName, allegroFile, outputFolder);
            var cisShapeFile = new ShapefileData($"{response.Value.Text}", allegroFile.GetShapeFile());
            var shapefileFolder = await outputFolder.CreateFolderAsync("Shapefiles", CreationCollisionOption.OpenIfExists);
            await cisShapeFile.WriteToFolder(shapefileFolder);
            //if (MirFilter.IsChecked ?? false)
            //    await CreateExcelFile($"{curFileName} MIR Skips", new List<(string Name, string Data)>() { ("MIR Skips", mirFilterData) });
            var imageFiles = new List<StorageFile>();
            for (int i = 0; i < pages.Count; ++i)
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

        private async Task CreateStandardFiles(string fileName, CombinedAllegroCISFile allegroFile, StorageFolder outputFolder)
        {
            var tabular = allegroFile.GetTabularData();
            await CreateExcelFile($"{fileName} Tabular Data", new List<(string Name, string Data)>() { ("Tabular Data", tabular) }, outputFolder);
            var dataMetrics = new DataMetrics(allegroFile.GetPoints());
            await CreateExcelFile($"{fileName} Data Metrics", dataMetrics.GetSheets(), outputFolder);
            var testStation = allegroFile.GetTestStationData();
            await CreateExcelFile($"{fileName} Test Station Data", new List<(string Name, string Data)>() { ("Test Station Data", testStation) }, outputFolder);
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
