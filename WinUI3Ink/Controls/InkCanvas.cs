using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DirectN;
using DirectN.Extensions.Com;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Input.Inking;

namespace WinUI3Ink.Controls;

// inspired by https://github.com/microsoft/microsoft-ui-xaml/blob/main/src/controls/dev/InkCanvas/InkCanvas.cpp
public sealed partial class InkCanvas : Control
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(Color),
        typeof(InkCanvas),
        new PropertyMetadata(Colors.Black, (d, e) =>
        {
            var atts = new InkDrawingAttributes { Color = (Color)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty IsInputEnabledProperty = DependencyProperty.Register(
        nameof(IsInputEnabled),
        typeof(bool),
        typeof(InkCanvas),
        new PropertyMetadata(true, (d, e) =>
        {
            var value = (bool)e.NewValue;
            _ = ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.IsInputEnabled = value);
        }));

    public static readonly DependencyProperty DrawAsHighlighterProperty = DependencyProperty.Register(
        nameof(DrawAsHighlighter),
        typeof(bool),
        typeof(InkCanvas),
        new PropertyMetadata(false, (d, e) =>
        {
            var atts = new InkDrawingAttributes { DrawAsHighlighter = (bool)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty FitToCurveProperty = DependencyProperty.Register(
        nameof(FitToCurve),
        typeof(bool),
        typeof(InkCanvas),
        new PropertyMetadata(true, (d, e) =>
        {
            var atts = new InkDrawingAttributes { FitToCurve = (bool)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty IgnorePressureProperty = DependencyProperty.Register(
        nameof(IgnorePressure),
        typeof(bool),
        typeof(InkCanvas),
        new PropertyMetadata(false, (d, e) =>
        {
            var atts = new InkDrawingAttributes { IgnorePressure = (bool)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty IgnoreTiltProperty = DependencyProperty.Register(
        nameof(IgnoreTilt),
        typeof(bool),
        typeof(InkCanvas),
        new PropertyMetadata(false, (d, e) =>
        {
            var atts = new InkDrawingAttributes { IgnoreTilt = (bool)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty PenTipProperty = DependencyProperty.Register(
        nameof(PenTip),
        typeof(PenTipShape),
        typeof(InkCanvas),
        new PropertyMetadata(PenTipShape.Circle, (d, e) =>
        {
            var atts = new InkDrawingAttributes { PenTip = (PenTipShape)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty PenTipTransformProperty = DependencyProperty.Register(
        nameof(PenTipTransform),
        typeof(Matrix3x2),
        typeof(InkCanvas),
        new PropertyMetadata(Matrix3x2.Identity, (d, e) =>
        {
            var atts = new InkDrawingAttributes { PenTipTransform = (Matrix3x2)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(Size),
        typeof(InkCanvas),
        new PropertyMetadata(new Size(2, 2), (d, e) =>
        {
            var atts = new InkDrawingAttributes { Size = (Size)e.NewValue };
            ((InkCanvas)d).QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
        }));

    private readonly static ThreadLocal<ThreadData> _threadData = new(() => new());

    private IComObject<IInkPresenterDesktop>? _inkPresenterDesktop;
    private ComObject<IDCompositionVisual>? _inkRootVisual;
    private HWND _hostHwnd;
    private TargetData? _targetData;
    private InkPresenter? _presenter;

    public InkCanvas()
    {
        DefaultStyleKey = typeof(InkCanvas);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public bool IsInputEnabled { get => (bool)GetValue(IsInputEnabledProperty); set => SetValue(IsInputEnabledProperty, value); }
    public bool DrawAsHighlighter { get => (bool)GetValue(DrawAsHighlighterProperty); set => SetValue(DrawAsHighlighterProperty, value); }
    public bool FitToCurve { get => (bool)GetValue(FitToCurveProperty); set => SetValue(FitToCurveProperty, value); }
    public bool IgnorePressure { get => (bool)GetValue(IgnorePressureProperty); set => SetValue(IgnorePressureProperty, value); }
    public bool IgnoreTilt { get => (bool)GetValue(IgnoreTiltProperty); set => SetValue(IgnoreTiltProperty, value); }
    public PenTipShape PenTip { get => (PenTipShape)GetValue(PenTipProperty); set => SetValue(PenTipProperty, value); }
    public Matrix3x2 PenTipTransform { get => (Matrix3x2)GetValue(PenTipTransformProperty); set => SetValue(PenTipTransformProperty, value); }
    public Size Size { get => (Size)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public Task<InkStrokeContainer?> GetStrokeContainer() => QueueInkPresenterWorkItem(presenter => presenter.StrokeContainer);
    public Task<InkInputProcessingConfiguration?> GetInputProcessingConfiguration() => QueueInkPresenterWorkItem(presenter => presenter.InputProcessingConfiguration);
    public Task<InkUnprocessedInput?> GetUnprocessedInput() => QueueInkPresenterWorkItem(presenter => presenter.UnprocessedInput);
    public Task<InkStrokeInput?> GetStrokeInput() => QueueInkPresenterWorkItem(presenter => presenter.StrokeInput);

    public Task<InkDrawingAttributes?> CopyDefaultDrawingAttributes() => QueueInkPresenterWorkItem(presenter => presenter.CopyDefaultDrawingAttributes());
    public Task<InkSynchronizer?> ActivateCustomDrying() => QueueInkPresenterWorkItem(presenter => presenter.ActivateCustomDrying());
    public Task<bool> SetPredefinedConfiguration(InkPresenterPredefinedConfiguration configuration) => QueueInkPresenterWorkItem(presenter => presenter.SetPredefinedConfiguration(configuration));
    public Task<bool> UpdateDefaultDrawingAttributes(InkDrawingAttributes atts)
    {
        ArgumentNullException.ThrowIfNull(atts);
        return QueueInkPresenterWorkItem(presenter => presenter.UpdateDefaultDrawingAttributes(atts));
    }

    // all calls to InkPresenter must be marshaled through the QueueInkPresenterWorkItem to ensure they are executed on the correct thread and after the presenter is created
    public Task<bool> QueueInkPresenterWorkItem(Action<InkPresenter> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // we fail gracefully if the thread data is not available or has been disposed, which can happen if the control is being unloaded
        // or if the caller is trying to access the presenter early
        var threadData = _threadData.Value;
        if (threadData is null)
            return Task.FromResult(false);

        if (threadData.IsDisposed)
            return Task.FromResult(false);

        var presenter = _presenter;
        if (presenter != null)
            return threadData.InkDesktopHost.Run(() =>
            {
                action(presenter);
                return true;
            });

        return Task.Run(async () =>
        {
            await CreateInkPresenterAsync(threadData);
            return await threadData.InkDesktopHost.Run(() =>
            {
                var presenter = _presenter;
                if (presenter != null)
                {
                    action(presenter);
                    return true;
                }
                return false;
            });
        });
    }

    public Task<T?> QueueInkPresenterWorkItem<T>(Func<InkPresenter, T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var threadData = _threadData.Value;
        if (threadData is null)
            return Task.FromResult<T?>(default);

        if (threadData.IsDisposed)
            return Task.FromResult<T?>(default);

        var presenter = _presenter;
        if (presenter != null)
        {
            var ret = threadData.InkDesktopHost.Run(() => func(presenter));
            return ret!;
        }

        return Task.Run(async () =>
        {
            await CreateInkPresenterAsync(threadData);
            return await threadData.InkDesktopHost.Run(() =>
            {
                var presenter = _presenter;
                if (presenter != null)
                    return func(presenter);

                return default;
            });
        });
    }

    private HWND GetHwnd() => Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);

    private Task<bool> CreateInkPresenter()
    {
        _threadData.Value!.Get();

        // set the default drawing attributes in this thread
        var atts = new InkDrawingAttributes
        {
            Color = Color,
            DrawAsHighlighter = DrawAsHighlighter,
            FitToCurve = FitToCurve,
            IgnorePressure = IgnorePressure,
            IgnoreTilt = IgnoreTilt,
            PenTip = PenTip,
            Size = Size,
            PenTipTransform = PenTipTransform
        };

        return QueueInkPresenterWorkItem(presenter =>
        {
            presenter.InputDeviceTypes = Windows.UI.Core.CoreInputDeviceTypes.Mouse | Windows.UI.Core.CoreInputDeviceTypes.Pen | Windows.UI.Core.CoreInputDeviceTypes.Touch;
            presenter.UpdateDefaultDrawingAttributes(atts);
        });
    }

    private async Task CreateInkPresenterAsync(ThreadData threadData)
    {
        if (_presenter != null)
            return;

        _presenter = await threadData.InkDesktopHost.Run(() =>
        {
            threadData.InkDesktopHost.Object.CreateInkPresenter(typeof(IInkPresenterDesktop).GUID, out var presenterPtr).ThrowOnError();
            _inkPresenterDesktop = ComObject.FromPointer<IInkPresenterDesktop>(presenterPtr);
            return InkPresenter.FromAbi(presenterPtr);
        });
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await CreateInkPresenter();

        AttachToVisualLink();

        XamlRoot.Changed += OnXamlRootChanged;
        SizeChanged += OnSizeChanged;

        PositionInkVisual();
    }

    private void AttachToVisualLink()
    {
        var threadData = _threadData.Value;
        if (threadData == null)
            return;

        var hwnd = GetHwnd();
        if (_hostHwnd == hwnd)
            return;

        if (_hostHwnd != 0)
        {
            DetachFromVisualLink();
        }
        _hostHwnd = hwnd;

        threadData.CompositionDevice.Object.CreateVisual(out var visualObj).ThrowOnError();
        _inkRootVisual = new ComObject<IDCompositionVisual>(visualObj);

        QueueInkPresenterWorkItem(presenter =>
        {
            ComObject.WithComInstance(_inkRootVisual, unk =>
            {
                _inkPresenterDesktop!.Object.SetRootVisual(unk, 0).ThrowOnError();
            });
        });

        AttachToCompositionTarget(threadData);
    }

    private void AttachToCompositionTarget(ThreadData threadData)
    {
        var hwnd = Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
        _targetData = TargetData.Get(threadData, hwnd);

        _targetData.TargetRootVisual!.Object.AddVisual(_inkRootVisual!.Object, true, null).ThrowOnError();

        LayoutUpdated += (s, e) => PositionInkVisual();
    }

    private void DetachFromVisualLink()
    {
        DetachFromCompositionTarget();
        _inkRootVisual?.Dispose();
        _inkRootVisual = null;
        _hostHwnd = 0;

        _threadData.Value?.CompositionDevice.Object.Commit().ThrowOnError();
    }

    private void DetachFromCompositionTarget()
    {
        var targetData = _targetData;
        if (targetData == null)
            return;

        var inkRootVisual = _inkRootVisual;
        if (inkRootVisual != null)
        {
            targetData.TargetRootVisual?.Object.RemoveVisual(inkRootVisual.Object).ThrowOnError();
        }

        _targetData?.Return();
        _targetData = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateInkPresenterSize();
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateInkPresenterSize();

    private void PositionInkVisual()
    {
        var inkRootVisual = _inkRootVisual;
        if (inkRootVisual == null)
            return;

        var transformer = TransformToVisual(null);
        var rect = transformer.TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));

        var scaleRect = transformer.TransformBounds(new Rect(0, 0, 1, 1));

        var rootScale = XamlRoot.RasterizationScale;
        inkRootVisual.Object.SetOffsetX((float)(scaleRect.X * rootScale));
        inkRootVisual.Object.SetOffsetY((float)(scaleRect.Y * rootScale));

        var visualTransform = new D2D_MATRIX_3X2_F(
            (float)scaleRect.Width, 0,
            0, (float)scaleRect.Height,
            0, 0);

        if (FlowDirection == FlowDirection.RightToLeft)
        {
            visualTransform._11 *= -1;
            visualTransform._31 = (float)(rect.Width * rootScale);
        }

        inkRootVisual.Object.SetTransform(visualTransform);

        _threadData.Value!.CompositionDevice.Object.Commit().ThrowOnError();

        UpdateInkPresenterSize();
    }

    private void UpdateInkPresenterSize()
    {
        var transformer = TransformToVisual(null);
        var rect = transformer.TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));

        var rootScale = XamlRoot.RasterizationScale;
        QueueInkPresenterWorkItem(presenter =>
        {
            _inkPresenterDesktop?.Object.SetSize((float)(rect.Width * rootScale), (float)(rect.Height * rootScale)).ThrowOnError();
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        XamlRoot.Changed -= OnXamlRootChanged;
        SizeChanged -= OnSizeChanged;

        DetachFromVisualLink();

        var ipd = Interlocked.Exchange(ref _inkPresenterDesktop, null);
        if (ipd != null)
        {
            _threadData.Value!.InkDesktopHost.Run(() => ipd.Dispose());
        }

        if (_threadData.IsValueCreated)
        {
            _threadData.Value?.Return();
        }
    }

    // per thread data for the ink presenter and composition device, we need to ensure they are created and used on the same thread, and disposed when the thread is no longer in use
    private sealed partial class ThreadData
    {
        private IComObject<IInkDesktopHost>? _inkDesktopHost;
        private IComObject<IDCompositionDevice>? _compositionDevice;

        public int Count { get; private set; }
        public bool IsDisposed => Count == 0;
        public IComObject<IInkDesktopHost> InkDesktopHost => _inkDesktopHost ?? throw new ObjectDisposedException(nameof(IInkDesktopHost));
        public IComObject<IDCompositionDevice> CompositionDevice => _compositionDevice ?? throw new ObjectDisposedException(nameof(IDCompositionDevice));

        public void Get()
        {
            Count++;
            if (Count == 1)
            {
                _inkDesktopHost = ComObject.CoCreate<IInkDesktopHost>(Constants.InkDesktopHost)!;

                Functions.DCompositionCreateDevice3(0, typeof(IDCompositionDevice).GUID, out var devicePtr).ThrowOnError();
                _compositionDevice = ComObject.FromPointer<IDCompositionDevice>(devicePtr)!;
            }
        }

        public void Return()
        {
            if (Count > 0)
            {
                Count--;
                if (Count == 0)
                {
                    _inkDesktopHost?.Dispose();
                    _compositionDevice?.Dispose();
                }
            }
        }
    }

    // per target (HWND) data for the composition target and root visual, we need to ensure they are shared across all threads that use the same target, and disposed when the target is no longer in use
    private sealed partial class TargetData(HWND hwnd)
    {
        private static readonly ConcurrentDictionary<HWND, TargetData> _targetDataByHwnd = new();

        private ComObject<IDCompositionVisual>? _targetRootVisual;
        private ComObject<IDCompositionTarget>? _compositionTarget;

        public int Count { get; private set; }
        public bool IsDisposed => Count == 0;
        public HWND Hwnd { get; } = hwnd;
        public IComObject<IDCompositionVisual>? TargetRootVisual => _targetRootVisual;

        public static TargetData Get(ThreadData threadData, HWND hwnd)
        {
            if (!_targetDataByHwnd.TryGetValue(hwnd, out var targetData))
            {
                targetData = new TargetData(hwnd);
                threadData.CompositionDevice.Object.CreateTargetForHwnd(hwnd, true, out var targetObj).ThrowOnError();
                targetData._compositionTarget = new ComObject<IDCompositionTarget>(targetObj)!;

                threadData.CompositionDevice.Object.CreateVisual(out var rootVisualObj).ThrowOnError();
                targetData._targetRootVisual = new ComObject<IDCompositionVisual>(rootVisualObj)!;
                targetData._compositionTarget.Object.SetRoot(targetData._targetRootVisual.Object).ThrowOnError();

                threadData.CompositionDevice.Object.Commit().ThrowOnError();

                targetData = _targetDataByHwnd.AddOrUpdate(hwnd, targetData, (key, oldValue) => oldValue);
            }

            targetData.Count++;
            return targetData;
        }

        public void Return()
        {
            if (Count > 0)
            {
                Count--;
                if (Count == 0)
                {
                    _targetDataByHwnd.TryRemove(Hwnd, out _);
                    _compositionTarget?.Dispose();
                    _targetRootVisual?.Dispose();
                }
            }
        }
    }
}

// helpers around IInkHostWorkItem.Invoke to run code on the ink desktop host thread and marshal exceptions back to the caller
internal static partial class InkDesktopHostExtensions
{
    public static Task Run(this IComObject<IInkDesktopHost> host, Action action) => Run(host.Object, action);
    public static Task Run(this IInkDesktopHost host, Action action)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(action);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new InkHostWorkItem(() =>
        {
            try
            {
                action();
                tcs.SetResult();
                return Constants.S_OK;
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                return Constants.E_FAIL;
            }
        });

        var hr = host.QueueWorkItem(item);
        if (hr.IsError)
        {
            tcs.SetException(new COMException("Failed to queue work item.", hr));
        }
        return tcs.Task;
    }

    public static Task<T> Run<T>(this IComObject<IInkDesktopHost> host, Func<T> func) => Run(host.Object, func);
    public static Task<T> Run<T>(this IInkDesktopHost host, Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(func);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new InkHostWorkItem(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
                return Constants.S_OK;
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                return Constants.E_FAIL;
            }
        });

        var hr = host.QueueWorkItem(item);
        if (hr.IsError)
        {
            tcs.SetException(new COMException("Failed to queue work item.", hr));
        }
        return tcs.Task;
    }

    [System.Runtime.InteropServices.Marshalling.GeneratedComClass]
    private sealed partial class InkHostWorkItem(Func<HRESULT> func) : IInkHostWorkItem, IAgileObject
    {
        HRESULT IInkHostWorkItem.Invoke() => func();
    }
}
