using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.ElementExtensions;
using static Microsoft.UI.Reactor.Factories;
using static PanoramaViewer.Factories;

namespace PanoramaViewer;

sealed class App : Component
{
    private static readonly Uri ImageSource = new("Assets/image360.jpg", UriKind.Relative);
    private static readonly Uri VideoSource = new("Assets/video360.mp4", UriKind.Relative);

    public override Element Render()
    {
        var (selectedSample, setSelectedSample) = UseState(0);
        var (waveScale, setWaveScale) = UseState(0.1);
        var (waveSpeed, setWaveSpeed) = UseState(1.0);
        var (waveHeight, setWaveHeight) = UseState(1.0);
        var (waveChoppiness, setWaveChoppiness) = UseState(0.048);

        Element content = selectedSample switch
        {
            0 => Video360(VideoSource),
            1 => Image360(ImageSource),
            _ => OceanSample(waveScale, setWaveScale, waveSpeed, setWaveSpeed, waveHeight, setWaveHeight, waveChoppiness, setWaveChoppiness),
        };

        return Grid(
            [GridSize.Star()],
            [GridSize.Auto, GridSize.Auto, GridSize.Star()],
            TitleBar("Panorama Viewer"),
            RadioButtons(["360 Video", "360 Image", "Ocean Shader"], selectedSample, setSelectedSample)
                .Margin(16)
                .Grid(1, 0).Set(rb => rb.MaxColumns = 3),
            content.Grid(2, 0));
    }

    private static Element OceanSample(
        double waveScale,
        Action<double> setWaveScale,
        double waveSpeed,
        Action<double> setWaveSpeed,
        double waveHeight,
        Action<double> setWaveHeight,
        double waveChoppiness,
        Action<double> setWaveChoppiness)
    {
        return Grid(
            [GridSize.Star()],
            [GridSize.Star(), GridSize.Auto],
            Ocean(waveScale, waveSpeed, waveHeight, waveChoppiness).Grid(rowSpan: 2),
            VStack(8,
                OceanSlider("Wave scale", waveScale, 0.02, 0.3, 0.01, setWaveScale),
                OceanSlider("Wave speed", waveSpeed, 0.0, 3.0, 0.05, setWaveSpeed),
                OceanSlider("Wave height", waveHeight, 0.2, 2.5, 0.05, setWaveHeight),
                OceanSlider("Choppiness", waveChoppiness, 0.0, 0.15, 0.002, setWaveChoppiness))
                .Padding(16).Background(new ThemeRef("ControlOnImageFillColorDefaultBrush")).Width(300).Margin(8)
                .CornerRadius(8)
                .HorizontalAlignment(Microsoft.UI.Xaml.HorizontalAlignment.Left)
                .Set(sp => sp.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8))
                .Grid(1, 0));
    }

    private static Element OceanSlider(string label, double value, double min, double max, double step, Action<double> setValue) =>
        Slider(value, min, max, setValue)
            .Header($"{label}: {value:F3}")
            .StepFrequency(step)
            .ThumbToolTip();
}
