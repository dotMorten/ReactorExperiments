using System.Diagnostics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PanoramaViewer;

internal sealed class Image360Renderer : PanoramaRenderer
{
    private Uri? _source;

    public override void SetSource(Uri source)
    {
        if (_source == source)
        {
            return;
        }

        _source = source;
        _ = CreateTextureAsync(requestRender: true);
    }

    protected override Task InitializePanoramaTextureAsync()
    {
        return CreateTextureAsync();
    }

    private async Task<ImageData?> LoadTextureDataAsync()
    {
        if (string.IsNullOrEmpty(_source?.OriginalString))
        {
            return null;
        }

        IRandomAccessStream? stream = null;
        DataWriter? writer = null;
        try
        {
            if (!_source.IsAbsoluteUri)
            {
                byte[] imageData = await File.ReadAllBytesAsync(_source.OriginalString);
                stream = new InMemoryRandomAccessStream();
                writer = new DataWriter(stream);
                writer.WriteBytes(imageData);
                await writer.StoreAsync();
                stream.Seek(0);
            }
            else if (_source.IsFile)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(_source.LocalPath);
                stream = await file.OpenReadAsync();
            }
            else if (_source.Scheme == "ms-appx")
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(_source);
                stream = await file.OpenReadAsync();
            }
            else if (_source.Scheme == Uri.UriSchemeHttp || _source.Scheme == Uri.UriSchemeHttps)
            {
                using var httpClient = new System.Net.Http.HttpClient();
                using var response = await httpClient.GetAsync(_source);
                if (!response.IsSuccessStatusCode)
                {
                    Trace.WriteLine("Failed to load image from URL: " + _source + " - " + response.ReasonPhrase);
                    return null;
                }

                byte[] imageData = await response.Content.ReadAsByteArrayAsync();
                stream = new InMemoryRandomAccessStream();
                writer = new DataWriter(stream);
                writer.WriteBytes(imageData);
                await writer.StoreAsync();
                stream.Seek(0);
            }

            if (stream is null)
            {
                return null;
            }

            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return new ImageData(pixelData.DetachPixelData(), decoder.PixelWidth, decoder.PixelHeight);
        }
        finally
        {
            stream?.Dispose();
            writer?.Dispose();
        }
    }

    private async Task CreateTextureAsync(bool requestRender = false)
    {
        if (_source is null)
        {
            return;
        }

        ImageData? imageData = await LoadTextureDataAsync();

        lock (RenderSyncRoot)
        {
            if (IsDisposed || D3DDevice is null || D3DContext is null)
            {
                return;
            }

            ReleasePanoramaTextureResources();
            if (imageData is not null)
            {
                ApplyTextureData(imageData.Value);
            }
        }

        if (requestRender)
        {
            RequestRender();
        }
    }

    private unsafe void ApplyTextureData(ImageData imageData)
    {
        CreatePanoramaTextureResources(imageData.Width, imageData.Height);
        fixed (byte* pixelData = imageData.Pixels)
        {
            UpdatePanoramaTexture(pixelData, imageData.RowPitch);
        }
    }

    private readonly record struct ImageData(byte[] Pixels, uint Width, uint Height)
    {
        public uint RowPitch => Width * 4;
    }
}
