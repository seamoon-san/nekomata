using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace Nekomata.UI.Helpers;

public static class ThemeTransitionHelper
{
    /// <summary>
    /// Applies the new theme with a smooth fade transition.
    /// </summary>
    /// <param name="newTheme">The theme to apply.</param>
    public static void ApplyThemeSmoothly(ApplicationTheme newTheme)
    {
        var windows = Application.Current.Windows.OfType<Window>().ToList();
        var overlays = new Dictionary<Window, FrameworkElement>();

        // 1. Capture snapshots of all open windows
        foreach (var window in windows)
        {
            if (window.WindowState == WindowState.Minimized) continue;
            
            var overlay = CreateSnapshotOverlay(window);
            if (overlay != null)
            {
                overlays[window] = overlay;
            }
        }

        // 2. Apply the new theme
        ApplicationThemeManager.Apply(newTheme);

        // 3. Animate the overlays to fade out
        foreach (var kvp in overlays)
        {
            AnimateOverlay(kvp.Key, kvp.Value);
        }
    }

    private static FrameworkElement? CreateSnapshotOverlay(Window window)
    {
        // FluentWindow (and standard Window) usually has a Panel (like Grid) as Content
        if (window.Content is not Panel rootPanel) return null;
        if (rootPanel.ActualWidth <= 0 || rootPanel.ActualHeight <= 0) return null;

        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            int width = (int)(rootPanel.ActualWidth * dpi.DpiScaleX);
            int height = (int)(rootPanel.ActualHeight * dpi.DpiScaleY);

            if (width <= 0 || height <= 0) return null;

            var rtb = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            rtb.Render(rootPanel);
            rtb.Freeze();

            var image = new Image
            {
                Source = rtb,
                IsHitTestVisible = true, // Block interactions during the transition
                Stretch = Stretch.Fill,
                Width = rootPanel.ActualWidth,
                Height = rootPanel.ActualHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            // If the root is a Grid, ensure the image covers all cells
            if (rootPanel is Grid)
            {
                Grid.SetRowSpan(image, int.MaxValue);
                Grid.SetColumnSpan(image, int.MaxValue);
            }
            
            // Add the overlay as the last child so it appears on top
            rootPanel.Children.Add(image);
            
            return image;
        }
        catch (Exception)
        {
            // If something goes wrong during capture, we just skip the overlay
            return null;
        }
    }

    private static void AnimateOverlay(Window window, FrameworkElement overlay)
    {
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Completed += (s, e) =>
        {
            // Remove the overlay when animation completes
            if (window.Content is Panel rootPanel)
            {
                rootPanel.Children.Remove(overlay);
            }
        };

        overlay.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}
