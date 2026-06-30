using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using WinRT;
using static PanoramaViewer.DXInterop;

namespace PanoramaViewer;

internal readonly record struct RenderingFrameEventArgs(TimeSpan ElapsedTime);

internal abstract class DXRenderer : IDisposable
{
    private static readonly Guid IidDxgiFactory2 = new("50c83a1c-e072-4c48-87b0-3630fa36a6d0");

    private SwapChainPanel? _swapchainPanel;
    private IDXGISwapChain1? _swapchain;
    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private IDXGIDevice? _dxgiDevice;
    private ID3D11RenderTargetView? _renderTargetView;
    private ID3D11Texture2D? _depthStencilTexture;
    private ID3D11DepthStencilView? _depthStencilView;
    private IntPtr _d3dContextPtr;
    private IntPtr _swapChainPtr;
    private IntPtr _renderTargetViewPtr;
    private D3D11_VIEWPORT _viewport;
    private readonly object _renderSync = new();
    private readonly AutoResetEvent _renderRequested = new(false);
    private readonly ManualResetEvent _renderThreadStopped = new(true);
    private readonly ManualResetEvent _renderThreadShutdown = new(false);
    private readonly Stopwatch _renderStopwatch = Stopwatch.StartNew();
    private Thread? _renderThread;
    private DispatcherQueue? _dispatcherQueue;
    private TimeSpan _lastRenderTimestamp;
    private volatile bool _needsRender = true;
    private volatile bool _isInitialized;
    private volatile bool _renderHooked;
    private volatile bool _disposed;
    private Task? _initializationTask;

    protected SwapChainPanel? SwapChainPanel => _swapchainPanel;

    protected IDXGISwapChain1? SwapChain => _swapchain;

    protected ID3D11Device? D3DDevice => _d3dDevice;

    protected ID3D11DeviceContext? D3DContext => _d3dContext;

    protected ID3D11RenderTargetView? RenderTargetView => _renderTargetView;

    protected ID3D11DepthStencilView? DepthStencilView => _depthStencilView;

    protected D3D11_VIEWPORT Viewport => _viewport;

    protected bool IsInitialized => _isInitialized;

    protected bool IsDisposed => _disposed;

    protected object RenderSyncRoot => _renderSync;

    protected IntPtr D3DContextPtr => _d3dContextPtr;

    protected DispatcherQueue? DispatcherQueue => _dispatcherQueue;

    public event EventHandler<RenderingFrameEventArgs>? RenderingFrame;

    public void Attach(SwapChainPanel swapchainPanel)
    {
        if (_swapchainPanel == swapchainPanel)
        {
            Start();
            return;
        }

        DetachPanelEvents();
        _swapchainPanel = swapchainPanel;
        _dispatcherQueue = swapchainPanel.DispatcherQueue;
        _swapchainPanel.Loaded += OnLoaded;
        _swapchainPanel.Unloaded += OnUnloaded;
        _swapchainPanel.SizeChanged += OnSizeChanged;
        Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnhookRendering();
        DetachPanelEvents();
        ReleaseDeviceResources();
        GC.SuppressFinalize(this);
    }

    ~DXRenderer()
    {
        ReleaseDeviceResources();
    }

    public void RequestRender()
    {
        if (!_needsRender)
        {
            _lastRenderTimestamp = _renderStopwatch.Elapsed;
        }

        _needsRender = true;
        _renderRequested.Set();
    }

    protected abstract Task InitializeRendererResourcesAsync();

    protected abstract bool HasRendererResources();

    protected abstract void RenderFrame();

    protected abstract void ReleaseRendererResources();

    protected virtual void OnSwapChainResized()
    {
    }

    protected virtual float[] ClearColor => [0.0f, 0.0f, 0.0f, 1.0f];

    private void DetachPanelEvents()
    {
        if (_swapchainPanel is null)
        {
            return;
        }

        _swapchainPanel.Loaded -= OnLoaded;
        _swapchainPanel.Unloaded -= OnUnloaded;
        _swapchainPanel.SizeChanged -= OnSizeChanged;
        _swapchainPanel = null;
        _dispatcherQueue = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnhookRendering();
        ReleaseDeviceResources();
        _isInitialized = false;
        _initializationTask = null;
    }

