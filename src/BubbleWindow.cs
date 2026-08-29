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

        private readonly Button captureButton;
        private readonly double idleOpacity;
        private bool composing;

        // onCapture＝左鍵（記錄：Trilium/歸檔）、onClipboard＝右鍵（只進剪貼簿）
        public BubbleWindow(Action onCapture, Action onClipboard, Action onNote, double idleOpacity)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Opacity = idleOpacity;
            this.idleOpacity = idleOpacity;

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

            captureButton = new Button
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
            captureButton.Click += (s, e) => onCapture?.Invoke();
            captureButton.MouseRightButtonUp += (s, e) =>
            {
                onClipboard?.Invoke();
                e.Handled = true;
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(grip);
            panel.Children.Add(captureButton);

            if (onNote != null)
            {
                var noteButton = new Button
                {
                    Content = "📝",
                    FontSize = 22,
                    Width = 52,
                    Height = 52,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Focusable = false
                };
                noteButton.Click += (s, e) => onNote();
                panel.Children.Add(noteButton);
            }

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x20, 0x20, 0x20)),
                CornerRadius = new CornerRadius(10),
                Child = panel
            };

            MouseEnter += (s, e) => Opacity = 1.0;
            MouseLeave += (s, e) => Opacity = composing ? 1.0 : this.idleOpacity;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
                    exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
            };
        }

        /// <summary>入力欄を開いている間は「もう一度押せば送る」と分かる見た目にする。</summary>
        public void SetComposing(bool value)
        {
            composing = value;
            captureButton.Content = value ? "✅" : "📷";
            captureButton.ToolTip = value ? "送出（含輸入框的文字）" : null;
            Opacity = value ? 1.0 : idleOpacity;
        }
    }
}
