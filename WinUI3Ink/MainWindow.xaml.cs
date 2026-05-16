using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Windows.Storage.Pickers;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using WinUI3Ink.Controls;

namespace WinUI3Ink;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNewWindow(object sender, RoutedEventArgs e)
    {
        var size = AppWindow.Size;
        DirectN.Extensions.Utilities.TaskUtilities.RunWithNewSTAThread(() =>
        {
            Thread.CurrentThread.IsBackground = true;

            var dq = DispatcherQueueController.CreateOnCurrentThread();
            WindowsXamlManager.InitializeForCurrentThread();

            var childWindow = new MainWindow();
            childWindow.AppWindow.ResizeClient(new SizeInt32(size.Width * 2 / 3, size.Height * 2 / 3));
            childWindow.Activate();

            // we need this for InkCanvas to work properly (it uses async / await calls)
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dq.DispatcherQueue));

            dq.DispatcherQueue.RunEventLoop();
        });
    }

    private async Task OnSave(InkCanvas canvas)
    {
        var size = canvas.ActualSize;
        // pass a color to force a background, otherwise strokes will be drawn on transparent background.
        using var bmp = await canvas.GetBitmap((uint)size.X, (uint)size.Y);//, Microsoft.UI.Colors.White);

        var picker = new FileSavePicker(AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            DefaultFileExtension = ".png",
            SuggestedFileName = "InkCanvasImage.png"
        };
        picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" }); // don't change by [...]

        var result = await picker.PickSaveFileAsync().AsTask();
        if (result == null)
            return;

        using var stream = new FileStream(result.Path, FileMode.Create, FileAccess.Write).AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bmp);
        await encoder.FlushAsync();
    }

    private static async Task Clear(InkCanvas canvas)
    {
        var container = await canvas.GetStrokeContainer();
        container!.Clear();
    }

    private void OnSaveLeft(object sender, RoutedEventArgs e) => _ = OnSave(blackOnWhiteCanvas);
    private void OnSaveRight(object sender, RoutedEventArgs e) => _ = OnSave(whiteOnBlackCanvas);
    private async void OnClearLeft(object sender, RoutedEventArgs e) => _ = Clear(blackOnWhiteCanvas);
    private async void OnClearRight(object sender, RoutedEventArgs e) => _ = Clear(whiteOnBlackCanvas);
}