    private async void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_swapchainPanel?.XamlRoot is null || _swapchainPanel.ActualWidth <= 0 || _swapchainPanel.ActualHeight <= 0)
        {
            return;
        }

        if (!_isInitialized)
        {
            await EnsureInitializedAsync();
            return;
        }

        CreateSizeDependentResources();
        OnSwapChainResized();
        RequestRender();
    }

    private void Start()
    {
        if (_disposed || _swapchainPanel is null)
        {
            return;
        }

        HookRendering();
    }

    private void HookRendering()
    {
        if (_renderHooked)
        {
            _ = EnsureInitializedAndRequestRenderAsync();
            return;
        }

        _renderThreadShutdown.Reset();
        _renderThreadStopped.Reset();
        _renderThread = new Thread(RenderLoop)
        {
            IsBackground = true,
            Name = $"{GetType().Name}-RenderLoop",
        };
        _renderThread.SetApartmentState(ApartmentState.MTA);
        _renderHooked = true;
        _renderThread.Start();
        _ = EnsureInitializedAndRequestRenderAsync();
    }

    private void UnhookRendering()
    {
        if (!_renderHooked)
        {
            return;
        }

        _renderHooked = false;
        _renderThreadShutdown.Set();
        _renderRequested.Set();
        if (_renderThread is not null && _renderThread.IsAlive && _renderThread != Thread.CurrentThread)
        {
            _renderThreadStopped.WaitOne();
        }

        _renderThread = null;
        _renderThreadStopped.Reset();
    }

    private async Task EnsureInitializedAndRequestRenderAsync()
    {
        await EnsureInitializedAsync();
        if (!_disposed && _renderHooked && _isInitialized)
        {
            RequestRender();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_disposed || _isInitialized)
        {
            return;
        }

        if (_initializationTask is not null)
        {
            await _initializationTask;
            return;
        }

        if (_swapchainPanel?.XamlRoot is null || _swapchainPanel.ActualWidth <= 0 || _swapchainPanel.ActualHeight <= 0)
        {
            return;
        }

        _initializationTask = InitializeAsyncCore();

        try
        {
            await _initializationTask;
            _isInitialized = true;
        }
        finally
        {
            _initializationTask = null;
        }
    }

    private async Task InitializeAsyncCore()
    {
        CreateDeviceResources();
        CreateSwapChain();
        await InitializeRendererResourcesAsync();
        CreateSizeDependentResources();
        OnSwapChainResized();
    }

    private unsafe void CreateDeviceResources()
    {
        lock (_renderSync)
        {
            if (_d3dDevice is not null && _d3dContext is not null)
            {
                return;
            }

            var featureLevels = new ReadOnlySpan<D3D_FEATURE_LEVEL>([
                D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
                D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
                D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
                D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_3,
                D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_2,
                D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1,
            ]);
            var flags = D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT;
            var result = Windows.Win32.PInvoke.D3D11CreateDevice(
                null,
                D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
                default,
                flags,
                featureLevels,
                7,
                out _d3dDevice,
                out _,
                out _d3dContext);

            ThrowIfFailed(result, "Failed to create the D3D11 device.");
            CaptureComInterfacePointer(ref _d3dContextPtr, _d3dContext, typeof(ID3D11DeviceContext).GUID);
        }
    }

    private unsafe void CreateSwapChain()
    {
        lock (_renderSync)
        {
            if (_swapchain is not null || _swapchainPanel is null || _d3dDevice is null)
            {
                return;
            }

            DXGI_SWAP_CHAIN_DESC1 swapChainDesc = new()
            {
                Width = (uint)Math.Max(1, Math.Ceiling(_swapchainPanel.ActualWidth)),
                Height = (uint)Math.Max(1, Math.Ceiling(_swapchainPanel.ActualHeight)),
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                Stereo = new Windows.Win32.Foundation.BOOL(false),
                SampleDesc = new DXGI_SAMPLE_DESC
                {
                    Count = 1,
                    Quality = 0,
                },
                BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                Scaling = 0,
                SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
                AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE,
                Flags = 0,
            };

            ReleaseComObject(ref _dxgiDevice);
            _dxgiDevice = _d3dDevice.As<IDXGIDevice>();
            _dxgiDevice.GetAdapter(out IDXGIAdapter dxgiAdapter);

            Guid factoryGuid = IidDxgiFactory2;
            dxgiAdapter.GetParent(&factoryGuid, out object? factoryObject);
            IDXGIFactory2 dxgiFactory = (IDXGIFactory2)factoryObject!;

            dxgiFactory.CreateSwapChainForComposition(_d3dDevice, &swapChainDesc, null, out IDXGISwapChain1 swapchain);
            _swapchain = swapchain;
            CaptureComInterfacePointer(ref _swapChainPtr, _swapchain, typeof(IDXGISwapChain).GUID);

            ISwapChainPanelNative panelNative = _swapchainPanel.As<ISwapChainPanelNative>();
            panelNative.SetSwapChain(swapchain);

            Marshal.ReleaseComObject(dxgiFactory);
            Marshal.ReleaseComObject(dxgiAdapter);
        }
    }

    private unsafe void CreateSizeDependentResources()
    {
        lock (_renderSync)
        {
            if (_swapchain is null || _d3dDevice is null || _swapchainPanel is null)
            {
                return;
            }

            uint width = (uint)Math.Max(1, Math.Ceiling(_swapchainPanel.ActualWidth));
            uint height = (uint)Math.Max(1, Math.Ceiling(_swapchainPanel.ActualHeight));

            ReleaseComPointer(ref _renderTargetViewPtr);
            ReleaseComObject(ref _depthStencilView);
            ReleaseComObject(ref _depthStencilTexture);
            ReleaseComObject(ref _renderTargetView);

            _swapchain.ResizeBuffers(2, width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, 0);

            Guid backBufferGuid = typeof(ID3D11Texture2D).GUID;
            _swapchain.GetBuffer(0, &backBufferGuid, out object? backBufferObject);
            ID3D11Texture2D backBuffer = (ID3D11Texture2D)backBufferObject!;
            _renderTargetView = CreateRenderTargetViewNative(_d3dDevice, backBuffer);
            CaptureComInterfacePointer(ref _renderTargetViewPtr, _renderTargetView, typeof(ID3D11RenderTargetView).GUID);
            Marshal.ReleaseComObject(backBuffer);

            D3D11_TEXTURE2D_DESC depthStencilDesc = new()
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_D24_UNORM_S8_UINT,
                SampleDesc = new DXGI_SAMPLE_DESC
                {
                    Count = 1,
                    Quality = 0,
                },
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_DEPTH_STENCIL,
                CPUAccessFlags = 0,
                MiscFlags = 0,
            };

            _depthStencilTexture = CreateTexture2DNative(_d3dDevice, &depthStencilDesc, null, "Failed to create the depth stencil texture.");
            _depthStencilView = CreateDepthStencilViewNative(_d3dDevice, _depthStencilTexture);

            _viewport = new D3D11_VIEWPORT
            {
                TopLeftX = 0,
                TopLeftY = 0,
                Width = width,
                Height = height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f,
            };
        }
    }

    private void ReleaseDeviceResources()
    {
        lock (_renderSync)
        {
            ReleaseRendererResources();
            ReleaseComPointer(ref _renderTargetViewPtr);
            ReleaseComPointer(ref _swapChainPtr);
            ReleaseComPointer(ref _d3dContextPtr);
            ReleaseComObject(ref _depthStencilView);
            ReleaseComObject(ref _depthStencilTexture);
            ReleaseComObject(ref _renderTargetView);
            ReleaseComObject(ref _swapchain);
            ReleaseComObject(ref _d3dContext);
            ReleaseComObject(ref _dxgiDevice);
            ReleaseComObject(ref _d3dDevice);
        }
    }

    private unsafe bool TryRenderFrameCore()
    {
        if (!_needsRender || !_isInitialized || !_renderHooked || _disposed)
        {
            return false;
        }

        lock (_renderSync)
        {
            if (_swapchain is null ||
                _d3dContext is null ||
                _renderTargetView is null ||
                _depthStencilView is null ||
                _d3dContextPtr == IntPtr.Zero ||
                _swapChainPtr == IntPtr.Zero ||
                _renderTargetViewPtr == IntPtr.Zero ||
                !HasRendererResources())
            {
                return false;
            }

            _needsRender = false;

            TimeSpan renderTimestamp = _renderStopwatch.Elapsed;
            TimeSpan elapsedTime = _lastRenderTimestamp == TimeSpan.Zero ? TimeSpan.Zero : renderTimestamp - _lastRenderTimestamp;
            _lastRenderTimestamp = renderTimestamp;

            RenderingFrame?.Invoke(this, new RenderingFrameEventArgs(elapsedTime));

            ClearRenderTargetViewNative(_d3dContextPtr, _renderTargetViewPtr, ClearColor);

            OMSetRenderTargetsNative(_d3dContextPtr, _renderTargetViewPtr);
            D3D11_VIEWPORT viewport = _viewport;
            RSSetViewportsNative(_d3dContextPtr, &viewport, 1);

            RenderFrame();
            PresentNative(_swapChainPtr, 1, 0);
            return true;
        }
    }

    private void RenderLoop()
    {
        WaitHandle[] handles = [_renderRequested, _renderThreadShutdown];

        try
        {
            while (true)
            {
                int signaledHandle = WaitHandle.WaitAny(handles);
                if (signaledHandle == 1)
                {
                    return;
                }

                TryRenderFrameCore();
            }
        }
        finally
        {
            _renderThreadStopped.Set();
        }
    }

    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport]
    private interface ISwapChainPanelNative
    {
        void SetSwapChain(IDXGISwapChain swapChain);
    }
}
