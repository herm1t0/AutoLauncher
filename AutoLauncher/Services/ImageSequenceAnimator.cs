using System.Buffers.Binary;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GdiImage = System.Drawing.Image;
using Shared;

namespace AutoLauncher.Services;

/// <summary>
/// Plays an animated GIF with correct alpha-channel transparency (ARGB).
/// All frames are pre-rendered in the constructor and cached.
/// During animation -- only reference switching, zero allocations.
/// </summary>
public class ImageSequenceAnimator : IDisposable
{
    private readonly BitmapSource[] _frames;
    private readonly BitmapSource[] _recoloredFrames;
    private readonly int[] _delays;
    private int _currentFrame;
    private DispatcherTimer? _timer;
    private readonly System.Diagnostics.Stopwatch _sw = new();
    private bool _disposed;
    private bool _useRecolored;

    public event EventHandler<BitmapSource>? FrameChanged;

    /// <summary>
    /// The first frame of the GIF, taking the current theme into account
    /// (available immediately after construction).
    /// </summary>
    public BitmapSource FirstFrame => _useRecolored ? _recoloredFrames[0] : _frames[0];

    /// <summary>
    /// Async creation of the animator -- frame rendering is performed
    /// on a background thread without blocking the UI.
    /// </summary>
    public static async Task<ImageSequenceAnimator> CreateAsync(byte[] gifData)
    {
        return await Task.Run(() => new ImageSequenceAnimator(gifData));
    }

    /// <summary>
    /// Loads a GIF from a byte array (synchronously).
    /// </summary>
    private ImageSequenceAnimator(byte[] gifData)
    {
        using var ms = new MemoryStream(gifData);
        using var gifImage = GdiImage.FromStream(ms, useEmbeddedColorManagement: false);

        var dim = new FrameDimension(gifImage.FrameDimensionsList[0]);
        var frameCount = gifImage.GetFrameCount(dim);
        var width = gifImage.Width;
        var height = gifImage.Height;

        // Parse frame delays (PropertyTagFrameDelay = 0x5100)
        _delays = new int[frameCount];
        try
        {
            var propItem = gifImage.GetPropertyItem(0x5100);
            if (propItem?.Value != null)
            {
                for (var i = 0; i < frameCount; i++)
                {
                    var delayCs = BinaryPrimitives.ReadInt32LittleEndian(propItem.Value.AsSpan(i * 4, 4));
                    _delays[i] = Math.Max(delayCs, 10) * 10; // hundredths of a second -> ms, min 100 ms
                }
            }
            else
            {
                Array.Fill(_delays, 100);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to parse GIF frame delays: {ex.Message}");
            Array.Fill(_delays, 100);
        }

        // Pre-render all frames into frozen BitmapSources
        _frames = new BitmapSource[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            gifImage.SelectActiveFrame(dim, i);
            _frames[i] = RenderFrame(gifImage, width, height);
        }

        // Recolored copies for light theme (HSL transformation)
        _recoloredFrames = new BitmapSource[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            _recoloredFrames[i] = RecolorFrame(_frames[i], isLight: true);
        }
    }

    /// <summary>
    /// Renders a single GIF frame into a frozen BitmapSource with alpha channel.
    /// </summary>
    private static BitmapSource RenderFrame(GdiImage gifImage, int width, int height)
    {
        // Render onto a fresh transparent canvas
        using var bitmap = new System.Drawing.Bitmap(width, height);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.DrawImage(gifImage, 0, 0);
        }

        // Read ARGB pixels directly (preserving alpha channel)
        var rect = new System.Drawing.Rectangle(0, 0, width, height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int stride = bmpData.Stride;
            int pixelSize = stride * height;
            var pixels = new byte[pixelSize];
            Marshal.Copy(bmpData.Scan0, pixels, 0, pixelSize);

            var bs = BitmapSource.Create(
                width, height, 96, 96,
                PixelFormats.Bgra32, null,
                pixels, stride);
            bs.Freeze();
            return bs;
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }

    /// <summary>
    /// HSL transformation: for light theme, increase Lightness,
    /// slightly reduce Saturation; for dark - return original unchanged.
    /// Much more natural than simple inversion (255-RGB).
    /// </summary>
    private static BitmapSource RecolorFrame(BitmapSource source, bool isLight)
    {
        // For dark theme, no changes - return original
        if (!isLight) return source;

        int w = source.PixelWidth, h = source.PixelHeight;
        var stride = w * 4; // BGRA32
        var pixels = new byte[h * stride];
        source.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            var a = pixels[i + 3];

            if (a == 0) continue; // leave transparency untouched

            // Check "chromaticity" by max-min channel difference.
            // Important: HSL gives sat=1.0 for near-white pixels (R=255,G=252,B=248)
            // at l>0.5, which is wrong. We look at the actual channel difference.
            var minC = Math.Min(Math.Min(r, g), b);
            var maxC = Math.Max(Math.Max(r, g), b);
            var delta = maxC - minC;

            if (delta < 10)
            {
                // Nearly achromatic (gray/white/black) -- brightness only,
                // guaranteed no color cast
                var lum = (r * 0.299f + g * 0.587f + b * 0.114f) / 255f;
                lum = 0.15f + lum * 0.75f;
                var gray = (byte)(lum * 255);
                pixels[i] = pixels[i + 1] = pixels[i + 2] = gray;
                continue;
            }

            // Color pixel -- full HSL transformation
            RgbToHsl(r, g, b, out var hue, out var sat, out var light);

            // Light theme (Catppuccin Latte):
            //   - increase brightness: black -> ~15% gray, white -> ~90%
            //   - reduce saturation for a pastel look
            light = 0.15f + light * 0.75f;
            sat *= 0.85f;

            // HSL -> RGB
            HslToRgb(hue, sat, light, out r, out g, out b);

            pixels[i] = b; // B
            pixels[i + 1] = g; // G
            pixels[i + 2] = r; // R
            // a (i+3) - left unchanged
        }

        var bs = BitmapSource.Create(w, h, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, pixels, stride);
        bs.Freeze();
        return bs;
    }

