using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GalCompanion
{
    /// <summary>
    /// 📷 を 1 回押したときに気泡の隣に出る入力欄。モーダルにしないので、
    /// 書きかけのままゲーム画面を見に戻ってから送ることもできる。
    /// </summary>
    internal sealed class ComposerWindow : Window
    {
        private readonly TextBox textBox;
        private readonly CheckBox attachBox;

        public event Action Committed;
        public event Action Cancelled;

        public string Text => textBox.Text;

        /// <summary>截圖を添えるか。翻訳の指摘は該当箇所の絵があってこそ後で読めるので既定は付ける。</summary>
        public bool AttachScreenshot => attachBox.IsChecked == true;

        public ComposerWindow(string gameTitle, string destination, bool attachScreenshot)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Height;
            Width = 420;

            var label = string.IsNullOrWhiteSpace(destination) ? "補充描述" : destination;
            var heading = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(gameTitle) ? label : $"{label} — {gameTitle}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 64,
                MaxHeight = 200,
                FontSize = 14,
                Padding = new Thickness(4)
            };

            attachBox = new CheckBox
            {
                Content = "附上截圖",
                Foreground = Brushes.White,
                IsChecked = attachScreenshot,
                VerticalAlignment = VerticalAlignment.Center
            };

            var hint = new TextBlock
            {
                Text = "再按一次同一顆按鈕送出　·　Enter 送出　·　Shift+Enter 換行　·　Esc 取消",
                Foreground = Brushes.White,
                Opacity = 0.7,
                FontSize = 11,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            // ROG Ally のように物理キーが無い環境でも送れるようボタンも置く
            var send = new Button
            {
                Content = "送出",
                Padding = new Thickness(14, 4, 14, 4),
                Margin = new Thickness(6, 0, 0, 0)
            };
            send.Click += (s, e) => Committed?.Invoke();

            var cancel = new Button
            {
                Content = "取消",
                Padding = new Thickness(14, 4, 14, 4)
            };
            cancel.Click += (s, e) => Cancelled?.Invoke();

            var right = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            right.Children.Add(cancel);
            right.Children.Add(send);

            var buttons = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(attachBox, Dock.Left);
            buttons.Children.Add(attachBox);
            buttons.Children.Add(right);

            var stack = new StackPanel();
            stack.Children.Add(heading);
            stack.Children.Add(textBox);
            stack.Children.Add(hint);
            stack.Children.Add(buttons);

            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x20, 0x20, 0x20)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Child = stack
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Cancelled?.Invoke();
                }
                else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                {
                    e.Handled = true;
                    Committed?.Invoke();
                }
            };

            Loaded += (s, e) =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
            };
        }
    }
}
