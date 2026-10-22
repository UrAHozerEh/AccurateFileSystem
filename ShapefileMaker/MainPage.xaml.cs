using System;
using System.Collections.Generic;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;


namespace ShapefileMaker;
/// <summary>
/// An empty page that can be used on its own or navigated to within a <see cref="Frame">.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void ImportClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {

    }

    private async void Import()
    {
        var errorDialog = new MessageDialog("", "Error");

        var filePicker = new FileOpenPicker();
        filePicker.FileTypeFilter.Add(".csv");
        var file = await filePicker.PickSingleFileAsync();
        if (file == null)
        {
            return;
        }


        var folderPicker = new FolderPicker();
        folderPicker.FileTypeFilter.Add(".csv");
        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder == null)
        {
            return;
        }

        var extension = file.FileType;
        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            errorDialog.Content = "File must be CSV";
            _ = await errorDialog.ShowAsync();
            return;
        }

        //var lines = file.GetLines();

    }


}
