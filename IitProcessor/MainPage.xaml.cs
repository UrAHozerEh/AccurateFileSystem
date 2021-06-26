﻿using AccurateFileSystem;
using AccurateFileSystem.EsriShapefile;
using AccurateFileSystem.Kmz;
using AccurateFileSystem.Xml;
using AccurateReportSystem;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Page = Windows.UI.Xaml.Controls.Page;
using Colors = Windows.UI.Colors;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IitProcessor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public string ReportQ = "";
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void buttonDoWork_Click(object sender, RoutedEventArgs e)
        {
            await DoWork();
        }

        private async Task DoWork()
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null)
                return;
            var outputFolder = await folder.CreateFolderAsync("Processed Data", CreationCollisionOption.OpenIfExists);
            var curFolders = await folder.GetFoldersAsync();
            var masterFolders = new List<StorageFolder>();

            if (curFolders.Count(f => f.DisplayName == "DCVG" || f.DisplayName == "ACVG") == 0)
            {
                masterFolders.Add(folder);
            }
            else
            {
                masterFolders = curFolders.ToList();
            }
            var masterFiles = await folder.GetFilesAsync();
            StorageFile regionFile = null;
            StorageFile kmlFile = null;
            foreach (var masterFile in masterFiles)
            {
                if (masterFile.DisplayName.Contains("regions", StringComparison.OrdinalIgnoreCase))
                    regionFile = masterFile;
                if (masterFile.FileType.ToLower() == ".kml")
                    kmlFile = masterFile;
            }
            Dictionary<string, List<List<BasicGeoposition>>> lineData = null;
            IitRegionFile regions = null;
            if (kmlFile != null)
                lineData = GetKmlLineData(await GeneralXmlFile.GetGeneralXml(kmlFile));
            if (regionFile != null)
                regions = await IitRegionFile.GetIitRegion(regionFile);
            var globalAlignData = new List<(double, double, double, double)>();
            foreach (var masterFolder in masterFolders)
            {
                await ParseIitMasterFolder(masterFolder, regions, outputFolder, lineData);
            }
            var dialog = new MessageDialog($"Finished making IIT Stuff");
            await dialog.ShowAsync();
        }

        private async Task ParseIitMasterFolder(StorageFolder masterFolder, IitRegionFile regions, StorageFolder outputFolder, Dictionary<string, List<List<BasicGeoposition>>> lineData)
        {
            if (masterFolder.DisplayName == "Processed Data")
                return;
            var isDcvg = masterFolder.DisplayName == "DCVG";
            var folders = await masterFolder.GetFoldersAsync();
            ReportQ = "";

            var masterFiles = await masterFolder.GetFilesAsync();
            StorageFile regionFile = null;
            StorageFile kmlFile = null;
            foreach (var masterFile in masterFiles)
            {
                if (masterFile.DisplayName.Contains("regions", StringComparison.OrdinalIgnoreCase))
                    regionFile = masterFile;
                if (masterFile.FileType.ToLower() == ".kml")
                    kmlFile = masterFile;
            }
            if (kmlFile != null)
                lineData = GetKmlLineData(await GeneralXmlFile.GetGeneralXml(kmlFile));
            if (regionFile != null)
                regions = await IitRegionFile.GetIitRegion(regionFile);

            foreach (var curFolder in folders)
            {
                await ParseIitFolder(curFolder, regions, isDcvg, outputFolder, lineData);
            }
            var outputFile = await outputFolder.CreateFileAsync($"{masterFolder.DisplayName} Report Q.xlsx", CreationCollisionOption.ReplaceExisting);
            using (var outStream = await outputFile.OpenStreamForWriteAsync())
            using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                var wbPart = spreadDoc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                wbPart.Workbook.AppendChild(new Sheets());
                AddData(wbPart, ReportQ, 1, "Report Q", new List<string>() { "A1:A2", "B1:B2", "C1:D1", "E1:F1", "G1:G2", "H1:H2", "I1:I2", "J1:M1", "N1:Q1" });
                //AddData(wbPart, reportQ, 2, "Report Q2", new List<string>() { "A1:A2", "B1:B2", "C1:D1", "E1:F1", "G1:G2", "H1:H2", "I1:I2", "J1:M1", "N1:Q1" });
                wbPart.Workbook.Save();
            }
        }

        private async Task ParseIitFolder(StorageFolder folder, IitRegionFile regions, bool isDcvg, StorageFolder outputFolder, Dictionary<string, List<List<BasicGeoposition>>> lineData)
        {
            var displayName = folder.DisplayName;
            var plusIndex = displayName.IndexOf('+');
            if (plusIndex != -1)
            {
                displayName = displayName.Substring(plusIndex + 1).Trim();
            }

            var storageFiles = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);
            var cisFiles = new List<AllegroCISFile>();
            var dcvgFiles = new List<AllegroCISFile>();
            var pcmFiles = new List<CsvPcm>();
            var acvgReads = new List<(BasicGeoposition, double)>();

            foreach (var file in storageFiles)
            {
                var factory = new FileFactory(file);
                var newFile = await factory.GetFile();
                if (newFile is CsvPcm)
                {
                    pcmFiles.Add(newFile as CsvPcm);
                    continue;
                }
                if (newFile is AllegroCISFile)
                {
                    var allegroFile = newFile as AllegroCISFile;
                    if (allegroFile.Type == FileType.OnOff)
                        cisFiles.Add(allegroFile);
                    if (allegroFile.Type == FileType.DCVG)
                        dcvgFiles.Add(allegroFile);
                    continue;
                }
                if (newFile == null && file.FileType.ToLower() == ".acvg")
                {
                    acvgReads = ParseAcvgReads(await file.GetLines());
                }
            }
            var hca = regions.GetHca(displayName);
            if (cisFiles.Count == 0)
            {
                var (startMp, endMp) = hca.GetMpForHca();
                var region = hca.Regions[0];
                var startGps = region.StartGps;
                var endGps = region.EndGps;
                ReportQ += $"{hca.Name}\t{hca.LineName}\t{startMp}\t{endMp}\tNT\tNT\t{region.Name}\t";
                ReportQ += $"{hca.HcaGpsLength:F0}\tNT\t{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t";
                ReportQ += $"{endGps.Latitude:F8}\t{endGps.Longitude:F8}\t{hca.FirstTimeString}\tNT\tNT\tNT\t\n";
                return;
            }
            cisFiles = GetUniqueFiles(cisFiles);
            dcvgFiles = GetUniqueFiles(dcvgFiles);
            var combinedCisFile = CombinedAllegroCISFile.CombineFiles("Combined", cisFiles, 1500);
            var curLineData = lineData.GetValueOrDefault(hca.LineName);

            combinedCisFile.ReverseBasedOnHca(hca);
            var (startHcaFootage, endHcaFootage) = AddHcaComments(combinedCisFile, hca);
            combinedCisFile.StraightenGps();
            combinedCisFile.RemoveComments("+");
            combinedCisFile.AlignToLineData(curLineData, startHcaFootage, endHcaFootage);

            var hcaStartPoint = combinedCisFile.GetClosesetPoint(startHcaFootage);
            hca.Regions[0].StartGps = hcaStartPoint.Point.GPS;
            hca.Regions[0].GpsPoints[0] = hcaStartPoint.Point.GPS;
            if (hca.StartBuffer != null)
            {
                var startBufferGpsCount = hca.StartBuffer.GpsPoints.Count;
                hca.StartBuffer.EndGps = hcaStartPoint.Point.GPS;
                hca.StartBuffer.GpsPoints[startBufferGpsCount - 1] = hcaStartPoint.Point.GPS;
            }

            var hcaEndPoint = combinedCisFile.GetClosesetPoint(endHcaFootage);
            hca.Regions.Last().EndGps = hcaEndPoint.Point.GPS;
            var endHcaRegionGpsCount = hca.Regions.Last().GpsPoints.Count;
            hca.Regions.Last().GpsPoints[endHcaRegionGpsCount - 1] = hcaEndPoint.Point.GPS;
            if (hca.EndBuffer != null)
            {
                hca.EndBuffer.StartGps = hcaEndPoint.Point.GPS;
                hca.EndBuffer.GpsPoints[0] = hcaEndPoint.Point.GPS;
            }

            combinedCisFile.SetFootageFromGps();
            combinedCisFile.AddPcmDepthData(pcmFiles);

            var dcvgData = new List<(double, double, BasicGeoposition)>();
            if (combinedCisFile.HasStartSkip)
            {
                combinedCisFile.ShiftPoints(-combinedCisFile.Points[1].Footage);
            }
            var combinedFootages = new List<(double, BasicGeoposition)>();

            foreach (var (foot, _, point, _, _) in combinedCisFile.Points)
            {
                if (point.HasGPS)
                {
                    combinedFootages.Add((foot, point.GPS));
                }
            }

            if (combinedCisFile.HasStartSkip)
                combinedFootages.RemoveAt(0);
            if (combinedCisFile.HasEndSkip)
                combinedFootages.RemoveAt(combinedFootages.Count - 1);

            if (isDcvg)
            {
                foreach (var file in dcvgFiles)
                {
                    dcvgData.AddRange(GetAlignedDcvgData(file, combinedFootages, hca));
                }
            }
            else
            {
                dcvgData.AddRange(GetAlignedAcvgData(acvgReads, combinedFootages, hca));
            }
            dcvgData.Sort((first, second) => first.Item1.CompareTo(second.Item1));


            PgeEcdaReportInformation reportInfo;
            if (isDcvg)
            {
                reportInfo = new PgeEcdaReportInformation(combinedCisFile, dcvgFiles, hca, 10, false);
            }
            else
            {
                reportInfo = new PgeEcdaReportInformation(combinedCisFile, acvgReads, hca, 10, false);
            }


            var reportQ = "";
            reportQ += reportInfo.GetReportQ();
            await MakeIITGraphsUpdated(reportInfo.CisFile, reportInfo, isDcvg, displayName, hca, outputFolder);
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

        private async Task MakeIITGraphsUpdated(CombinedAllegroCISFile file, PgeEcdaReportInformation ecdaReport, bool isDcvg, string folderName, Hca hca, StorageFolder outputFolder = null)
        {
            if (outputFolder == null)
                outputFolder = ApplicationData.Current.LocalFolder;
            var dcvgData = ecdaReport.GetIndicationData();

            var maxDepth = 200;
            var curDepth = 50.0;
            var directionData = new List<(double, bool, string)>();
            var pcmLables = new List<(double, string)>();
            AddMaxDepthComment(file, maxDepth);

            var depthData = file.GetDoubleData("Depth");
            var offData = ecdaReport.GetOffData();
            var onData = ecdaReport.GetOnData();
            //if (CheckDepthGaps(depthData, onData.First().footage, onData.Last().footage, 75))
            //    onData = onData;
            var commentData = file.GetCommentData();
            if (onData.Count == 1)
            {
                onData.Add((5, onData[0].Value));
                offData.Add((5, offData[0].Value));
            }
            var report = new GraphicalReport();
            report.LegendInfo.NameFontSize = 16f;
            if (file.Points.Last().Footage < 100)
            {
                report.PageSetup = new AccurateReportSystem.PageSetup(100, 10);
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
            if(hca.Name != "331")
            onOffGraph.Series.Add(off);
            onOffGraph.Series.Add(redLine);
            onOffGraph.DrawTopBorder = false;

            if (file != null && file.Points.Last().Footage < 100)
            {
                onOffGraph.CommentSeries.PercentOfGraph = 0.25f;
            }

            var dcvgLabels = dcvgData.Select((value) => (value.Item1, value.Item2.ToString("F1") + (isDcvg ? "%" : ""))).ToList();

            var indicationLabel = isDcvg ? "DCVG" : "ACVG";
            var dcvgIndication = new PointWithLabelGraphSeries($"{indicationLabel} Indication", -0.2, dcvgLabels)
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
                Title = $"PGE IIT Survey {folderName}"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            var surveyDirectionChart = new Chart(report, "Survey Direction With Survey Date");
            surveyDirectionChart.LegendInfo.NameFontSize = 14f;
            var cisClass = new Chart(report, "CIS Severity");
            var cisIndication = new PGECISIndicationChartSeries(file.GetPoints(), cisClass, hca);
            cisClass.Series.Add(cisIndication);

            var dcvgClass = new Chart(report, "DCVG Severity");
            var dcvgIndicationSeries = new PgeDcvgIndicationChartSeries(dcvgData, dcvgClass, isDcvg);
            dcvgClass.Series.Add(dcvgIndicationSeries);
            dcvgClass.LegendInfo.NameFontSize = 13f;

            var ecdaClassChart = new Chart(report, "ECDA Clas.");
            var ecdaClassSeries = new PGEDirectExaminationPriorityChartSeries(ecdaClassChart, cisIndication, dcvgIndicationSeries);
            ecdaClassChart.Series.Add(ecdaClassSeries);

            var surveyDirectionSeries = new SurveyDirectionWithDateSeries(file.GetDirectionWithDateData());
            surveyDirectionChart.Series.Add(surveyDirectionSeries);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);
            splitContainer.AddSelfSizedContainer(ecdaClassChart);
            splitContainer.AddSelfSizedContainer(surveyDirectionChart);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var surveyLength = onData.Last().Footage;
            var pages = report.PageSetup.GetAllPages(0, surveyLength);

            for (int i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var image = report.GetImage(page, 300);
                var imageFileName = $"{folderName} Page {pageString}.png";
                if (pages.Count == 1)
                    imageFileName = $"{folderName} Graph.png";
                var imageFile = await outputFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
            }

            var areas = ecdaClassSeries.GetUpdatedReportQ();
            var uniqueRegions = hca.GetUniqueRegions();
            uniqueRegions.Sort();
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
            foreach (var (startFoot, endFoot, cisSeverity, dcvgSeverity, region, priority, reason) in areas)
            {
                var depthInArea = extrapolatedDepth.Where(value => value.Item1 >= startFoot && value.Item1 <= endFoot);
                var minDepth = depthInArea.Count() != 0 ? depthInArea.Min(value => value.Item2) : -1;
                var (startMp, endMp) = hca.GetMpForRegion(region);
                output.Append($"{hca.Name}\t{hca.LineName}\t{startMp}\t{endMp}\t");
                var length = endFoot - startFoot;
                var minDepthString = minDepth == -1 ? "" : minDepth.ToString("F0");
                output.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{region.ReportQName}\t{length:F0}\t{minDepthString}\t");
                var startGps = file.GetClosesetGps(startFoot);
                output.Append($"{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t");
                var endGps = file.GetClosesetGps(endFoot);
                var firstTime = hca.FirstTimeString;
                if (region.IsBuffer)
                    firstTime = "N/A";
                output.Append($"{endGps.Latitude:F8}\t{endGps.Longitude:F8}\t{firstTime}\t");
                if (reason.Contains("SKIP."))
                {
                    output.AppendLine($"NT\tNT\tNT\t{region.LongSkipReason}");
                    skipReport.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{Math.Max(endFoot - startFoot, 1):F0}\t");
                    skipReport.Append($"{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t");
                    skipReport.AppendLine($"{endGps.Latitude:F8}\t{endGps.Longitude:F8}\t{region.ShortSkipReason}");
                }
                else
                {
                    output.AppendLine($"{cisSeverity.GetDisplayName()}\t{dcvgSeverity.GetDisplayName()}\t{PriorityDisplayName(priority)}\t{reason}");
                }
            }
            ReportQ += output.ToString();
            var shapefileFolder = await outputFolder.CreateFolderAsync("Shapefiles", CreationCollisionOption.OpenIfExists);
            var cisShapeFileStringBuilder = new StringBuilder();
            foreach (var line in ecdaClassSeries.CISShapeFileOutput)
            {
                var lineString = string.Join('\t', line);
                cisShapeFileStringBuilder.AppendLine(lineString);
            }
            var cisShapeFileTest = new ShapefileData($"{folderName} CIS Shapefile", ecdaClassSeries.CISShapeFileOutput);
            await cisShapeFileTest.WriteToFolder(shapefileFolder);

            var cisKmlFileTest = new KmlFile($"{folderName} CIS Shapefile", file.GetCisKmlData());
            await cisKmlFileTest.WriteToFile(outputFolder);

            var depthShapeFileStringBuilder = new StringBuilder();
            var depthShapeData = new List<string[]>();
            foreach (var line in ecdaClassSeries.CISShapeFileOutput)
            {
                if (string.IsNullOrWhiteSpace(line[7]))
                    continue;
                if (line[0] != "LABEL")
                    line[0] = $"Depth: {line[7]}";
                depthShapeData.Add(line);
                var lineString = string.Join('\t', line);
                depthShapeFileStringBuilder.AppendLine(lineString);
            }
            if (depthShapeData.Count > 1)
            {
                var depthShapeFileTest = new ShapefileData($"{folderName} Depth Shapefile", depthShapeData);
                await depthShapeFileTest.WriteToFolder(shapefileFolder);

                var depthKmlFileTest = new KmlFile($"{folderName} Depth Shapefile", file.GetDepthKmlData());
                await depthKmlFileTest.WriteToFile(outputFolder);
            }

            var dcvgShapeFileStringBuilder = new StringBuilder();
            foreach (var line in ecdaClassSeries.IndicationShapeFileOutput)
            {
                var lineString = string.Join('\t', line);
                dcvgShapeFileStringBuilder.AppendLine(lineString);
            }
            if (dcvgData.Count > 0)
            {
                var dcvgShapeFileTest = new ShapefileData($"{folderName} {(isDcvg ? "DCVG" : "ACVG")} Shapefile", ecdaClassSeries.IndicationShapeFileOutput);
                await dcvgShapeFileTest.WriteToFolder(shapefileFolder);
            }

            var skipReportString = skipReport.ToString();
            var testStation = file.GetTestStationData();
            var depthException = new StringBuilder();
            for (int i = 0; i < extrapolatedDepth.Count; ++i)
            {
                (curFoot, curDepth) = extrapolatedDepth[i];
                if (curDepth < 36)
                {
                    var start = i;
                    var curIndex = i;
                    var minDepth = curDepth;
                    while (curDepth < 36 && curIndex != extrapolatedDepth.Count)
                    {
                        (curFoot, curDepth) = extrapolatedDepth[curIndex];
                        if (curDepth < minDepth)
                            minDepth = curDepth;
                        ++curIndex;
                    }
                    --curIndex;
                    var (startFoot, _) = extrapolatedDepth[start];
                    var (endFoot, _) = extrapolatedDepth[curIndex];
                    depthException.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{Math.Max(endFoot - startFoot, 1):F0}\t{minDepth:F0}\t");
                    var startGps = file.GetClosesetGps(startFoot);
                    depthException.Append($"{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t");
                    var endGps = file.GetClosesetGps(endFoot);
                    depthException.AppendLine($"{endGps.Latitude:F8}\t{endGps.Longitude:F8}");
                    i = curIndex + 1;
                }
                if (curDepth > 72)
                {
                    var start = i;
                    var curIndex = i;
                    var max = curDepth;
                    while (curDepth > 72 && curIndex != extrapolatedDepth.Count)
                    {
                        (curFoot, curDepth) = extrapolatedDepth[curIndex];
                        if (curDepth > max)
                            max = curDepth;
                        ++curIndex;
                    }
                    --curIndex;
                    var (startFoot, _) = extrapolatedDepth[start];
                    var (endFoot, _) = extrapolatedDepth[curIndex];
                    depthException.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{Math.Max(endFoot - startFoot, 1):F0}\t{max:F0}\t");
                    var startGps = file.GetClosesetGps(startFoot);
                    depthException.Append($"{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t");
                    var endGps = file.GetClosesetGps(endFoot);
                    depthException.AppendLine($"{endGps.Latitude:F8}\t{endGps.Longitude:F8}");

                    i = curIndex + 1;
                }
            }
            var depthString = depthException.ToString();
            var uniqueRegionsString = string.Join(", ", uniqueRegions);
            var (hcaStartMp, hcaEndMp) = hca.GetMpForHca();
            //var reportLLengths = ecdaReport.GetActualReadFootage();
            var reportLLengths2 = GetActualReadFootage(areas);
            var reportLString = $"Indirect Inspection:\tCIS\t{indicationLabel}\t{uniqueRegionsString}\t{hcaStartMp}\t{hcaEndMp}\t{hca.LineName}\t{"HCA " + hca.Name}\nLength (feet)\t{reportLLengths2[0]}\t{reportLLengths2[0]}\n\t";
            var reportLNext = "Length (feet)\t";
            for (int i = 1; i <= 4; ++i)
            {
                reportLString += $"{PriorityDisplayName(i)}\t";
                reportLNext += $"{reportLLengths2[i]}\t";
            }
            reportLString += "\n" + reportLNext;

            skipReportString = "\n\n" + skipReportString;
            depthString = "\n\n" + depthString;

            foreach (var (foot, _, point, _, _) in file.Points)
            {
                foreach (var tsRead in point.TestStationReads)
                {
                    if (!(tsRead is ACTestStationRead ac))
                        continue;
                    if (ac.Value >= 10)
                        return;
                }
            }

            //var testFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"{folderName} Reports V2.xlsx", CreationCollisionOption.ReplaceExisting);
            //using (var outStream = await testFile.OpenStreamForWriteAsync())
            //using (var workbook = new XLWorkbook())
            //{
            //    workbook.SaveAs(outStream);
            //}

            //await FileIO.WriteTextAsync(outputFile, outputString);
            var outputFile = await outputFolder.CreateFileAsync($"{folderName} Reports.xlsx", CreationCollisionOption.ReplaceExisting);
            using (var outStream = await outputFile.OpenStreamForWriteAsync())
            using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            {
                var wbPart = spreadDoc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();
                wbPart.Workbook.AppendChild(new Sheets());
                AddData(wbPart, reportLString, 1, "Report L", new List<string>());
                AddData(wbPart, skipReportString, 2, "CIS Skip Report", new List<string>() { "A1:B1", "C1:C2", "D1:G1" });
                AddData(wbPart, skipReportString, 3, $"Other Skip Report", new List<string>() { "A1:B1", "C1:C2", "D1:G1" });
                AddData(wbPart, testStation, 4, "Test Station and Coupon Data", new List<string>());
                AddData(wbPart, depthString, 5, "Depth Exception", new List<string>() { "A1:B1", "C1:C2", "D1:D2", "E1:H1" });
                AddData(wbPart, "Survey Stationing\tAC Read\tComments\tLatitude\tLongitude", 7, "AC Touch Voltage", new List<string>());
                wbPart.Workbook.Save();
            }

            //var exceptionsFullData = PGECISIndicationChartSeries.DataPoint.Headers;
            //foreach (var exception in cisIndication.DataUpdated)
            //{
            //    if (!exception.IsExtrapolated)
            //        exceptionsFullData += "\n" + exception.ToString();
            //}
            //outputFile = await outputFolder.CreateFileAsync($"{folderName} CIS Exceptions Data.xlsx", CreationCollisionOption.ReplaceExisting);
            //using (var outStream = await outputFile.OpenStreamForWriteAsync())
            //using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            //{
            //    var wbPart = spreadDoc.AddWorkbookPart();
            //    wbPart.Workbook = new Workbook();
            //    wbPart.Workbook.AppendChild(new Sheets());
            //    var wbData = exceptionsFullData.TrimEnd('\n').TrimEnd('\r');
            //    AddData(wbPart, wbData, 1, "Exception Data", new List<string>());
            //    wbPart.Workbook.Save();
            //}

            //outputFile = await outputFolder.CreateFileAsync($"{folderName} CIS Shapefile.xlsx", CreationCollisionOption.ReplaceExisting);
            //using (var outStream = await outputFile.OpenStreamForWriteAsync())
            //using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            //{
            //    var wbPart = spreadDoc.AddWorkbookPart();
            //    wbPart.Workbook = new Workbook();
            //    wbPart.Workbook.AppendChild(new Sheets());
            //    var wbData = cisShapeFileStringBuilder.ToString().TrimEnd('\n').TrimEnd('\r');
            //    AddData(wbPart, wbData, 1, "Shapefile", new List<string>());
            //    wbPart.Workbook.Save();
            //}
            //outputFile = await outputFolder.CreateFileAsync($"{folderName} Depth Shapefile.xlsx", CreationCollisionOption.ReplaceExisting);
            //using (var outStream = await outputFile.OpenStreamForWriteAsync())
            //using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            //{
            //    var wbPart = spreadDoc.AddWorkbookPart();
            //    wbPart.Workbook = new Workbook();
            //    wbPart.Workbook.AppendChild(new Sheets());
            //    AddData(wbPart, depthShapeFileStringBuilder.ToString().TrimEnd('\n').TrimEnd('\r'), 1, "Shapefile", new List<string>());
            //    wbPart.Workbook.Save();
            //}
            //if (dcvgData.Count > 0)
            //{
            //    outputFile = await outputFolder.CreateFileAsync($"{folderName} {(isDcvg ? "DCVG" : "ACVG")} Shapefile.xlsx", CreationCollisionOption.ReplaceExisting);
            //    using (var outStream = await outputFile.OpenStreamForWriteAsync())
            //    using (var spreadDoc = SpreadsheetDocument.Create(outStream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
            //    {
            //        var wbPart = spreadDoc.AddWorkbookPart();
            //        wbPart.Workbook = new Workbook();
            //        wbPart.Workbook.AppendChild(new Sheets());
            //        AddData(wbPart, dcvgShapeFileStringBuilder.ToString().TrimEnd('\n').TrimEnd('\r'), 1, "Shapefile", new List<string>());
            //        wbPart.Workbook.Save();
            //    }
            //}
        }

        private string ToStationing(double footage)
        {
            int hundred = (int)footage / 100;
            int tens = (int)footage % 100;
            return hundred.ToString().PadLeft(1, '0') + "+" + tens.ToString().PadLeft(2, '0');
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
    }
}
