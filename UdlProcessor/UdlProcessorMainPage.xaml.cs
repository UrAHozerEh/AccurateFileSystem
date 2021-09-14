using AccurateFileSystem;
using AccurateFileSystem.Udl;
using AccurateReportSystem;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UdlProcessor
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

        private async void DoWorkButtonClick(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var masterFolder = await folderPicker.PickSingleFolderAsync();
            if (masterFolder == null)
            {
                return;
            }

            var folders = await masterFolder.GetFoldersAsync();
            var masterOutputFolder = await masterFolder.CreateFolderAsync("0000 Processed Data", CreationCollisionOption.OpenIfExists);
            folders = folders.OrderBy(folder => folder.DisplayName).ToList().AsReadOnly();

            var files = await masterFolder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            foreach (var storageFile in files)
            {
                var fileFactory = new FileFactory(storageFile);
                var file = await fileFactory.GetFile();
                if (!(file is UdlFile))
                {
                    continue;
                }

                var udl = file as UdlFile;
                var udlData = udl.GetDayFullData(24);
                var fileName = file.Name.Replace(".csv", "");
                var fileFolder = await masterOutputFolder.CreateFolderAsync(fileName, CreationCollisionOption.OpenIfExists);
                foreach (var (readType, graphData) in udlData)
                {
                    if (readType == "Temperature")
                    {
                        continue;
                    }

                    var (min, max) = udl.GetMaxAndMinValues(readType);
                    if (min == 0 && max == 0)
                    {
                        continue;
                    }
                    var readFolder = await fileFolder.CreateFolderAsync(readType + " Daily Graphs", CreationCollisionOption.OpenIfExists);
                    foreach (var (graphName, values) in graphData)
                    {
                        if (values.Values.Count == 0)
                        {
                            continue;
                        }
                        await MakeHourlyGraphs(values, readType, $"'{fileName}' on {graphName}", readFolder, 1, 0.25, 5.0 / 60.0, 5.0 / 60.0, min, max);
                    }
                    var fullDataSet = udl.GetFullData(readType);
                    if (fullDataSet.Values.Count == 0)
                    {
                        continue;
                    }
                    var graphTitle = $"{fullDataSet.StartTime:g} to {fullDataSet.EndTime:g} All Data".Replace("/", "-");
                    var fullFileName = $"{readType} {fileName} All Data";
                    await MakeFullGraphs(fullDataSet, readType, fullFileName, fileFolder, min, max);
                }
            }
        }

        private GraphicalReport CreateReport(UdlDataSet dataSet, string reportTitle, string readName, double min, double max, double minorTimeOffset, double majorTimeOffset)
        {
            var report = new GraphicalReport();
            var graph1 = new Graph(report);
            var values = dataSet.Values;
            var largest = Math.Max(Math.Abs(max), Math.Abs(min));
            var diff = max - min;
            if (diff < .05)
            {
                max = 50;
                min = -50;
                graph1.YAxesInfo.Y1Title = "Potentials (Millivolts)";
                values = values.Select(data => (data.Hour, data.Value * 1000)).ToList();
                report.YAxesInfo.MinorGridlines.Offset = 1.0;
                report.YAxesInfo.MajorGridlines.Offset = 10.0;
            }
            else if (diff < .5)
            {
                max *= 1000;
                min *= 1000;
                graph1.YAxesInfo.Y1Title = "Potentials (Millivolts)";
                values = values.Select(data => (data.Hour, data.Value * 1000)).ToList();
                report.YAxesInfo.MinorGridlines.Offset = 10.0;
                report.YAxesInfo.MajorGridlines.Offset = 100.0;
            }

            var valueSeries = new GraphSeries(readName, values)
            {
                LineColor = Colors.Blue
            };

            graph1.Series.Add(valueSeries);
            graph1.DrawTopBorder = false;
            graph1.DrawBottomBorder = false;

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;
            report.XAxisInfo.MajorGridline.Offset = majorTimeOffset;
            report.XAxisInfo.MinorGridline.Offset = minorTimeOffset;

            var extra = (max - min) * 0.05;


            report.YAxesInfo.Y1MinimumValue = min - extra;
            report.YAxesInfo.Y1MaximumValue = max + extra;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = false
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = reportTitle
            };

            report.XAxisInfo.Title = "Time (MST)";
            report.XAxisInfo.LabelFormat = "Hours";

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(graph1);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            report.PageSetup = PageSetup.HourPageSetup;

            report.LegendInfo.WidthInches = 0;

            return report;
        }

        private async Task MakeHourlyGraphs(UdlDataSet dataSet, string readName, string graphName, StorageFolder outputFolder, double hoursPerPage, double majorOffset, double minorOffset, double overlap, double min, double max)
        {
            var reportName = $"{readName} {graphName}";
            var report = CreateReport(dataSet, reportName, readName, min, max, minorOffset, majorOffset);

            var firstTime = dataSet.Values[0].Hour;
            var lastTime = dataSet.Values[dataSet.Values.Count - 1].Hour;

            report.XAxisInfo.StartDate = dataSet.StartTime;
            report.XAxisInfo.LabelFormat = "DateHour";
            report.XAxisInfo.ExtraTitleHeight = report.XAxisInfo.TitleFontSize + 5;
            report.PageSetup.FootagePerPage = hoursPerPage;
            report.PageSetup.Overlap = overlap;

            var timeSpan = dataSet.EndTime - dataSet.StartTime;
            var pages = report.PageSetup.GetAllPages(dataSet.StartTime.Hour, timeSpan.TotalHours);

            var imageFiles = new List<StorageFile>();
            for (var i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                if (page.EndFootage < firstTime)
                {
                    continue;
                }
                if (page.StartFootage > lastTime)
                {
                    break;
                }
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var fileName = $"{reportName} Page {pageString}.png";
                if (pages.Count == 1)
                {
                    fileName = $"{reportName} Graph.png";
                }
                var imageFile = await outputFolder.CreateFileAsync($"{fileName}", CreationCollisionOption.ReplaceExisting);
                using (var image = report.GetImage(page, 300))
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
                imageFiles.Add(imageFile);
            }
        }

        private async Task MakeFullGraphs(UdlDataSet dataSet, string readName, string reportName, StorageFolder outputFolder, double min, double max)
        {
            var startHour = dataSet.Values.First().Hour;
            var endHour = dataSet.Values.Last().Hour;
            var totalHours = endHour - startHour;
            var minorTimeOffset = 2;
            var majorTimeOffset = 6;
            if (totalHours > 36)
            {
                minorTimeOffset = 6;
                majorTimeOffset = 24;
            }
            var report = CreateReport(dataSet, reportName, readName, min, max, minorTimeOffset, majorTimeOffset);
            report.XAxisInfo.LabelFormat = "DateHour";
            report.XAxisInfo.StartDate = dataSet.StartTime;
            report.XAxisInfo.MinorGridline.IsEnabled = true;
            report.PageSetup.FootagePerPage = totalHours;
            report.PageSetup.Overlap = 0;

            var pages = report.PageSetup.GetAllPages(startHour, totalHours);

            var imageFiles = new List<StorageFile>();
            if (pages.Count > 1)
            {
                return;
            }
            var page = pages[0];
            var imageFile = await outputFolder.CreateFileAsync($"{reportName} Graph.png", CreationCollisionOption.ReplaceExisting);
            using (var image = report.GetImage(page, 300))
            using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            }
            imageFiles.Add(imageFile);
        }
    }
}