    /// <summary>
    /// Converts a color from RGB to HSL (hue 0-360, saturation 0-1, lightness 0-1).
    /// </summary>
    private static void RgbToHsl(byte r, byte g, byte b, out float h, out float s, out float l)
    {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        var min = Math.Min(Math.Min(rf, gf), bf);
        var max = Math.Max(Math.Max(rf, gf), bf);
        var delta = max - min;

        l = (max + min) / 2f;

        if (delta < 0.0001f)
        {
            h = 0f;
            s = 0f;
            return;
        }

        s = l <= 0.5f ? delta / (max + min) : delta / (2f - max - min);

        if (Math.Abs(max - rf) < 0.0001f)
            h = ((gf - bf) / delta + (gf < bf ? 6f : 0f)) * 60f;
        else if (Math.Abs(max - gf) < 0.0001f)
            h = ((bf - rf) / delta + 2f) * 60f;
        else
            h = ((rf - gf) / delta + 4f) * 60f;
    }

    /// <summary>
    /// Converts a color from HSL back to RGB.
    /// </summary>
    private static void HslToRgb(float h, float s, float l, out byte r, out byte g, out byte b)
    {
        if (s < 0.0001f)
        {
            byte gray = (byte)(l * 255);
            r = g = b = gray;
            return;
        }

        var hNorm = h / 60f;
        var i = (int)Math.Floor(hNorm);
        var f = hNorm - i;
        var p = l * (1f - s);
        var q = l * (1f - s * f);
        var t = l * (1f - s * (1f - f));

        float rf, gf, bf;
        switch (i)
        {
            case 0:
                rf = l;
                gf = t;
                bf = p;
                break;
            case 1:
                rf = q;
                gf = l;
                bf = p;
                break;
            case 2:
                rf = p;
                gf = l;
                bf = t;
                break;
            case 3:
                rf = p;
                gf = q;
                bf = l;
                break;
            case 4:
                rf = t;
                gf = p;
                bf = l;
                break;
            default:
                rf = l;
                gf = p;
                bf = q;
                break;
        }

        r = (byte)(rf * 255);
        g = (byte)(gf * 255);
        b = (byte)(bf * 255);
    }

    /// <summary>
    /// Switches between original frames (dark theme) and
    /// HSL-recolored frames (light theme).
    /// </summary>
    public void SetThemeMode(bool isLight)
    {
        if (_useRecolored == isLight) return;
        _useRecolored = isLight;

        // If animation is already running -- emit the current frame in the new variant
        FrameChanged?.Invoke(this, GetCurrentFrame());
    }

    private BitmapSource GetCurrentFrame()
    {
        return _useRecolored ? _recoloredFrames[_currentFrame] : _frames[_currentFrame];
    }

    public void Start()
    {
        if (_frames.Length <= 1) return;

        _sw.Reset();
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += OnTimerTick;
        _timer.Interval = TimeSpan.FromMilliseconds(_delays[0]);
        _timer.Start();
        _sw.Start();
    }

    private void Stop()
    {
        if (_timer == null) return;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        // Drift compensation: subtract time spent on the previous frame
        _sw.Stop();
        var elapsed = _sw.ElapsedMilliseconds;

        _currentFrame = (_currentFrame + 1) % _frames.Length;
        FrameChanged?.Invoke(this, GetCurrentFrame());

        var target = _delays[_currentFrame];
        var compensated = Math.Max((int)(target - elapsed), 10); // min 10 ms
        _timer!.Interval = TimeSpan.FromMilliseconds(compensated);

        _sw.Restart();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _timer = null;
        GC.SuppressFinalize(this);
    }
}