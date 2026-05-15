using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.Graphics;

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

    private async void OnClearLeft(object sender, RoutedEventArgs e)
    {
        var container = await blackOnWhiteCanvas.GetStrokeContainer();
        container!.Clear();
    }

    private async void OnClearRight(object sender, RoutedEventArgs e)
    {
        var container = await whiteOnBlackCanvas.GetStrokeContainer();
        container!.Clear();
    }
}
