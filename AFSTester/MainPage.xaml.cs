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
        private HttpBaseProtocolFilter Filter;
        private HttpClient Client;
        public MainPage()
        {
            this.InitializeComponent();
            Filter = new HttpBaseProtocolFilter();
            //Filter.ServerCredential = new Windows.Security.Credentials.PasswordCredential("https://apps-secure.phoenix.gov", email, password);
            Client = new HttpClient(Filter);
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
                    var graph = new Graph(report.DrawArea, null, report);
                    var on = new GraphSeries("On", allegroFile.GetDoubleData("On"));
                    var off = new GraphSeries("Off", allegroFile.GetDoubleData("Off"));
                    graph.Series.Add(on);
                    graph.Series.Add(off);
                    report.Container = graph;
                    var images = report.GetImages(allegroFile.StartFootage, allegroFile.EndFootage);
                    for (int i = 0; i < images.Count; ++i)
                    {
                        var page = $"{i + 1}".PadLeft(3, '0');
                        var image = images[i];
                        var imageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync($"Test Page {page}" + ".bmp", CreationCollisionOption.ReplaceExisting);
                        using (var stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await image.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Bmp);
                        }
                    }
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
            if (Windows.Graphics.Printing.PrintManager.IsSupported())
            {
                var report = new GraphicalReport();
                await Windows.Graphics.Printing.PrintManager.ShowPrintUIAsync();
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
