using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using WinRT;
using static PanoramaViewer.DXInterop;

namespace PanoramaViewer;

internal sealed class Video360Renderer : PanoramaRenderer
{
    private Uri? _source;
    private MediaPlayer? _mediaPlayer;
    private MediaSource? _mediaSource;
    private IDirect3DSurface? _videoSurface;
    private volatile bool _hasVideoFrame;

    public override void SetSource(Uri source)
    {
        if (_source == source)
        {
            return;
        }

        _source = source;
        _ = ConfigureMediaSourceAsync();
    }

    protected override async Task InitializePanoramaTextureAsync()
    {
        EnsureMediaPlayer();
        await ConfigureMediaSourceAsync();
        EnsureVideoTextureFromCurrentMedia();
    }

    protected override bool HasPanoramaSourceResources()
    {
        return _mediaPlayer is not null && _videoSurface is not null && _hasVideoFrame;
    }

    protected override void PreparePanoramaTexture()
    {
        try
        {
            _mediaPlayer!.CopyFrameToVideoSurface(_videoSurface!);
        }
        catch (COMException ex)
        {
            _hasVideoFrame = false;
            Trace.WriteLine($"CopyFrameToVideoSurface failed: 0x{ex.HResult:X8}");
        }
    }

    protected override void ReleasePanoramaSourceResources()
    {
        ReleaseVideoSurface();
        ReleaseMediaPlayer();
    }

    private void EnsureMediaPlayer()
    {
        if (_mediaPlayer is not null)
        {
            return;
        }

        _mediaPlayer = new MediaPlayer
        {
            AutoPlay = true,
            IsLoopingEnabled = true,
            IsVideoFrameServerEnabled = true,
            RealTimePlayback = true,
        };
        _mediaPlayer.CommandManager.IsEnabled = false;
        _mediaPlayer.MediaOpened += OnMediaOpened;
        _mediaPlayer.MediaFailed += OnMediaFailed;
        _mediaPlayer.VideoFrameAvailable += OnVideoFrameAvailable;
    }

    private async Task ConfigureMediaSourceAsync()
    {
        if (_mediaPlayer is null || _source is null)
        {
            return;
        }

        lock (RenderSyncRoot)
        {
            _hasVideoFrame = false;
            ReleaseVideoSurface();
            ReleasePanoramaTextureResources();
        }

        MediaSource? mediaSource = await CreateMediaSourceAsync(_source);
        _mediaSource?.Dispose();
        _mediaSource = mediaSource;
        _mediaPlayer.Source = _mediaSource;
        if (_mediaSource is not null)
        {
            _mediaPlayer.Play();
        }
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            _hasVideoFrame = false;
            EnsureVideoTextureFromCurrentMedia();
        });
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Trace.WriteLine($"Video playback failed: {args.Error} {args.ErrorMessage}");
    }

    private void OnVideoFrameAvailable(MediaPlayer sender, object args)
    {
        if (_videoSurface is null)
        {
            DispatcherQueue?.TryEnqueue(EnsureVideoTextureFromCurrentMedia);
            return;
        }

        _hasVideoFrame = true;
        RequestRender();
    }

    private void EnsureVideoTextureFromCurrentMedia()
    {
        if (_mediaPlayer is null || D3DDevice is null || D3DContext is null)
        {
            return;
        }

        uint width = Math.Max(1u, _mediaPlayer.PlaybackSession.NaturalVideoWidth);
        uint height = Math.Max(1u, _mediaPlayer.PlaybackSession.NaturalVideoHeight);

        lock (RenderSyncRoot)
        {
            if (IsDisposed || D3DDevice is null || D3DContext is null)
            {
                return;
            }

            ReleaseVideoSurface();
            ReleasePanoramaTextureResources();
            CreatePanoramaTextureResources(width, height);
            _videoSurface = CreateDirect3DSurfaceFromTexture(PanoramaTexture!);
            _hasVideoFrame = false;
        }
    }

    private void ReleaseVideoSurface()
    {
        _hasVideoFrame = false;
        _videoSurface?.Dispose();
        _videoSurface = null;
    }

    private void ReleaseMediaPlayer()
    {
        if (_mediaPlayer is null)
        {
            return;
        }

        _mediaPlayer.MediaOpened -= OnMediaOpened;
        _mediaPlayer.MediaFailed -= OnMediaFailed;
        _mediaPlayer.VideoFrameAvailable -= OnVideoFrameAvailable;
        _mediaPlayer.Source = null;
        _mediaSource?.Dispose();
        _mediaSource = null;
        _mediaPlayer.Dispose();
        _mediaPlayer = null;
    }

    private static async Task<MediaSource?> CreateMediaSourceAsync(Uri source)
    {
        try
        {
            Uri resolvedSource = ResolveSourceUri(source);

            if (resolvedSource.IsFile)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(resolvedSource.LocalPath);
                return MediaSource.CreateFromStorageFile(file);
            }

            if (resolvedSource.Scheme == "ms-appx")
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(resolvedSource);
                return MediaSource.CreateFromStorageFile(file);
            }

            return MediaSource.CreateFromUri(resolvedSource);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to create video media source: {ex.Message}");
            return null;
        }
    }

    private static Uri ResolveSourceUri(Uri source)
    {
        if (!source.IsAbsoluteUri)
        {
            return new Uri(Path.GetFullPath(source.OriginalString));
        }

        return source;
    }

    private static IDirect3DSurface CreateDirect3DSurfaceFromTexture(ID3D11Texture2D texture)
    {
        IntPtr dxgiSurfacePtr = GetComInterfacePointer(texture, typeof(IDXGISurface).GUID);

        try
        {
            int hr = CreateDirect3D11SurfaceFromDXGISurface(dxgiSurfacePtr, out IntPtr inspectableSurfacePtr);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            try
            {
                return MarshalInterface<IDirect3DSurface>.FromAbi(inspectableSurfacePtr);
            }
            finally
            {
                MarshalInterface<IDirect3DSurface>.DisposeAbi(inspectableSurfacePtr);
            }
        }
        finally
        {
            Marshal.Release(dxgiSurfacePtr);
        }
    }

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);
}
