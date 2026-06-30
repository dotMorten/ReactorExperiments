using Microsoft.UI.Input;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Core.V1Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace PanoramaViewer;

public abstract record PanoramaElement(Uri Source) : Element;
public sealed record Image360Element(Uri Source) : PanoramaElement(Source);
public sealed record Video360Element(Uri Source) : PanoramaElement(Source);
public sealed record OceanElement(double WaveScale, double WaveSpeed, double WaveHeight, double WaveChoppiness) : Element;

internal sealed class Image360 : PanoramaComponent<Image360Element, Image360Renderer>;
internal sealed class Video360 : PanoramaComponent<Video360Element, Video360Renderer>;
internal sealed class OceanView : ViewportComponent<OceanElement, OceanRenderer>
{
    protected override void ConfigureRenderer(OceanRenderer renderer, OceanElement props)
    {
        renderer.WaveScale = (float)props.WaveScale;
        renderer.WaveSpeed = (float)props.WaveSpeed;
        renderer.WaveHeight = (float)props.WaveHeight;
        renderer.WaveChoppiness = (float)props.WaveChoppiness;
    }
}

internal sealed record SwapChainPanelElement(Action<SwapChainPanel> OnAttached) : Element;

internal sealed class SwapChainPanelHandler : IElementHandler<SwapChainPanelElement, SwapChainPanel>
{
    public SwapChainPanel Mount(MountContext ctx, SwapChainPanelElement element)
    {
        var panel = ctx.RentControl<SwapChainPanel>();
        element.OnAttached(panel);
        return panel;
    }

    public void Update(UpdateContext ctx, SwapChainPanelElement oldEl, SwapChainPanelElement newEl, SwapChainPanel control)
    {
        newEl.OnAttached(control);
    }

    public void Unmount(UnmountContext ctx, SwapChainPanel control)
    {
        ctx.ReturnControl(control);
    }
}

public static class Factories
{
    static Factories()
    {
        ControlRegistry.Register(static () => new SwapChainPanelHandler());
    }

    public static Element Image360(Uri source) =>
        Microsoft.UI.Reactor.Factories.Component<Image360, Image360Element>(new Image360Element(source));

    public static Element Video360(Uri source) =>
        Microsoft.UI.Reactor.Factories.Component<Video360, Video360Element>(new Video360Element(source));

    public static Element Ocean(double waveScale = 0.1, double waveSpeed = 1.0, double waveHeight = 1.0, double waveChoppiness = 0.048) =>
        Microsoft.UI.Reactor.Factories.Component<OceanView, OceanElement>(new OceanElement(waveScale, waveSpeed, waveHeight, waveChoppiness));
}

