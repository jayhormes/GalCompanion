using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace GalCompanion
{
    // 遊戲進行中顯示的浮動氣泡。WS_EX_NOACTIVATE 讓點擊不搶焦點，
    // 遊戲維持前景視窗，截圖才抓得到遊戲畫面。
    internal sealed class BubbleWindow : Window
    {
        public event Action<double, double> Moved;

        public BubbleWindow(Action onScreenshot)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Opacity = 0.55;

            var grip = new Border
            {
                Width = 16,
                Background = new SolidColorBrush(Color.FromArgb(0x55, 0xAA, 0xAA, 0xAA)),
                CornerRadius = new CornerRadius(10, 0, 0, 10),
                Cursor = Cursors.SizeAll,
                Child = new TextBlock
                {
                    Text = "⋮",
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            grip.MouseLeftButtonDown += (s, e) =>
            {
                DragMove();
                Moved?.Invoke(Left, Top);
            };

            var button = new Button
            {
                Content = "📷",
                FontSize = 22,
                Width = 52,
                Height = 52,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false
            };
            button.Click += (s, e) => onScreenshot?.Invoke();

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(grip);
            panel.Children.Add(button);

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x20, 0x20, 0x20)),
                CornerRadius = new CornerRadius(10),
                Child = panel
            };

            MouseEnter += (s, e) => Opacity = 1.0;
            MouseLeave += (s, e) => Opacity = 0.55;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                    exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
            };
        }
    }
}
