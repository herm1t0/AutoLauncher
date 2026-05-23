using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AutoLauncher.Helpers;
using AutoLauncher.Services;

namespace AutoLauncher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ConfigService _configService;
    private readonly LauncherService _launcher;
    private readonly ThemeService _themeService;

    private ImageSequenceAnimator? _gifAnimator;
    private EventHandler<BitmapSource>? _gifFrameChangedHandler;
    private CancellationTokenSource? _cts;
    private bool _hasStarted;
    private bool _isClosing;

    public MainWindow(ConfigService configService, LauncherService launcher, ThemeService themeService)
    {
        _configService = configService;
        _launcher = launcher;
        _themeService = themeService;

        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
        _launcher.ProgressChanged += OnLaunchProgress;
    }

    // - Drag window
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    // - Close on Escape
    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _isClosing) return;
        _isClosing = true;
        if (_cts is { } cts)
            await cts.CancelAsync();
        ProgressIndicator.BeginAnimation(FrameworkElement.WidthProperty, null);
        await this.AnimateOpacityAsync(Opacity, 0);
        DisposeGif();
        Application.Current.Shutdown();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();

        // 1) Load config and apply theme BEFORE showing the window
        var config = await _configService.LoadAsync();

        // Resolve theme via ThemeService
        var (themePrefix, isLight) = _themeService.ResolveTheme(config.Theme);
        _themeService.ApplyTheme(themePrefix, isLight);

        // Update hardcoded progress bar glow color (DynamicResource not supported)
        if (ProgressGlow is { } glow)
        {
            glow.Color = _themeService.GetProgressGlowColor(themePrefix, isLight);
        }

        // Apply Topmost from config
        Topmost = config.Topmost;

        // 2) Load GIF from embedded resource (Assets.cat.gif)
        //    Create animator asynchronously (frame rendering on background thread,
        //    does not block UI) and subscribe to frames. Timer is not started --
        //    we'll start after fade-in to avoid frame cycling in an invisible window.
        await using (var gifStream = assembly.GetManifestResourceStream("Assets.cat.gif"))
        {
            if (gifStream != null)
            {
                var gifBytes = new byte[gifStream.Length];
                await gifStream.ReadExactlyAsync(gifBytes, 0, gifBytes.Length);

                // Async creation: frame rendering on background thread
                _gifAnimator = await ImageSequenceAnimator.CreateAsync(gifBytes);
                _gifFrameChangedHandler = (_, frame) => CatImage.Source = frame;
                _gifAnimator.FrameChanged += _gifFrameChangedHandler;
                _gifAnimator.SetThemeMode(isLight);

                // First frame respecting current theme (FirstFrame -- computed property)
                CatImage.Source = _gifAnimator.FirstFrame;
            }
        }

        // 3) Start GIF animation immediately (before fade-in), so the cat
        //    is already animated when the window appears
        _gifAnimator?.Start();

        // 4) Smooth window appearance (fade-in)
        await this.AnimateOpacityAsync(0, 1);

        if (config.Apps.Count == 0)
        {
            ActionText.Text = "😿  No apps configured";
            return;
        }

        // 5) CancellationTokenSource for cancellation on window close
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // 6) Launch applications asynchronously -- LaunchAllAsync yields control
        //    back to the UI thread during awaits (I/O, delays), not blocking animation
        try
        {
            await _launcher.LaunchAllAsync(config.Apps, token);
        }
        catch (OperationCanceledException)
        {
            // User closed the window -- normal situation, do nothing
        }
    }

    /// <summary>
    /// Handles launch progress events: updates progress bar, formats action text,
    /// shows/hides the counter, and triggers shutdown on completion.
    /// </summary>
    private void OnLaunchProgress(object? sender, LaunchProgressEventArgs e)
    {
        // InvokeAsync with Render -- processed before each frame,
        // ensuring timely progress bar updates
        Dispatcher.InvokeAsync(() =>
        {
            // Show counter on first progress
            if (!_hasStarted && e.Status != LaunchStatus.Completed)
            {
                _hasStarted = true;
                ProgressText.Visibility = Visibility.Visible;
            }

            SetActionText(e);

            // Smooth animation to target value via WPF DoubleAnimation
            AnimationHelper.AnimateProgressBar(ProgressIndicator, ProgressTrack, e.ProgressPercent);

            if (e.Status != LaunchStatus.Completed)
            {
                ProgressText.Text = $"{e.CurrentIndex} of {e.TotalCount} launched";
            }
            else
            {
                // Final message -- close after 3 sec (timer keeps ticking
                // to smoothly animate progress bar to 100%)
                _ = ShutdownAfterDelayAsync();
            }
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Formats ActionText based on LaunchStatus: emoji + bold app name + suffix.
    /// Shows the application icon if AppIconPath is provided.
    /// </summary>
    private void SetActionText(LaunchProgressEventArgs e)
    {
        // Application icon
        var icon = IconHelper.LoadAppIcon(e.AppIconPath, e.AppPath);
        if (icon != null)
        {
            AppIconImage.Source = icon;
            AppIconImage.Visibility = Visibility.Visible;
        }
        else
        {
            AppIconImage.Source = null;
            AppIconImage.Visibility = Visibility.Collapsed;
        }

        ActionText.Inlines.Clear();
        var appName = e.AppName ?? "";

        var statusText = e.Status switch
        {
            LaunchStatus.Launching => appName,
            LaunchStatus.Success => $"\u2705  {appName}  \u2713",
            LaunchStatus.Failed => $"\u274c  {appName}  \u2717",
            LaunchStatus.Skipped => $"\u26a0\ufe0f  Skipped invalid entry: {e.ErrorMessage ?? "unknown"}",
            LaunchStatus.Completed => $"Launched {e.CurrentIndex} of {e.TotalCount}!",
            _ => appName
        };

        if (string.IsNullOrWhiteSpace(statusText))
            return;

        if (!string.IsNullOrWhiteSpace(appName) && statusText.Contains(appName, StringComparison.Ordinal))
        {
            // Split by app name: [prefix] + [name (bold)] + [suffix]
            var idx = statusText.IndexOf(appName, StringComparison.Ordinal);
            var before = statusText[..idx];
            var after = statusText[(idx + appName.Length)..];

            ActionText.Inlines.Add(new Run(before));
            ActionText.Inlines.Add(new Run(appName) { FontWeight = FontWeights.Bold });
            ActionText.Inlines.Add(new Run(after));
        }
        else
        {
            // No name (final message or error) -- render all text as-is
            ActionText.Inlines.Add(new Run(statusText));
        }
    }

    private async Task ShutdownAfterDelayAsync()
    {
        // Wait 3 seconds for the user to see the final state
        await Task.Delay(3000);
        if (_cts is { } cts)
            await cts.CancelAsync();
        await this.AnimateOpacityAsync(Opacity, 0);
        DisposeGif();
        Application.Current.Shutdown();
    }

    // - Cleanup on close
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        _cts?.Cancel();
        ProgressIndicator.BeginAnimation(FrameworkElement.WidthProperty, null);
        DisposeGif();
    }

    private void DisposeGif()
    {
        if (_gifAnimator != null)
        {
            if (_gifFrameChangedHandler != null)
                _gifAnimator.FrameChanged -= _gifFrameChangedHandler;
            _gifAnimator.Dispose();
            _gifAnimator = null;
        }

        _cts?.Dispose();
        _cts = null;
    }
}
