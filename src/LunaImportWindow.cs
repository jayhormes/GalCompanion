using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GalCompanion
{
    /// <summary>
    /// 取り込む前に何がどう当たったかを見せる。自動で当てにいく以上、
    /// 目視できないと事故に気づけない。
    /// </summary>
    internal sealed class LunaImportWindow : Window
    {
        private readonly CheckBox overwriteBox;
        private readonly CheckBox sessionsBox;
        private readonly TextBlock summary;
        private readonly StackPanel report;
        private readonly Button apply;

        public bool Overwrite => overwriteBox.IsChecked == true;
        public bool WriteSessions => sessionsBox.IsChecked == true;

        /// <summary>チェックを変えたら計画を組み直してもらう。</summary>
        public event Action ReplanRequested;

        public LunaImportWindow()
        {
            Title = "從 LunaTranslator 匯入遊玩時間";
            Width = 720;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            summary = new TextBlock { Margin = new Thickness(12, 12, 12, 6), TextWrapping = TextWrapping.Wrap };

            report = new StackPanel { Margin = new Thickness(12, 0, 12, 12) };
            var scroll = new ScrollViewer
            {
                Content = report,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            overwriteBox = new CheckBox
            {
                Content = "連 Playnite 已經有時數的也覆蓋",
                Margin = new Thickness(12, 0, 12, 4),
            };
            overwriteBox.Checked += (s, e) => Replan();
            overwriteBox.Unchecked += (s, e) => Replan();

            sessionsBox = new CheckBox
            {
                Content = "同時寫入逐次遊玩紀錄（熱力圖會看到真實的歷史分布）",
                IsChecked = true,
                Margin = new Thickness(12, 0, 12, 8),
            };

            apply = new Button
            {
                Content = "寫入",
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(6, 0, 12, 12),
                IsDefault = true,
            };
            apply.Click += (s, e) => DialogResult = true;

            var cancel = new Button
            {
                Content = "取消",
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(12, 0, 0, 12),
                IsCancel = true,
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(apply);

            var dock = new DockPanel();
            DockPanel.SetDock(summary, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            DockPanel.SetDock(sessionsBox, Dock.Bottom);
            DockPanel.SetDock(overwriteBox, Dock.Bottom);
            dock.Children.Add(summary);
            dock.Children.Add(buttons);
            dock.Children.Add(sessionsBox);
            dock.Children.Add(overwriteBox);
            dock.Children.Add(scroll);
            Content = dock;
        }

        private void Replan()
        {
            var handler = ReplanRequested;
            if (handler != null)
            {
                handler();
            }
        }

        public void SetPlan(List<PlanEntry> plan)
        {
            var writable = plan.Count(p => p.Action == PlanAction.Write);
            summary.Text = writable > 0
                ? $"會寫入 {writable} 款。確認下面的配對沒問題再按「寫入」。"
                : "沒有要寫入的項目。";
            apply.IsEnabled = writable > 0;

            report.Children.Clear();
            foreach (var group in plan.GroupBy(p => p.Action).OrderBy(g => (int)g.Key))
            {
                report.Children.Add(new TextBlock
                {
                    Text = $"{LunaImportService.Describe(group.Key)}（{group.Count()}）",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 4),
                });
                foreach (var entry in group.OrderByDescending(e => e.LunaSeconds))
                {
                    report.Children.Add(new TextBlock
                    {
                        Text = LunaImportService.Describe(entry),
                        Margin = new Thickness(12, 1, 0, 1),
                    });
                }
            }
        }
    }
}
