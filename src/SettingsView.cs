using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GalCompanion
{
    /// <summary>
    /// 設定画面。XAML を持たずコードで組む。DataContext は ConfigViewModel。
    /// </summary>
    public class SettingsView : UserControl
    {
        private readonly Action onResetBubble;

        public SettingsView(Action onResetBubble = null)
        {
            this.onResetBubble = onResetBubble;

            var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 10) };

            panel.Children.Add(Header("截圖"));
            panel.Children.Add(CheckRow("按 📷 / 📝 先開輸入框，再按一次才送出", "Settings.CaptureWithNote",
                "可以在遊戲當下直接寫一句註解，跟截圖一起送到 Trilium："
                + "📷 進遊戲筆記、📝 進翻譯問題。不寫就是連按兩次。"
                + "輸入框裡可以取消「附上截圖」只送文字。需要啟用 Trilium 才生效。"));
            panel.Children.Add(CheckRow("截圖後播提示音", "Settings.PlaySound", null));
            panel.Children.Add(CheckRow("只截遊戲畫面（不含視窗邊框）", "Settings.ClientAreaOnly", null));
            panel.Children.Add(CheckRow("左鍵截圖也存一份 PNG 到本機", "Settings.SaveToFile",
                "Playnite 本身不會顯示這些檔案，搭配 Screenshot Visualizer 之類的擴充才看得到。"));
            panel.Children.Add(TextRow("截圖存放根目錄", "Settings.ScreenshotRoot",
                "留空 = Playnite 的 ExtraMetadata 目錄。"));
            panel.Children.Add(TextRow("截圖模式", "Settings.CaptureMode",
                "auto（先試 PrintWindow，全黑就改抓螢幕）／printwindow／screencrop。"));
            panel.Children.Add(TextRow("全域熱鍵", "Settings.Hotkey",
                "例 Shift+F12、Ctrl+Alt+S。留空 = 不用熱鍵，只用氣泡窗。"));

            panel.Children.Add(Header("氣泡窗"));
            panel.Children.Add(CheckRow("遊戲進行中顯示氣泡窗", "Settings.ShowBubble", null));
            panel.Children.Add(TextRow("平時透明度", "Settings.BubbleOpacity",
                "0.1 到 1.0。滑鼠移上去一律不透明。"));
            panel.Children.Add(ResetBubbleRow());

            panel.Children.Add(Header("Trilium"));
            panel.Children.Add(Note(
                "📷 寫進當天的心得筆記，📝 寫進心得底下的子議題；兩顆都可以「截圖＋註解」一起送。"
                + "當天的日記由 Trilium 自己的日期筆記端點決定，端點不能用時才退回標題比對。"));
            panel.Children.Add(CheckRow("啟用 Trilium", "Settings.TriliumEnabled", null));
            panel.Children.Add(TextRow("伺服器網址", "Settings.TriliumUrl", "例 https://trilium.example.com"));
            panel.Children.Add(TextRow("ETAPI token", "Settings.TriliumToken", "Trilium → Options → ETAPI 產生。"));
            panel.Children.Add(TextRow("找不到當天日記時的父 note id", "Settings.TriliumParentNoteId",
                "從 Trilium 網址列的 #root/xxxxx 抓最後那段。"));
            panel.Children.Add(TextRow("日期格式", "Settings.TriliumDateFormat",
                "用來比對日記標題，例 yyyy.MM.dd。"));
            panel.Children.Add(TextRow("心得筆記標題", "Settings.TriliumImpressionsTitle", "📷 寫這裡。"));
            panel.Children.Add(TextRow("子議題筆記標題", "Settings.TriliumTranslationTitle", "📝 寫這裡。"));
            panel.Children.Add(CheckRow("截圖自動送 Trilium", "Settings.TriliumSendScreenshots",
                "關掉的話只有 📝 手動記錄會送。"));

            panel.Children.Add(Header("存檔同步"));
            panel.Children.Add(CheckRow("啟用存檔同步", "Settings.SaveSyncEnabled", null));
            panel.Children.Add(TextRow("rclone 路徑", "Settings.RclonePath", "在 PATH 裡就留 rclone。"));
            panel.Children.Add(TextRow("rclone remote", "Settings.RcloneRemote", "例 nas:playnite-saves"));
            panel.Children.Add(TextRow("時間比對容差（秒）", "Settings.SaveSyncToleranceSeconds",
                "不要小於 3，zip 的時間戳只有 2 秒解析度。"));
            panel.Children.Add(CheckRow("在 NAS 保留每次推送的歷史版本", "Settings.SaveSyncKeepHistory", null));
            panel.Children.Add(Note("每款遊戲的存檔路徑規則仍要在 config.json 的 SaveRules 編輯。"));

            panel.Children.Add(Header("日區啟動"));
            panel.Children.Add(TextRow("LEProc.exe 路徑", "Settings.LocaleEmulatorPath",
                "填了遊戲右鍵選單才會出現 Locale Emulator 轉換。"));
            panel.Children.Add(TextRow("LE profile GUID", "Settings.LocaleEmulatorProfileGuid",
                "留空 = 用 LE 設定的預設 profile。"));

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel
            };
        }

        private FrameworkElement ResetBubbleRow()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            var button = new Button
            {
                Content = "把氣泡窗移回畫面中央",
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var done = new TextBlock
            {
                Text = "已重置，下次顯示會出現在中央。",
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed
            };
            button.Click += (s, e) =>
            {
                onResetBubble?.Invoke();
                done.Visibility = Visibility.Visible;
            };

            stack.Children.Add(button);
            stack.Children.Add(new TextBlock
            {
                Text = "換螢幕或改解析度後氣泡窗可能跑到看不見的地方，用這個拉回來。",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 0)
            });
            stack.Children.Add(done);
            return stack;
        }

        private static TextBlock Header(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 16, 0, 4)
            };
        }

        private static TextBlock Note(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private static FrameworkElement TextRow(string label, string path, string hint)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            stack.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 2) });

            var box = new TextBox();
            box.SetBinding(TextBox.TextProperty, new Binding(path)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            stack.Children.Add(box);

            if (!string.IsNullOrEmpty(hint))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = hint,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            return stack;
        }

        private static FrameworkElement CheckRow(string label, string path, string hint)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            var check = new CheckBox { Content = label };
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding(path)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            stack.Children.Add(check);

            if (!string.IsNullOrEmpty(hint))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = hint,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7,
                    Margin = new Thickness(20, 2, 0, 0)
                });
            }
            return stack;
        }
    }
}
