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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
            List<File> newFiles = new List<File>();

            foreach (var file in files)
            {
                var factory = new FileFactory(file);
                var newFile = await factory.GetFile();
                if (newFile is AllegroCISFile)
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
                    //var images = report.GetImages(allegroFile.StartFootage, allegroFile.EndFootage);
                    //for (int i = 0; i < images.Count; ++i)
                    //{
                        //var page = $"{i + 1}".PadLeft(3, '0');
                        //var image = images[i];
                        //var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"Test Page {page}" + ".png", CreationCollisionOption.ReplaceExisting);
                        //using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                        //{
                            //await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                        //}
                    //}
                }
                if (newFile != null)
                    newFiles.Add(newFile);
            }
            newFiles.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));
            for (int i = 0; i < newFiles.Count; ++i)
            {
                var curFile = newFiles[i];
                for (int j = i + 1; j < newFiles.Count; ++j)
                {
                    var nextFile = newFiles[j];
                    if (curFile.Name != nextFile.Name)
                        break;
                    if (curFile.IsEquivalent(nextFile))
                    {
                        newFiles.RemoveAt(j);
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
        }
    }
}
