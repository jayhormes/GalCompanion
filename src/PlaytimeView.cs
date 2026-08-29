using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GalCompanion
{
    /// <summary>
    /// 遊んだ量のカレンダー（GitHub の草と同じ並べ方）と、いちばん遊んだタイトル。
    /// LunaTranslator にあった図の置き換え。
    /// </summary>
    internal sealed class PlaytimeView : UserControl
    {
        private const int CellSize = 12;
        private const int CellGap = 3;
        private const int Weeks = 53;

        private static readonly Color Base = Color.FromRgb(0x3A, 0x7B, 0xD5);

        public PlaytimeView(List<PlaySession> sessions)
        {
            var all = sessions ?? new List<PlaySession>();
            var daily = SessionLog.DailyTotals(all);
            var cells = HeatmapLayout.Build(daily, DateTime.Now.Date, Weeks);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(Title("遊玩時間"));
            panel.Children.Add(Summary(all, daily));
            panel.Children.Add(new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 12, 0, 0),
                Content = Grid(cells),
            });
            panel.Children.Add(Legend());
            panel.Children.Add(Title("玩最久的"));
            panel.Children.Add(TopGames(all));

            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel,
            };
        }

        private static TextBlock Title(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 16, 0, 8),
            };
        }

        private static UIElement Summary(List<PlaySession> sessions, Dictionary<DateTime, long> daily)
        {
            long total = 0;
            foreach (var session in sessions)
            {
                total += session.Seconds;
            }
            var days = daily.Count(p => p.Value > 0);

            return new TextBlock
            {
                Text = $"共 {FormatHours(total)}　{sessions.Count} 次　有玩的日子 {days} 天",
                Opacity = 0.8,
            };
        }

        private static UIElement Grid(List<HeatmapCell> cells)
        {
            var canvas = new Canvas
            {
                Height = HeatmapLayout.Days * (CellSize + CellGap),
                Width = Math.Max(1, (cells.Count == 0 ? 0 : cells[cells.Count - 1].Week + 1))
                        * (CellSize + CellGap),
            };

            foreach (var cell in cells)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = CellSize,
                    Height = CellSize,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = new SolidColorBrush(Shade(cell.Level)),
                    ToolTip = $"{cell.Date:yyyy-MM-dd}　{FormatHours(cell.Seconds)}",
                };
                Canvas.SetLeft(rect, cell.Week * (CellSize + CellGap));
                Canvas.SetTop(rect, cell.Day * (CellSize + CellGap));
                canvas.Children.Add(rect);
            }
            return canvas;
        }

        private static UIElement Legend()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0),
            };
            row.Children.Add(new TextBlock { Text = "少", Opacity = 0.6, Margin = new Thickness(0, 0, 6, 0) });
            for (var level = 0; level <= 4; level++)
            {
                row.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = CellSize,
                    Height = CellSize,
                    RadiusX = 2,
                    RadiusY = 2,
                    Margin = new Thickness(0, 0, CellGap, 0),
                    Fill = new SolidColorBrush(Shade(level)),
                });
            }
            row.Children.Add(new TextBlock { Text = "多", Opacity = 0.6, Margin = new Thickness(3, 0, 0, 0) });
            return row;
        }

        private static UIElement TopGames(List<PlaySession> sessions)
        {
            var panel = new StackPanel();
            var byGame = sessions
                .GroupBy(s => s.GameId)
                .Select(g => new
                {
                    Name = g.Select(s => s.GameName).LastOrDefault(n => !string.IsNullOrWhiteSpace(n))
                           ?? g.Key.ToString(),
                    Seconds = g.Sum(s => (long)s.Seconds),
                })
                .OrderByDescending(g => g.Seconds)
                .Take(15)
                .ToList();

            if (byGame.Count == 0)
            {
                panel.Children.Add(new TextBlock { Text = "還沒有紀錄。", Opacity = 0.7 });
                return panel;
            }

            var max = byGame[0].Seconds;
            foreach (var game in byGame)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

                var hours = new TextBlock
                {
                    Text = FormatHours(game.Seconds),
                    Width = 70,
                    TextAlignment = TextAlignment.Right,
                    Opacity = 0.8,
                };
                DockPanel.SetDock(hours, Dock.Right);
                row.Children.Add(hours);

                var bar = new System.Windows.Shapes.Rectangle
                {
                    Height = 14,
                    RadiusX = 2,
                    RadiusY = 2,
                    Width = Math.Max(2, 240.0 * game.Seconds / Math.Max(1, max)),
                    Fill = new SolidColorBrush(Shade(3)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 8, 0),
                };
                DockPanel.SetDock(bar, Dock.Left);
                row.Children.Add(bar);

                row.Children.Add(new TextBlock
                {
                    Text = game.Name,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                panel.Children.Add(row);
            }
            return panel;
        }

        internal static Color Shade(int level)
        {
            if (level <= 0)
            {
                return Color.FromArgb(0x30, 0x88, 0x88, 0x88);
            }
            // 濃さは 4 段。透明度で出すのでテーマの背景に馴染む
            var alpha = (byte)(0x40 + 0x30 * Math.Min(level, 4));
            return Color.FromArgb(alpha, Base.R, Base.G, Base.B);
        }

        internal static string FormatHours(long seconds)
        {
            if (seconds <= 0)
            {
                return "0h";
            }
            if (seconds < 3600)
            {
                return (seconds / 60) + "m";
            }
            return (seconds / 3600.0).ToString("0.0", CultureInfo.InvariantCulture) + "h";
        }
    }
}
