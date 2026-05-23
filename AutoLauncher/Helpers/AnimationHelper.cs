using System.Windows;
using System.Windows.Media.Animation;

namespace AutoLauncher.Helpers;

/// <summary>
/// Provides reusable WPF animation helpers for opacity fades and progress bar animation.
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// Smoothly animates the Opacity of a UIElement from one value to another.
    /// Returns a Task that completes when the animation finishes.
    /// </summary>
    public static Task AnimateOpacityAsync(this UIElement element, double from, double to, double seconds = 0.3)
    {
        var tcs = new TaskCompletionSource();
        var anim = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) => tcs.TrySetResult();
        element.BeginAnimation(UIElement.OpacityProperty, anim);
        return tcs.Task;
    }

    /// <summary>
    /// Smooth progress bar fill animation using WPF DoubleAnimation.
    /// Animates the indicator width, calculated from the track width.
    /// </summary>
    public static void AnimateProgressBar(FrameworkElement indicator, FrameworkElement track, double targetPercent)
    {
        var trackWidth = track.ActualWidth;
        var targetWidth = trackWidth * targetPercent / 100.0;
        var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(1000))
        {
            EasingFunction = new PowerEase { EasingMode = EasingMode.EaseOut, Power = 3 }
        };
        indicator.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }
}
