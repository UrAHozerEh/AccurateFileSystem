using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
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
                if (newFile != null)
                    newFiles.Add(newFile);
            }
            newFiles.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));
            for(int i = 0; i < newFiles.Count; ++i)
            {
                var curFile = newFiles[i];
                for(int j = i+1; j < newFiles.Count; ++j)
                {
                    var nextFile = newFiles[j];
                    if(curFile.Name != nextFile.Name)
                        break;
                    if(curFile.IsEquivalent(nextFile))
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
