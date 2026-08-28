using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GalCompanion
{
    internal sealed class NoteInputWindow : Window
    {
        private readonly TextBox textBox;

        public string NoteText => textBox.Text;

        public NoteInputWindow(string gameTitle)
        {
            Title = string.IsNullOrEmpty(gameTitle) ? "記一筆" : $"記一筆 — {gameTitle}";
            Width = 460;
            Height = 220;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(10, 10, 10, 0),
                FontSize = 14
            };

            var submit = new Button
            {
                Content = "送出（Ctrl+Enter）",
                Margin = new Thickness(10),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            submit.Click += (s, e) => Submit();

            var dock = new DockPanel();
            DockPanel.SetDock(submit, Dock.Bottom);
            dock.Children.Add(submit);
            dock.Children.Add(textBox);
            Content = dock;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                }
                else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    Submit();
                }
            };
            Loaded += (s, e) => textBox.Focus();
        }

        private void Submit()
        {
            DialogResult = !string.IsNullOrWhiteSpace(textBox.Text);
        }
    }
}
