using AccurateFileSystem;
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
        public string ReportQ { get; set; } = "";
        public bool BothAcvgDcvg = false;
        public static bool IsPge { get; } = true;
        public double MaxDepth { get; set; } = 120;
        public double MinDepth { get; set; } = 36;
        public bool BufferStartPlusOne { get; set; } = true;
        public bool RawFilesNeedTwo { get; set; } = true;
        public bool BufferComments { get; set; } = true;
        public bool HcaComments { get; set; } = true;
        public bool LongFileFollowReportQGps { get; set; } = false;
        public bool ShortFileFollowReportQGps { get; set; } = true;
        public bool GraphsInSubFolder { get; set; } = false;
        public bool ReportsInSubFolder { get; set; } = false;

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
            {
                return;
            }

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
                {
                    regionFile = masterFile;
                }

                if (masterFile.FileType.ToLower() == ".kml")
                {
                    kmlFile = masterFile;
                }
            }
            Dictionary<string, List<List<BasicGeoposition>>> lineData = null;
            IitRegionFile regions = null;
            if (kmlFile != null)
            {
                lineData = GetKmlLineData(await GeneralXmlFile.GetGeneralXml(kmlFile));
            }

            if (regionFile != null)
            {
                regions = await IitRegionFile.GetIitRegion(regionFile);
            }

            var globalAlignData = new List<(double, double, double, double)>();
            foreach (var masterFolder in masterFolders)
            {
                await ParseIitMasterFolder(masterFolder, regions, outputFolder, lineData);
            }
            var dialog = new MessageDialog($"Finished making IIT Stuff");
            _ = await dialog.ShowAsync();
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
            var outputFile = await outputFolder.CreateFileAsync($"Report Q {masterFolder.DisplayName} Raw.xlsx", CreationCollisionOption.ReplaceExisting);
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
            var soilReads = new List<(BasicGeoposition, string)>();
            Skips cisSkips = null;
            Skips pcmSkips = null;
            Spacers cisSpacers = null;
            CommentReplacements commentReplacements = null;
            GlobalGpsShift gpsShift = null;

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
                    if (allegroFile.Type == FileType.OnOff || allegroFile.Type == FileType.Native)
                    {
                        cisFiles.Add(allegroFile);
                    }
                    if (allegroFile.Type == FileType.DCVG)
                        dcvgFiles.Add(allegroFile);
                    continue;
                }
                if (newFile is Skips)
                {
                    var skipFile = newFile as Skips;
                    if (newFile.Name.ToLower().Contains("cis") && cisSkips == null)
                        cisSkips = skipFile;
                    else if (newFile.Name.ToLower().Contains("pcm") && pcmSkips == null)
                        pcmSkips = skipFile;
                    else
                        throw new Exception("Unknown Skip File");
                }
                if (newFile is Spacers)
                {
                    cisSpacers = newFile as Spacers;
                }
                if (newFile is CommentReplacements)
                {
                    commentReplacements = newFile as CommentReplacements;
                }
                if (newFile is GlobalGpsShift)
                {
                    gpsShift = newFile as GlobalGpsShift;
                }
                if (newFile == null && file.FileType.ToLower() == ".acvg")
                {
                    acvgReads = ParseAcvgReads(await file.GetLines());
                }
                else if (newFile == null && file.FileType.ToLower() == ".soilres")
                {
                    var lines = await file.GetLines();
                    var splitLines = lines.Select(l => l.Split(','));
                    soilReads = splitLines.Select(s => (new BasicGeoposition() { Latitude = double.Parse(s[1]), Longitude = double.Parse(s[2]) }, s[0])).ToList();
                }
            }
            var hca = regions.GetHca(displayName);
            if (gpsShift != null && gpsShift.HasReportD)
            {
                hca.ShiftGps(gpsShift.ReportDLatitudeShift, gpsShift.ReportDLongitudeShift);
            }
            if (cisFiles.Count == 0)
            {
                var (startMp, endMp) = hca.GetMpForHca();
                var region = hca.Regions[0];
                var startGps = region.StartGps;
                var endGps = region.EndGps;
                ReportQ += $"{hca.Name.TrimEnd('a')}\t{hca.LineName}\t{startMp}\t{endMp}\tNT\tNT\t{region.Name}\t";
                ReportQ += $"{hca.HcaGpsLength:F0}\tNT\t{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t";
                ReportQ += $"{endGps.Latitude:F8}\t{endGps.Longitude:F8}\t{hca.Regions.First().FirstTimeString}\tNT\tNT\tNT\tNT\t{hca.Regions[0].LongSkipReason}\n";
                return;
            }
            cisFiles = GetUniqueFiles(cisFiles);
            foreach (var file in cisFiles)
            {
                if (file.Points.Count == 1)
                {
                    var newPoint = new AllegroDataPoint(file.Points[0], file.Points[0].GPS, "");
                    file.Points.Add(1, newPoint);
                }
            }
            dcvgFiles = GetUniqueFiles(dcvgFiles);
            var combinedCisFile = CombinedAllegroCisFile.CombineFiles("Combined", cisFiles, 1500);
            combinedCisFile.FixGps();
            if (gpsShift != null && gpsShift.HasPre)
            {
                combinedCisFile.ShiftGps(gpsShift.PreLatitudeShift, gpsShift.PreLongitudeShift);
            }
            List<List<BasicGeoposition>> curLineData = null;
            if (lineData != null)
            {
                curLineData = lineData.GetValueOrDefault(hca.LineName);
            }

            combinedCisFile.ReverseBasedOnHca(hca);
            var curFileSetGps = combinedCisFile.Points.Count > 4 ? LongFileFollowReportQGps : ShortFileFollowReportQGps;
            var (startHcaFootage, endHcaFootage, bufferEndFootage, bufferStartFootage) = AddHcaComments(combinedCisFile, hca, commentReplacements, curFileSetGps);

            //combinedCisFile.StraightenGps();
            if (IsPge)
            {
                if (curLineData != null)
                {
                    combinedCisFile.AlignToLineData(curLineData, startHcaFootage, endHcaFootage);
                    combinedCisFile.StraightenGps(bufferStartFootage: bufferStartFootage, bufferEndFootage: bufferEndFootage, maxAnchorDistance: double.MaxValue);
                    combinedCisFile.AlignToLineData(curLineData, startHcaFootage, endHcaFootage);
                }
                else
                {
                    combinedCisFile.StraightenGps(bufferStartFootage: bufferStartFootage, bufferEndFootage: bufferEndFootage, maxAnchorDistance: double.MaxValue);
                }
            }
            combinedCisFile.RemoveComments("+");

            var hcaStartPoint = combinedCisFile.GetClosesetPoint(startHcaFootage);
            hca.Regions[0].StartGps = hcaStartPoint.Point.GPS;
            if (hca.HasStartBuffer && bufferEndFootage.HasValue)
            {
                var bufferEndPoint = combinedCisFile.GetClosesetPoint(bufferEndFootage.Value);
                var startBufferGpsCount = hca.StartBuffer.GpsPoints.Count;
                hca.StartBuffer.EndGps = bufferEndPoint.Point.GPS;
            }

            var hcaEndPoint = combinedCisFile.GetClosesetPoint(endHcaFootage);
            hca.Regions.Last().EndGps = hcaEndPoint.Point.GPS;
            if (hca.HasEndBuffer && bufferStartFootage.HasValue)
            {
                var bufferStartPoint = combinedCisFile.GetClosesetPoint(bufferStartFootage.Value);
                hca.EndBuffer.StartGps = bufferStartPoint.Point.GPS;
            }
            combinedCisFile.SetFootageFromGps();
            if (combinedCisFile.HasStartSkip)
            {
                combinedCisFile.ShiftPoints(-combinedCisFile.Points[1].Footage);
            }

            combinedCisFile.AddPcmDepthData(pcmFiles);
            if (cisSpacers != null)
            {
                combinedCisFile.AddSpacerData(cisSpacers.Data);
            }
            var dcvgData = new List<(double, double, BasicGeoposition)>();

            var ampReads = combinedCisFile.AlignAmpReads(pcmFiles);
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

            if (gpsShift != null && gpsShift.HasPost)
            {
                combinedCisFile.ShiftGps(gpsShift.PostLatitudeShift, gpsShift.PostLongitudeShift);
                hca.ShiftGps(gpsShift.PostLatitudeShift, gpsShift.PostLongitudeShift);
            }

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

            if (pcmFiles.Count == 1 && pcmFiles.First().TxData.Count == 1)
            {
                var (txGps, _) = pcmFiles.First().TxData.First();
                var firstCis = combinedCisFile.Points.First();
                var firstDist = firstCis.Point.GPS.Distance(txGps);
                firstCis.Point.OriginalComment += $", {firstDist:F0} feet from PCM TX";
                firstCis.Point.StrippedComment += $", {firstDist:F0} feet from PCM TX";
                firstCis.Point.CleanComments();

                var lastCis = combinedCisFile.Points.Last();
                if (firstCis.Footage != lastCis.Footage)
                {
                    var lastDist = lastCis.Point.GPS.Distance(txGps);
                    lastCis.Point.OriginalComment += $", {lastDist:F0} feet from PCM TX";
                    lastCis.Point.StrippedComment += $", {lastDist:F0} feet from PCM TX";
                    lastCis.Point.CleanComments();
                }
            }
            else if (pcmFiles.Count != 0)
            {
                foreach (var pcmFile in pcmFiles)
                {
                    if (pcmFile.TxData.Count != 1 || pcmFile.AmpData.Count == 0)
                        continue;
                    CombinedDataPoint firstCis = null;
                    CombinedDataPoint lastCis = null;
                    var (txGps, _) = pcmFile.TxData.First();

                    foreach (var pcmPoint in pcmFile.AmpData)
                    {
                        var (closestCis, _) = combinedCisFile.GetClosestPoint(pcmPoint.Gps);
                        if (firstCis == null || closestCis.Footage < firstCis.Footage)
                        {
                            firstCis = closestCis;
                        }
                        if (lastCis == null || closestCis.Footage > lastCis.Footage)
                        {
                            lastCis = closestCis;
                        }
                    }
                    var firstDist = firstCis.Point.GPS.Distance(txGps);
                    firstCis.Point.OriginalComment += $", {firstDist:F0} feet from PCM TX";
                    firstCis.Point.StrippedComment += $", {firstDist:F0} feet from PCM TX";
                    firstCis.Point.CleanComments();

                    if (firstCis.Footage != lastCis.Footage)
                    {
                        var lastDist = lastCis.Point.GPS.Distance(txGps);
                        lastCis.Point.OriginalComment += $", {lastDist:F0} feet from PCM TX";
                        lastCis.Point.StrippedComment += $", {lastDist:F0} feet from PCM TX";
                        lastCis.Point.CleanComments();
                    }
                }
            }

            PgeEcdaReportInformation reportInfo;
            if (isDcvg)
            {
                reportInfo = new PgeEcdaReportInformation(combinedCisFile, dcvgFiles, ampReads, hca, 10, false);
            }
            else
            {
                reportInfo = new PgeEcdaReportInformation(combinedCisFile, acvgReads, ampReads, hca, 10, false);
            }



            var reportQ = "";
            if (reportInfo.EcdaData.Count == 0)
                reportInfo = reportInfo;
            reportQ += reportInfo.GetReportQ();
            await MakeIITGraphsUpdated(reportInfo.CisFile, reportInfo, isDcvg, displayName, hca, cisSkips, pcmSkips, outputFolder, pcmFiles.FirstOrDefault()?.TxData, soilReads, gpsShift);
        }

        private List<(BasicGeoposition Gps, double Value, double Percent)> GetAmpReads(List<CsvPcm> files)
        {
            var output = new List<(BasicGeoposition Gps, double Value, double Percent)>();
            foreach (var file in files)
            {
                var data = file.AmpData;
                if (data == null)
                    return output;
                for (var i = 0; i < data.Count; ++i)
                {
                    var (curGps, curAmps, _) = data[i];

                    var (_, prevAmps, _) = data[Math.Max(i - 1, 0)];
                    var prevDiff = prevAmps - curAmps;
                    var prevPercent = Math.Max(prevDiff / curAmps * 100, 0);

                    var (_, nextAmps, _) = data[Math.Min(i + 1, data.Count - 1)];
                    var nextDiff = nextAmps - curAmps;
                    var nextPercent = Math.Max(nextDiff / curAmps * 100, 0);

                    var percent = Math.Max(nextPercent, prevPercent);
                    output.Add((curGps, curAmps, percent));
                }

            }
            return output;
        }

        private void AddMaxDepthComment(CombinedAllegroCisFile file, double maxDepth)
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

        private async Task MakeIITGraphsFromNew()
        {
            // Get Input Folder
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add(".");
            var outputFolder = await folderPicker.PickSingleFolderAsync();
            if (outputFolder == null)
                return;
            var folderName = "HCA 5184";

            //read Data.txt file that is tab delimited
            var dataFile = await outputFolder.GetFileAsync("Data.txt");
            var data = await dataFile.GetLines();
            var fullData = new Dictionary<string, List<string>>();
            Dictionary<int, string> columnNames = null;

            foreach (var line in data)
            {
                var values = line.Split('\t');
                if (columnNames is null)
                {
                    columnNames = new Dictionary<int, string>();
                    for (int i = 0; i < values.Length; ++i)
                    {
                        var name = values[i];
                        columnNames.Add(i, name);
                        fullData.Add(name, new List<string>());
                    }
                    continue;
                }
                for (int i = 0; i < values.Length; ++i)
                {
                    fullData[columnNames[i]].Add(values[i]);
                }
            }
            var finalFootage = double.Parse(fullData["Extrapolated Final Gps Footage"].Last());

            var maxDepth = 200;
            var curDepth = 50.0;
            var maxDrawDistance = 20.0;
            var shortGraphLength = 200.0;
            var medGraphLength = 600.0;
            var directionData = new List<(double, bool, string)>();

            var report = new GraphicalReport();
            report.LegendInfo.NameFontSize = 16f;
            if (finalFootage < medGraphLength)
            {
                report.PageSetup = new AccurateReportSystem.PageSetup(200, 20);
                report.XAxisInfo.MajorGridline.Offset = 10;
            }
            if (finalFootage < shortGraphLength)
            {
                report.PageSetup = new AccurateReportSystem.PageSetup(100, 10);
                report.XAxisInfo.MajorGridline.Offset = 10;
            }

            var rawFootage = fullData["Extrapolated Final Gps Footage"];
            var onRawData = fullData["Mir Compensated On"];
            var offRawData = fullData["Mir Compensated Off"];
            var commentRawData = fullData["Raw Comment"];
            var dcvgPercentRawData = fullData["Dcvg Indication Percent"];
            var onData = new List<(double Footage, double Value)>();
            var offData = new List<(double Footage, double Value)>();
            var commentData = new List<(double, string)>();
            var dcvgData = new List<(double, double)>();

            for (int i = 0; i < rawFootage.Count; ++i)
            {
                var curFootage = double.Parse(rawFootage[i]);
                var curOn = onRawData[i];
                var curOff = offRawData[i];
                var curDcvg = dcvgPercentRawData[i];
                if (!string.IsNullOrWhiteSpace(curOn) && double.TryParse(curOn, out double onValue))
                {
                    onData.Add((curFootage, onValue));
                }
                if (!string.IsNullOrWhiteSpace(curOff) && double.TryParse(curOff, out double offValue))
                {
                    offData.Add((curFootage, offValue));
                }
                if (!string.IsNullOrWhiteSpace(curDcvg) && double.TryParse(curDcvg, out double dcvgValue))
                {
                    dcvgData.Add((curFootage, dcvgValue));
                }
                if (!string.IsNullOrWhiteSpace(commentRawData[i]))
                {
                    commentData.Add((curFootage, commentRawData[i]));
                }
                var curDirection = curFootage < finalFootage;
                directionData.Add((curFootage, false, "10/23/2024"));
            }

            var onOffGraph = new Graph(report);
            var on = new GraphSeries("On", onData)
            {
                LineColor = Colors.Blue,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Blue,
                ShapeRadius = 2,
                MaxDrawDistance = maxDrawDistance
            };
            var off = new GraphSeries("Off", offData)
            {
                LineColor = Colors.Green,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Green,
                ShapeRadius = 2,
                MaxDrawDistance = maxDrawDistance
            };
            var depth = new GraphSeries("Depth", new List<(double footage, double value)>())
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
            if (offData.Count > 0)
                onOffGraph.Series.Add(off);
            onOffGraph.Series.Add(redLine);
            onOffGraph.DrawTopBorder = false;

            if (finalFootage < shortGraphLength)
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

            // Old PCM Amp display
            //var ampSeries = new PointWithLabelGraphSeries("PCM (mA)", -0.4, ampLabels)
            //{
            //    ShapeRadius = 3,
            //    PointColor = Colors.Navy,
            //    BackdropOpacity = 1f,
            //    PointShape = GraphSeries.Shape.Square
            //};
            //if (ampData.Count > 0)
            //    onOffGraph.Series.Add(ampSeries);

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = $"PGE IIT Survey HCA 5184"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            var surveyDirectionChart = new Chart(report, "CIS Survey Direction With Survey Date");
            surveyDirectionChart.LegendInfo.NameFontSize = 14f;
            //var cisClass = new Chart(report, "CIS Severity");
            //var cisIndication = new ExceptionsChartSeries(cisClass.LegendInfo, cisClass.YAxesInfo);
            //var cisIndicationExcelData = file.GetTabularData(new List<(string Name, List<(double Footage, double Value)>)>
            //{
            //    ("200 (100 US & 100 DS) Foot Average", cisIndication.Averages),
            //    ("Used Averge for Baseline Footage", cisIndication.UsedBaselineFootages),
            //    ("Used Baseline", cisIndication.Baselines),
            //    ("PCM Data (mA)", ampData)
            //});
            //await CreateExcelFile($"{folderName} CIS Baseline Data", new List<(string Name, string Data)>() { ("Baseline Data", cisIndicationExcelData) }, outputFolder);
            //if (ampData.Count > 0)
            //    await CreateExcelFile($"{folderName} PCM Percent Change Data", file.PcmCalcOutput, outputFolder);


            //cisClass.Series.Add(cisIndication);

            //var dcvgClass = new Chart(report, "DCVG Severity");
            //var dcvgIndicationSeries = new PgeDcvgIndicationChartSeries(dcvgData, dcvgClass, isDcvg);
            //dcvgClass.Series.Add(dcvgIndicationSeries);
            //dcvgClass.LegendInfo.NameFontSize = 13f;

            //var ecdaClassChart = new Chart(report, "ECDA Clas.");
            //var ecdaClassSeries = new PGEDirectExaminationPriorityChartSeries(ecdaClassChart, cisIndication, dcvgIndicationSeries, ecdaReport.GetFullAmpData());
            //ecdaClassChart.Series.Add(ecdaClassSeries);

            var surveyDirectionSeries = new SurveyDirectionWithDateSeries(directionData);
            surveyDirectionChart.Series.Add(surveyDirectionSeries);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);

            //splitContainer.AddSelfSizedContainer(ecdaClassChart);

            //TODO: Add Region chart to graph.
            //var regionChart = new Chart(report, "Regions");
            //var regionSeries = new ChartSeries(ecdaClassChart, cisIndication, dcvgIndicationSeries, ecdaReport.GetFullAmpData());
            //regionChart.Series.Add(regionSeries);

            splitContainer.AddSelfSizedContainer(surveyDirectionChart);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;
            var surveyLength = onData.Last().Footage;
            var pages = report.PageSetup.GetAllPages(0, surveyLength);

            for (var i = 0; i < pages.Count; ++i)
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
        }


        private async Task MakeIITGraphsUpdated(CombinedAllegroCisFile file, PgeEcdaReportInformation ecdaReport, bool isDcvg, string folderName, Hca hca, Skips cisSkips, Skips pcmSkips, StorageFolder outputFolder = null, List<(BasicGeoposition, string)> txLocations = null, List<(BasicGeoposition, string)> soilRes = null, GlobalGpsShift gpsShift = null)
        {
            if (outputFolder == null)
                outputFolder = ApplicationData.Current.LocalFolder;
            var dcvgData = ecdaReport.GetIndicationData();

            var maxDepth = 200;
            var curDepth = 50.0;
            var maxDrawDistance = 20.0;
            var shortGraphLength = 200.0;
            var medGraphLength = 600.0;
            var directionData = new List<(double, bool, string)>();
            AddMaxDepthComment(file, maxDepth);

            var depthData = file.GetDoubleData("Depth");
            var offData = ecdaReport.GetOffData();
            var onData = ecdaReport.GetOnData();
            var ampData = ecdaReport.GetAmpData();
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
            if (file.Points.Last().Footage < medGraphLength)
            {
                report.PageSetup = new AccurateReportSystem.PageSetup(200, 20);
                report.XAxisInfo.MajorGridline.Offset = 10;
            }
            if (file.Points.Last().Footage < shortGraphLength)
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
                MaxDrawDistance = maxDrawDistance,
                SkipFootages = cisSkips?.Footages.Select(f => f.Footage).ToList()
            };
            var off = new GraphSeries("Off", offData.Where(d => d.Value != 0).ToList())
            {
                LineColor = Colors.Green,
                PointShape = GraphSeries.Shape.Circle,
                PointColor = Colors.Green,
                ShapeRadius = 2,
                MaxDrawDistance = maxDrawDistance,
                SkipFootages = cisSkips?.Footages.Select(f => f.Footage).ToList()
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
            if (ecdaReport.CisFile.Type == FileType.OnOff || ecdaReport.GetOffData().Any(d => d.Value != 0))
                onOffGraph.Series.Add(off);
            onOffGraph.Series.Add(redLine);
            onOffGraph.DrawTopBorder = false;

            if (file != null && file.Points.Last().Footage < shortGraphLength)
            {
                onOffGraph.CommentSeries.PercentOfGraph = 0.25f;
            }

            var dcvgLabels = dcvgData.Select((value) => (value.Item1, value.Item2.ToString("F1") + (isDcvg ? "%" : ""))).ToList();
            var ampLabels = ampData.Select((value) => (value.Item1, value.Item2.ToString("F0"))).ToList();

            var indicationLabel = isDcvg ? "DCVG" : "ACVG";
            if (IsPge)
            {
                var dcvgIndication = new PointWithLabelGraphSeries($"{indicationLabel} Indication", -0.2, dcvgLabels)
                {
                    ShapeRadius = 3,
                    PointColor = Colors.Red,
                    BackdropOpacity = 1f
                };
                onOffGraph.Series.Add(dcvgIndication);
            }
            else
            {
                var dcvgIndication = new PointWithLabelGraphSeries($"{indicationLabel} Indication", -0.2, dcvgLabels.Select(d => (d.Item1, "")).ToList())
                {
                    ShapeRadius = 3,
                    PointColor = Colors.Red,
                    BackdropOpacity = 1f
                };
                onOffGraph.Series.Add(dcvgIndication);
            }

            var secondLabel = isDcvg ? "ACVG" : "DCVG";
            var secondIndication = new PointWithLabelGraphSeries($"{secondLabel} Indication", -0.2, new List<(double, string)>())
            {
                ShapeRadius = 3,
                PointColor = Colors.Purple,
                BackdropOpacity = 1f
            };
            if (BothAcvgDcvg)
                onOffGraph.Series.Add(secondIndication);

            // Old PCM Amp display
            //var ampSeries = new PointWithLabelGraphSeries("PCM (mA)", -0.4, ampLabels)
            //{
            //    ShapeRadius = 3,
            //    PointColor = Colors.Navy,
            //    BackdropOpacity = 1f,
            //    PointShape = GraphSeries.Shape.Square
            //};
            //if (ampData.Count > 0)
            //    onOffGraph.Series.Add(ampSeries);

            report.XAxisInfo.IsEnabled = false;
            report.LegendInfo.HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left;
            report.LegendInfo.SeriesNameFontSize = report.YAxesInfo.Y1LabelFontSize;

            var bottomGlobalXAxis = new GlobalXAxis(report)
            {
                DrawPageInfo = true
            };

            var topGlobalXAxis = new GlobalXAxis(report, true)
            {
                Title = $"{(IsPge ? "PGE" : "SoCal Gas")} IIT Survey {folderName}"
            };

            var splitContainer = new SplitContainer(SplitContainerOrientation.Vertical);

            var surveyDirectionChart = new Chart(report, "CIS Survey Direction With Survey Date");
            surveyDirectionChart.LegendInfo.NameFontSize = 14f;
            var cisClass = new Chart(report, "CIS Severity");
            var cisIndication = new PGECISIndicationChartSeries(file, cisClass, hca, cisSkips);
            cisClass.Series.Add(cisIndication);

            var dcvgClass = new Chart(report, "DCVG Severity");
            var dcvgIndicationSeries = new PgeDcvgIndicationChartSeries(dcvgData, dcvgClass, isDcvg);
            dcvgClass.Series.Add(dcvgIndicationSeries);
            dcvgClass.LegendInfo.NameFontSize = 13f;

            var ecdaClassChart = new Chart(report, "ECDA Clas.");
            var ecdaClassSeries = new PGEDirectExaminationPriorityChartSeries(ecdaClassChart, cisIndication, dcvgIndicationSeries, ecdaReport.GetFullAmpData());
            ecdaClassChart.Series.Add(ecdaClassSeries);

            var surveyDirectionSeries = new SurveyDirectionWithDateSeries(file.GetDirectionWithDateData());
            surveyDirectionChart.Series.Add(surveyDirectionSeries);

            splitContainer.AddSelfSizedContainer(topGlobalXAxis);
            splitContainer.AddContainer(onOffGraph);
            if (ampData.Count > 0)
            {
                var ampGraph = new Graph(report);

                var minAmp = ampData.Min(v => v.Value);
                var maxAmp = ampData.Max(v => v.Value);
                if (maxAmp - minAmp <= 0.1)
                {
                    minAmp = maxAmp - (0.1 * maxAmp);
                    maxAmp = maxAmp + (0.1 * maxAmp);
                }
                var diff = maxAmp - minAmp;
                var yBuffer = diff * 0.1;
                var minY = minAmp - yBuffer;
                var maxY = maxAmp + yBuffer;
                var ySize = maxY - minY;
                ampGraph.YAxesInfo.Y2IsEnabled = false;
                ampGraph.YAxesInfo.Y1Title = "mA";
                ampGraph.YAxesInfo.Y1IsInverted = false;
                ampGraph.YAxesInfo.Y1MinimumValue = minY;
                ampGraph.YAxesInfo.Y1MaximumValue = maxY;
                ampGraph.YAxesInfo.MajorGridlines.Offset = ySize / 3;
                ampGraph.YAxesInfo.MinorGridlines.IsEnabled = false;
                ampGraph.LegendInfo.Name = "PCM Data";
                ampGraph.YAxesInfo.Y1LabelFormat = "F0";

                var ampsLine = new GraphSeries("PCM Data (mA)", ampData)
                {
                    LineColor = Colors.Blue,
                    PointShape = GraphSeries.Shape.Circle,
                    PointColor = Colors.Blue,
                    ShapeRadius = 2,
                    MaxDrawDistance = 130,
                    IsDrawnInLegend = false,
                    SkipFootages = pcmSkips?.Footages.Select(f => f.Footage).ToList()
                };
                ampGraph.Series.Add(ampsLine);
                splitContainer.AddContainerPercent(ampGraph, 0.15);
                var pcmDirectionChart = new Chart(report, "PCM Direction Away From TX With Survey Date");
                pcmDirectionChart.LegendInfo.NameFontSize = 14f;
                var pcmDirectionSeries = new SurveyDirectionWithDateSeries(ecdaReport.GetAmpDirectionData());
                pcmDirectionChart.Series.Add(pcmDirectionSeries);
                splitContainer.AddSelfSizedContainer(pcmDirectionChart);
            }
            splitContainer.AddSelfSizedContainer(ecdaClassChart);

            //TODO: Add Region chart to graph.
            //var regionChart = new Chart(report, "Regions");
            //var regionSeries = new ChartSeries(ecdaClassChart, cisIndication, dcvgIndicationSeries, ecdaReport.GetFullAmpData());
            //regionChart.Series.Add(regionSeries);

            splitContainer.AddSelfSizedContainer(surveyDirectionChart);
            splitContainer.AddSelfSizedContainer(bottomGlobalXAxis);
            report.Container = splitContainer;

            var cisIndicationExcelData = file.GetTabularData(new List<(string Name, List<(double Footage, double Value)>)>
            {
                ("200 (100 US & 100 DS) Foot Average", cisIndication.Averages),
                ("Used Averge for Baseline Footage", cisIndication.UsedBaselineFootages),
                ("Used Baseline", cisIndication.Baselines),
                ("PCM Data (mA)", ampData)
            });
            var baselineFolder = await outputFolder.CreateFolderAsync("CIS Baseline Data", CreationCollisionOption.OpenIfExists);
            await CreateExcelFile($"{folderName} CIS Baseline Data", new List<(string Name, string Data)>() { ("Baseline Data", cisIndicationExcelData) }, baselineFolder);
            if (ampData.Count > 0)
            {
                var pcmFolder = await outputFolder.CreateFolderAsync("PCM Percent Change Data", CreationCollisionOption.OpenIfExists);
                await CreateExcelFile($"{folderName} PCM Percent Change Data", file.PcmCalcOutput, pcmFolder);
            }

            var surveyLength = onData.Last().Footage;
            var pages = report.PageSetup.GetAllPages(0, surveyLength);
            var graphFolder = outputFolder;
            if (GraphsInSubFolder)
                graphFolder = await outputFolder.CreateFolderAsync("Graphs", CreationCollisionOption.OpenIfExists);
            for (var i = 0; i < pages.Count; ++i)
            {
                var page = pages[i];
                var pageString = $"{i + 1}".PadLeft(3, '0');
                var image = report.GetImage(page, 300);
                var imageFileName = $"{folderName} Page {pageString}.png";
                if (pages.Count == 1)
                    imageFileName = $"{folderName} Graph.png";
                var imageFile = await graphFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await image.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                }
            }

            var areas = ecdaClassSeries.GetUpdatedReportQ();
            var lastArea = areas.Last();
            if (lastArea.End == lastArea.Start && string.IsNullOrWhiteSpace(lastArea.Comments))
            {
                areas.RemoveAt(areas.Count - 1);
            }
            var uniqueRegions = hca.GetUniqueRegions();
            uniqueRegions.Sort();
            var output = new StringBuilder();
            var extrapolatedDepth = new List<(double, double)>();
            double curFoot;
            double curExtrapolatedFoot = 0;
            for (var i = 0; i < depthData.Count; ++i)
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
            foreach (var (startFoot, endFoot, cisSeverity, dcvgSeverity, thirdToolSeverity, region, priority, reason) in areas)
            {
                var depthInArea = extrapolatedDepth.Where(value => value.Item1 >= startFoot && value.Item1 <= endFoot);
                var minDepth = depthInArea.Count() != 0 ? depthInArea.Min(value => value.Item2) : -1;
                var (startMp, endMp) = hca.GetMpForRegion(region);
                var name = hca.Name;
                if (region.IsBuffer)
                    name = name + " Buffer";
                output.Append($"{name}\t{hca.LineName}\t{startMp}\t{endMp}\t");
                var length = endFoot - startFoot;
                var minDepthString = minDepth == -1 ? "" : minDepth.ToString("F0");
                output.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{region.ReportQName}\t{length:F0}\t{minDepthString}\t");
                var startGps = file.GetClosesetGps(startFoot);
                output.Append($"{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t");
                var endGps = file.GetClosesetGps(endFoot);
                var firstTime = region.FirstTimeString;
                //if (region.IsBuffer) // Is Buffer First Time N/A
                //    firstTime = "N/A";
                output.Append($"{endGps.Latitude:F8}\t{endGps.Longitude:F8}\t{firstTime}\t");
                if (reason.Contains("SKIP."))
                {
                    output.AppendLine($"NT\tNT\tNT\tNT\t{region.LongSkipReason}");
                    skipReport.Append($"{ToStationing(startFoot)}\t{ToStationing(endFoot)}\t{Math.Max(endFoot - startFoot, 1):F0}\t");
                    skipReport.Append($"{startGps.Latitude:F8}\t{startGps.Longitude:F8}\t");
                    skipReport.AppendLine($"{endGps.Latitude:F8}\t{endGps.Longitude:F8}\t{region.ShortSkipReason}");
                }
                else
                {
                    var thirdToolValue = thirdToolSeverity.GetDisplayName();
                    if (!region.Name.Contains("P"))
                        thirdToolValue = "NT";
                    if (IsPge)
                    {
                        output.AppendLine($"{cisSeverity.GetDisplayName()}\t{dcvgSeverity.GetDisplayName()}\t{thirdToolValue}\t{PriorityDisplayName(priority)}\t{reason.Replace("..", ".")}");
                    }
                    else
                    {
                        var (mostPositiveOn, mostPositiveOff) = file.GetMostPositive(startFoot, endFoot).Value;
                        output.AppendLine($"{cisSeverity.GetDisplayName()}\t{dcvgSeverity.GetDisplayName()}\t{thirdToolValue}\t{PriorityDisplayName(priority)}\t{reason.Replace("..", ".")}\t{mostPositiveOn:F3}\t{mostPositiveOff:F3}");
                    }
                }
            }
            ReportQ += output.ToString();
            var shapefileFolder = await outputFolder.CreateFolderAsync("Shapefiles", CreationCollisionOption.OpenIfExists);
            var googleShapefileFolder = await outputFolder.CreateFolderAsync("Google Earth", CreationCollisionOption.OpenIfExists);
            var cisShapeFileStringBuilder = new StringBuilder();
            foreach (var line in ecdaClassSeries.CISShapeFileOutput)
            {
                var lineString = string.Join('\t', line);
                cisShapeFileStringBuilder.AppendLine(lineString);
            }

            if (!IsPge)
            {
                var sempraShapeFileTest = new ShapefileData($"{folderName} Shapefile", ecdaClassSeries.SempraCISShapeFileOutput, false);
                await sempraShapeFileTest.WriteToFolder(outputFolder);
                var dcvgKmlFile = new KmlFile($"{folderName} DCVG Shapefile", dcvgData.Select(d => (d.Item1, d.Item3, "DCVG Indication")).ToList());
                await dcvgKmlFile.WriteToFile(googleShapefileFolder);
            }

            var cisShapeFileTest = new ShapefileData($"{folderName} CIS Shapefile", ecdaClassSeries.CISShapeFileOutput);
            await cisShapeFileTest.WriteToFolder(shapefileFolder);

            //#region soil res
            var soilResShapeData = new List<string[]>
            {
                new string[]
                {
                    "LABEL",
                    "LATITUDE",
                    "LONGITUDE"
                }
            };
            var soilResKmlData = new List<(double Footage, BasicGeoposition Gps, string Comment)>();
            foreach (var (soilDataGps, soilLabel) in soilRes)
            {
                soilResShapeData.Add(new string[]
                {
                    soilLabel,
                    soilDataGps.Latitude.ToString("F8"),
                    soilDataGps.Longitude.ToString("F8")
                });
                soilResKmlData.Add((0, soilDataGps, soilLabel));
            }
            if (soilRes.Count > 0)
            {
                var soilResShapefile = new ShapefileData($"{folderName} Soil Res Shapefile", soilResShapeData);
                await soilResShapefile.WriteToFolder(shapefileFolder);

                var soilResKml = new KmlFile($"{folderName} Soil Res Shapefile", soilResKmlData);
                await soilResKml.WriteToFile(googleShapefileFolder);
            }
            //#endregion

            var cisKmlFileTest = new KmlFile($"{folderName} CIS Shapefile", file.GetCisKmlData());
            await cisKmlFileTest.WriteToFile(googleShapefileFolder);



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
                await depthKmlFileTest.WriteToFile(googleShapefileFolder);
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

            var pcmShapeFileStringBuilder = new StringBuilder();
            foreach (var line in ecdaClassSeries.AmpsShapeFileOutput)
            {
                var lineString = string.Join('\t', line);
                dcvgShapeFileStringBuilder.AppendLine(lineString);
            }
            if (ampLabels.Count > 0)
            {
                var pcmShapeFileTest = new ShapefileData($"{folderName} PCM Shapefile", ecdaClassSeries.AmpsShapeFileOutput);
                await pcmShapeFileTest.WriteToFolder(shapefileFolder);
                var pcmKmlFileTest = new KmlFile($"{folderName} PCM Shapefile", ecdaClassSeries.GetAmpKmlData());
                await pcmKmlFileTest.WriteToFile(googleShapefileFolder);
            }

            var skipReportString = skipReport.ToString();
            var testStation = file.GetTestStationData();
            var depthException = new StringBuilder();
            for (var i = 0; i < extrapolatedDepth.Count; ++i)
            {
                (curFoot, curDepth) = extrapolatedDepth[i];
                if (curDepth < MinDepth)
                {
                    var start = i;
                    var curIndex = i;
                    var minDepth = curDepth;
                    while (curDepth < MinDepth && curIndex != extrapolatedDepth.Count)
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
                if (curDepth > MaxDepth)
                {
                    var start = i;
                    var curIndex = i;
                    var max = curDepth;
                    while (curDepth > MaxDepth && curIndex != extrapolatedDepth.Count)
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
            if (double.TryParse(hcaStartMp, out var hcaStartMpDouble))
            {
                hcaStartMp = $"{hcaStartMpDouble:F4}";
            }
            if (double.TryParse(hcaEndMp, out var hcaEndMpDouble))
            {
                hcaEndMp = $"{hcaEndMpDouble:F4}";
            }
            //var reportLLengths = ecdaReport.GetActualReadFootage();
            var reportLLengths2 = GetActualReadFootage(areas);
            var reportLLengthsToolThree = GetActualThirdToolFootage(areas);
            var reportLString = $"Indirect Inspection:\tCIS\t{indicationLabel}\t{uniqueRegionsString}\t{hcaStartMp}\t{hcaEndMp}\t{hca.LineName}\t{"HCA " + hca.Name}\nLength (feet)\t{reportLLengths2[0]}\t{reportLLengths2[0]}\t{reportLLengthsToolThree[0]}\n\t";
            var reportLNext = "Length (feet)\t";
            for (var i = 1; i <= 4; ++i)
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
            var reportsFolder = outputFolder;
            if (ReportsInSubFolder)
                reportsFolder = await outputFolder.CreateFolderAsync("Reports", CreationCollisionOption.OpenIfExists);
            var outputFile = await reportsFolder.CreateFileAsync($"{folderName} Reports.xlsx", CreationCollisionOption.ReplaceExisting);
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

        private string ToStationing(double footage)
        {
            var hundred = (int)footage / 100;
            var tens = (int)footage % 100;
            return hundred.ToString().PadLeft(1, '0') + "+" + tens.ToString().PadLeft(2, '0');
        }

        private (double HcaStartFootage, double HcaEndFootage, double? BufferEndFootage, double? BufferStartFootage) AddHcaComments(CombinedAllegroCisFile file, Hca hca, CommentReplacements replacements, bool setGps = true)
        {
            var startGap = hca.GetStartFootageGap();
            file.ShiftPoints(startGap);
            var hcaStartGps = hca.GetFirstNonSkipGps();
            var hcaEndGps = hca.GetLastNonSkipGps();
            var hasStartBuffer = hca.StartBuffer != null;
            var hasEndBuffer = hca.EndBuffer != null;
            var (hcaStartPoint, hcaStartDistance) = file.GetClosestPoint(hcaStartGps);

            var startBufferComment = " START OF BUFFER";
            var startHcaComment = " START OF HCA";
            var endBufferComment = " END OF BUFFER";
            var endHcaComment = " END OF HCA";
            if (replacements != null)
            {
                if (replacements.HasStartBufferReplacement)
                {
                    startBufferComment = " " + replacements.StartBufferReplacement.Trim();
                }
                if (replacements.HasStartHcaReplacement)
                {
                    startHcaComment = " " + replacements.StartHcaReplacement.Trim();
                }
                if (replacements.HasEndHcaReplacement)
                {
                    endHcaComment = " " + replacements.EndHcaReplacement.Trim();
                }
                if (replacements.HasEndBufferReplacement)
                {
                    endBufferComment = " " + replacements.EndBufferReplacement.Trim();
                }
            }

            if (hasStartBuffer)
            {
                if (BufferComments)
                    file.Points[file.HasStartSkip ? 1 : 0].Point.OriginalComment += startBufferComment;
            }
            else
            {
                hcaStartPoint = file.Points[file.HasStartSkip ? 1 : 0];
            }
            //var  hcaStartComment = (hasStartBuffer ? " END OF BUFFER" : "") + " START OF HCA";
            //hcaStartPoint = file.AddExtrapolatedPoint(hcaStartGps, hcaStartComment);
            var bufferEndIndex = file.Points.IndexOf(hcaStartPoint) - 1;
            double? bufferEndFootage = null;
            if (bufferEndIndex > 0)
            {
                if (BufferComments)
                {
                    var tempEndBufferComment = endBufferComment;
                    if (!string.IsNullOrWhiteSpace(file.Points[bufferEndIndex].Point.OriginalComment))
                        tempEndBufferComment += "+";
                    file.Points[bufferEndIndex].Point.OriginalComment += tempEndBufferComment;
                }
                bufferEndFootage = file.Points[bufferEndIndex].Footage;
            }
            if (HcaComments)
                hcaStartPoint.Point.OriginalComment += startHcaComment;
            if (setGps)
                hcaStartPoint.Point.GPS = hcaStartGps;

            var (hcaEndPoint, hcaEndDistance) = file.GetClosestPoint(hcaEndGps);
            if (hasEndBuffer)
            {
                if (BufferComments)
                {
                    file.Points[file.Points.Count - (file.HasEndSkip ? 2 : 1)].Point.OriginalComment += endBufferComment;
                }
            }
            else
            {
                hcaEndPoint = file.Points[file.Points.Count - (file.HasEndSkip ? 2 : 1)];
            }
            var bufferStartIndex = file.Points.IndexOf(hcaEndPoint) + 1;
            double? bufferStartFootage = null;
            if (bufferStartIndex < file.Points.Count - 1)
            {
                var tempStartBufferComment = startBufferComment;
                if(!string.IsNullOrWhiteSpace(file.Points[bufferStartIndex].Point.OriginalComment))
                    tempStartBufferComment += "+";
                if (BufferComments)
                    file.Points[bufferStartIndex].Point.OriginalComment += tempStartBufferComment;
                bufferStartFootage = file.Points[bufferStartIndex].Footage;
            }
            if (setGps)
                hcaEndPoint.Point.GPS = hcaEndGps;
            if (HcaComments)
                hcaEndPoint.Point.OriginalComment += endHcaComment;
            //var hcaEndComment = " END OF HCA" + (hasEndBuffer ? " START OF BUFFER" : "");
            //hcaEndPoint = file.AddExtrapolatedPoint(hcaEndGps, hcaEndComment);
            return (hcaStartPoint.Footage, hcaEndPoint.Footage, bufferEndFootage, bufferStartFootage);
        }


        private List<AllegroCISFile> GetUniqueFiles(List<AllegroCISFile> files)
        {
            files.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));
            for (var i = 0; i < files.Count; ++i)
            {
                var curFile = files[i];
                for (var j = i + 1; j < files.Count; ++j)
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
                var (_, dist, acvgExtrapFoot, _, gps) = cisCombinedData.AlignPoint(readGps);
                var region = hca.GetClosestRegion(gps);
                if (region.ShouldSkip)
                    continue;
                output.Add((acvgExtrapFoot, read, readGps));
            }
            return output;
        }

        private List<(double, double, BasicGeoposition)> GetAlignedDcvgData(AllegroCISFile dcvgFile, List<(double, BasicGeoposition)> cisCombinedData, Hca hca)
        {
            var lastGps = new BasicGeoposition();
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
            //var correction = 15.563025007672872650175335959592166719366374913056088;
            var output = new List<(BasicGeoposition Gps, double Read)>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var splitLine = line.Split(',');
                var lat = double.Parse(splitLine[0]);
                var lon = double.Parse(splitLine[1]);
                var read = double.Parse(splitLine[2]); //- correction;
                var gps = new BasicGeoposition() { Latitude = lat, Longitude = lon };
                output.Add((gps, read));
            }
            return output;
        }

        private List<int> GetActualReadFootage(List<(double Start, double End, PGESeverity CisSeverity, PGESeverity DcvgSeverity, PGESeverity PcmSeverity, HcaRegion Region, int Overall, string Comments)> areas)
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

        private List<int> GetActualThirdToolFootage(List<(double Start, double End, PGESeverity CisSeverity, PGESeverity DcvgSeverity, PGESeverity PcmSeverity, HcaRegion Region, int Overall, string Comments)> areas)
        {
            var output = Enumerable.Repeat(0, 5).ToList();
            foreach (var area in areas)
            {
                if (area.Comments.Contains("SKIP") || !area.Region.Name.Contains("P")) continue;
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
                if (placemark.GetObjects("name").Count == 0)
                    continue;
                var name = placemark.GetObjects("name")[0].Value;
                if (name.Contains(' '))
                {
                    name = name.Substring(0, name.IndexOf(' '));
                }
                var coordsObjs = placemark.GetObjects("coordinates");
                foreach (var coordsObj in coordsObjs)
                {
                    var gps = new List<BasicGeoposition>();
                    if (string.IsNullOrWhiteSpace(coordsObj.Value.Trim()))
                        continue;
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

        private void buttonDoNewWork_Click(object sender, RoutedEventArgs e)
        {
            MakeIITGraphsFromNew();
        }
    }
}
