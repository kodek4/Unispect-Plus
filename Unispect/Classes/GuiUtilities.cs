using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace Unispect
{
    /// <summary>
    /// GUI-specific utilities for WPF functionality
    /// </summary>
    public static class GuiUtilities
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out IntRect rect);

        public struct IntRect
        {
            public int Left, Top, Right, Bottom;
        }

        public static void ShowSystemMenu(Window window)
        {
            var hWnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            GetWindowRect(hWnd, out var pos);
            var hMenu = GetSystemMenu(hWnd, false);
            var cmd = TrackPopupMenu(hMenu, 0x100, pos.Left + 20, pos.Top + 20, 0, hWnd, IntPtr.Zero);
            if (cmd > 0) SendMessage(hWnd, 0x112, (IntPtr)cmd, IntPtr.Zero);
        }

        public static async void LaunchUrl(string url)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.AppStarting;
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                var nl = Environment.NewLine;
                await MessageBox(
                    $"Couldn't open: {url}.{nl}{nl}Exception:{nl}{ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        public static async Task<MessageDialogResult> MessageBox(string msg, string title = "",
            MessageDialogStyle messageDialogStyle = MessageDialogStyle.Affirmative,
            MetroDialogSettings metroDialogSettings = null)
        {
            if (string.IsNullOrEmpty(title))
                title = Application.Current.MainWindow?.Title;

            var mw = (Application.Current.MainWindow as MetroWindow);
            return await mw.ShowMessageAsync(title, msg, messageDialogStyle, metroDialogSettings);
        }

        public static void FadeFromTo(this UIElement uiElement, double fromOpacity, double toOpacity,
            int durationInMilliseconds, bool showOnStart, bool collapseOnFinish)
        {
            var timeSpan = TimeSpan.FromMilliseconds(durationInMilliseconds);
            var doubleAnimation =
                new DoubleAnimation(fromOpacity, toOpacity,
                    new Duration(timeSpan));

            uiElement.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
            if (showOnStart)
            {
                uiElement.ApplyAnimationClock(UIElement.VisibilityProperty, null);
                uiElement.Visibility = Visibility.Visible;
            }
            if (collapseOnFinish)
            {
                var keyAnimation = new ObjectAnimationUsingKeyFrames { Duration = new Duration(timeSpan) };
                keyAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Collapsed, KeyTime.FromTimeSpan(timeSpan)));
                uiElement.BeginAnimation(UIElement.VisibilityProperty, keyAnimation);
            }
        }

        public static void FadeIn(this UIElement uiElement, int durationInMilliseconds = 100)
        {
            uiElement.FadeFromTo(0, 1, durationInMilliseconds, true, false);
        }

        public static void FadeOut(this UIElement uiElement, int durationInMilliseconds = 100)
        {
            uiElement.FadeFromTo(1, 0, durationInMilliseconds, false, true);
        }

        public static void ResizeFromTo(this FrameworkElement uiElement, Size fromSize, Size toSize, int durationInMilliseconds)
        {
            var timeSpan = TimeSpan.FromMilliseconds(durationInMilliseconds);
            var sizeAnimationHeight = new DoubleAnimation(fromSize.Height, toSize.Height, new Duration(timeSpan));
            uiElement.BeginAnimation(FrameworkElement.HeightProperty, sizeAnimationHeight);
        }
    }
} 