internal abstract class ViewportComponent<TProps, TRenderer> : Component<TProps>
    where TProps : Element
    where TRenderer : DXRenderer, ICameraViewportRenderer, new()
{
    private const float MouseRotationScale = 0.0035f;
    private const float KeyboardRotationDelta = MathF.PI / 90.0f;
    private const float MinPitch = -MathF.PI / 2.0f;
    private const float MaxPitch = MathF.PI / 4.0f;
    private const float MinFieldOfView = MathF.PI / 6.0f;
    private const float MaxFieldOfView = 2.0f * MathF.PI / 3.0f;
    private const float MouseWheelZoomInFactor = 0.8f;
    private const float MouseWheelZoomOutFactor = 1.25f;
    private const float MouseWheelZoomSettleThreshold = 0.0005f;
    private const float MouseWheelZoomResponsiveness = 14.0f;
    private const int OemPlusVirtualKey = 0xBB;
    private const int OemMinusVirtualKey = 0xBD;
    private SwapChainPanel? _swapchainPanel;
    private TRenderer? _renderer;
    private float _wheelZoomTargetFieldOfView;
    private int _wheelZoomDirection;
    private bool _isWheelZoomAnimating;

    protected virtual void ConfigureRenderer(TRenderer renderer, TProps props)
    {
    }

    public override Element Render()
    {
        var rendererRef = UseRef<TRenderer?>(null);
        rendererRef.Current ??= new TRenderer();
        ConfigureRenderer(rendererRef.Current, Props);

        UseEffect(() =>
            {
                return () =>
                {
                    DetachInput();
                    rendererRef.Current?.Dispose();
                    rendererRef.Current = null;
                };
            },
            Array.Empty<object>());

        return new SwapChainPanelElement(panel =>
        {
            TRenderer? renderer = rendererRef.Current;
            if (renderer is null)
            {
                return;
            }

            renderer.Attach(panel);
            AttachInput(panel, renderer);
        });
    }

    private void AttachInput(SwapChainPanel swapchainPanel, TRenderer renderer)
    {
        if (_swapchainPanel == swapchainPanel)
        {
            AttachRendererFrame(renderer);
            return;
        }

        DetachInput();
        AttachRendererFrame(renderer);
        _swapchainPanel = swapchainPanel;
        _swapchainPanel.ManipulationMode = ManipulationModes.All;
        _swapchainPanel.IsTabStop = true;
        _swapchainPanel.Loaded += OnLoaded;
        _swapchainPanel.PointerWheelChanged += OnWheelChanged;
        _swapchainPanel.ManipulationDelta += OnManipulationDelta;
        _swapchainPanel.DoubleTapped += OnDoubleTapped;
        _swapchainPanel.KeyDown += OnKeyDown;
        FocusIfNothingElseHasFocus();
    }

    private void DetachInput()
    {
        if (_swapchainPanel is null)
        {
            return;
        }

        DetachRendererFrame();
        _swapchainPanel.Loaded -= OnLoaded;
        _swapchainPanel.PointerWheelChanged -= OnWheelChanged;
        _swapchainPanel.ManipulationDelta -= OnManipulationDelta;
        _swapchainPanel.DoubleTapped -= OnDoubleTapped;
        _swapchainPanel.KeyDown -= OnKeyDown;
        _swapchainPanel = null;
        StopWheelZoomAnimation();
    }

    private void AttachRendererFrame(TRenderer renderer)
    {
        if (_renderer == renderer)
        {
            return;
        }

        DetachRendererFrame();
        _renderer = renderer;
        _renderer.RenderingFrame += OnRenderingFrame;
    }

    private void DetachRendererFrame()
    {
        if (_renderer is null)
        {
            return;
        }

        _renderer.RenderingFrame -= OnRenderingFrame;
        _renderer = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FocusIfNothingElseHasFocus();
    }

    private void FocusIfNothingElseHasFocus()
    {
        if (_swapchainPanel?.XamlRoot is null)
        {
            return;
        }

        if (FocusManager.GetFocusedElement(_swapchainPanel.XamlRoot) is null)
        {
            _swapchainPanel.Focus(FocusState.Programmatic);
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_renderer is null)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Left:
                _renderer.Yaw -= KeyboardRotationDelta;
                break;
            case VirtualKey.Right:
                _renderer.Yaw += KeyboardRotationDelta;
                break;
            case VirtualKey.Up:
                _renderer.Pitch = Math.Clamp(_renderer.Pitch - KeyboardRotationDelta, MinPitch, MaxPitch);
                break;
            case VirtualKey.Down:
                _renderer.Pitch = Math.Clamp(_renderer.Pitch + KeyboardRotationDelta, MinPitch, MaxPitch);
                break;
            case VirtualKey.Add:
            case (VirtualKey)OemPlusVirtualKey:
                QueueZoomAnimation(-1, 1);
                e.Handled = true;
                break;
            case VirtualKey.Subtract:
            case (VirtualKey)OemMinusVirtualKey:
                QueueZoomAnimation(1, 1);
                e.Handled = true;
                break;
        }
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (_renderer is null)
        {
            return;
        }

        StopWheelZoomAnimation();
        var translation = e.Delta.Translation;
        _renderer.Yaw -= (float)translation.X * MouseRotationScale;
        _renderer.Pitch = Math.Clamp(_renderer.Pitch - (float)translation.Y * MouseRotationScale, MinPitch, MaxPitch);
        _renderer.FieldOfView = Math.Clamp(_renderer.FieldOfView / e.Delta.Scale, MinFieldOfView, MaxFieldOfView);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_renderer is null)
        {
            return;
        }

        QueueZoomAnimation(IsControlKeyDown() ? 1 : -1, 1);
        e.Handled = true;
    }

    private void OnWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_swapchainPanel is null || _renderer is null)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(_swapchainPanel).Properties;
        if (properties.MouseWheelDelta == 0)
        {
            return;
        }
        int direction = properties.MouseWheelDelta > 0 ? -1 : 1;
        int wheelDetents = Math.Max(1, Math.Abs(properties.MouseWheelDelta) / 120);
        QueueZoomAnimation(direction, wheelDetents);
        e.Handled = true;
    }

    private void QueueZoomAnimation(int direction, int steps)
    {
        if (_renderer is null)
        {
            return;
        }

        float zoomFactor = direction < 0 ? MouseWheelZoomInFactor : MouseWheelZoomOutFactor;
        float startingFieldOfView = direction == _wheelZoomDirection ? _wheelZoomTargetFieldOfView : _renderer.FieldOfView;

        _wheelZoomDirection = direction;
        _wheelZoomTargetFieldOfView = Math.Clamp(startingFieldOfView * MathF.Pow(zoomFactor, steps), MinFieldOfView, MaxFieldOfView);
        StartWheelZoomAnimation();
    }

    private static bool IsControlKeyDown()
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
    }

    private void StartWheelZoomAnimation()
    {
        if (_renderer is null)
        {
            return;
        }

        _isWheelZoomAnimating = true;
        _renderer.RequestRender();
    }

    private void OnRenderingFrame(object? sender, RenderingFrameEventArgs e)
    {
        if (!_isWheelZoomAnimating || _renderer is null)
        {
            return;
        }

        float currentFieldOfView = _renderer.FieldOfView;
        float remainingFieldOfView = _wheelZoomTargetFieldOfView - currentFieldOfView;
        if (MathF.Abs(remainingFieldOfView) <= MouseWheelZoomSettleThreshold)
        {
            _renderer.FieldOfView = _wheelZoomTargetFieldOfView;
            StopWheelZoomAnimation();
            return;
        }

        float elapsedSeconds = (float)e.ElapsedTime.TotalSeconds;
        float interpolation = 1.0f - MathF.Exp(-MouseWheelZoomResponsiveness * elapsedSeconds);
        _renderer.FieldOfView = Math.Clamp(
            currentFieldOfView + remainingFieldOfView * interpolation,
            MinFieldOfView,
            MaxFieldOfView);

        if (_isWheelZoomAnimating)
        {
            _renderer.RequestRender();
        }
    }

    private void StopWheelZoomAnimation()
    {
        _isWheelZoomAnimating = false;
        _wheelZoomDirection = 0;
    }
}

internal abstract class PanoramaComponent<TProps, TRenderer> : ViewportComponent<TProps, TRenderer>
    where TProps : PanoramaElement
    where TRenderer : PanoramaRenderer, new()
{
    protected override void ConfigureRenderer(TRenderer renderer, TProps props)
    {
        renderer.SetSource(props.Source);
    }
}
