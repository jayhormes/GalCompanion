using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace GalCompanion
{
    public class GalCompanionPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("80cdee03-e216-4df2-b247-a56056f61543");

        private GalCompanionConfig config;
        private HotkeyListener hotkey;
        private BubbleWindow bubble;
        private TriliumService trilium;
        private Game runningGame;

        public GalCompanionPlugin(IPlayniteAPI api) : base(api)
        {
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            config = LoadPluginSettings<GalCompanionConfig>();
            if (config == null)
            {
                config = new GalCompanionConfig();
                SavePluginSettings(config);
            }
            if (config.TriliumNoteBindings == null)
            {
                config.TriliumNoteBindings = new Dictionary<string, string>();
            }

            if (config.TriliumEnabled
                && !string.IsNullOrWhiteSpace(config.TriliumUrl)
                && !string.IsNullOrWhiteSpace(config.TriliumToken))
            {
                trilium = new TriliumService(new TriliumClient(config.TriliumUrl, config.TriliumToken));
                logger.Info("GalCompanion Trilium 整合已啟用");
            }

            if (string.IsNullOrWhiteSpace(config.Hotkey))
            {
                return;
            }

            try
            {
                hotkey = HotkeyListener.Register(config.Hotkey, CaptureRecord);
                logger.Info($"GalCompanion 截圖熱鍵已註冊：{config.Hotkey}");
            }
            catch (Exception e)
            {
                logger.Error(e, "GalCompanion 熱鍵註冊失敗");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "galcompanion-hotkey-error",
                    $"GalCompanion 熱鍵註冊失敗：{e.Message}",
                    NotificationType.Error));
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            hotkey?.Dispose();
            hotkey = null;
            CloseBubble();
            trilium?.Dispose();
            trilium = null;
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            runningGame = args.Game;
            ShowBubble();
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (runningGame != null && runningGame.Id == args.Game.Id)
            {
                runningGame = null;
            }
            if (runningGame == null)
            {
                HideBubble();
            }
        }

        public override void Dispose()
        {
            hotkey?.Dispose();
            hotkey = null;
            CloseBubble();
            trilium?.Dispose();
            trilium = null;
            base.Dispose();
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }

        private void ShowBubble()
        {
            if (config?.ShowBubble != true)
            {
                return;
            }
            RunOnUi(() =>
            {
                if (bubble == null)
                {
                    bubble = new BubbleWindow(
                        CaptureRecord,
                        CaptureClipboard,
                        trilium == null ? (Action)null : OpenNoteInput,
                        GalCompanionConfig.ClampOpacity(config.BubbleOpacity));
                    bubble.Moved += (x, y) =>
                    {
                        config.BubbleX = x;
                        config.BubbleY = y;
                        SavePluginSettings(config);
                    };
                    if (config.BubbleX.HasValue && config.BubbleY.HasValue)
                    {
                        bubble.Left = config.BubbleX.Value;
                        bubble.Top = config.BubbleY.Value;
                    }
                    else
                    {
                        bubble.Left = SystemParameters.WorkArea.Right - 120;
                        bubble.Top = SystemParameters.WorkArea.Top + 16;
                    }
                }
                bubble.Show();
            });
        }

        private void HideBubble()
        {
            RunOnUi(() => bubble?.Hide());
        }

        private void CloseBubble()
        {
            RunOnUi(() =>
            {
                bubble?.Close();
                bubble = null;
            });
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = "打開截圖資料夾",
                MenuSection = "GalCompanion",
                Action = a =>
                {
                    foreach (var game in a.Games)
                    {
                        var dir = GetScreenshotDir(game);
                        Directory.CreateDirectory(dir);
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
            };
        }

        private string GetScreenshotDir(Game game)
        {
            return ScreenshotPaths.GetDir(
                config?.ScreenshotRoot,
                Path.Combine(PlayniteApi.Paths.ConfigurationPath, "ExtraMetadata"),
                game?.Id);
        }

        // 左鍵/熱鍵：記錄（Trilium、可選本地歸檔）；右鍵：只進剪貼簿
        private void CaptureRecord()
        {
            Capture(clipboardOnly: false);
        }

        private void CaptureClipboard()
        {
            Capture(clipboardOnly: true);
        }

        private void Capture(bool clipboardOnly)
        {
            try
            {
                using (var bmp = CaptureService.CaptureForegroundWindow(config.CaptureMode, config.ClientAreaOnly))
                {
                    if (bmp == null)
                    {
                        logger.Warn("GalCompanion 截圖失敗：抓不到前景視窗畫面");
                        return;
                    }

                    if (clipboardOnly)
                    {
                        CopyToClipboard(bmp);
                    }
                    else
                    {
                        byte[] pngBytes;
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            pngBytes = ms.ToArray();
                        }

                        var recorded = false;
                        if (config.SaveToFile)
                        {
                            var dir = GetScreenshotDir(runningGame);
                            Directory.CreateDirectory(dir);
                            var path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                            File.WriteAllBytes(path, pngBytes);
                            logger.Info($"GalCompanion 截圖已存：{path}");
                            recorded = true;
                        }
                        if (trilium != null && config.TriliumSendScreenshots && runningGame != null)
                        {
                            SendToTrilium(pngBytes, null);
                            recorded = true;
                        }
                        if (!recorded)
                        {
                            // 沒有任何記錄目的地時退回剪貼簿，避免按了沒效果
                            CopyToClipboard(bmp);
                        }
                    }

                    if (config.PlaySound)
                    {
                        SystemSounds.Asterisk.Play();
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "GalCompanion 截圖失敗");
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "galcompanion-capture-error",
                    $"GalCompanion 截圖失敗：{e.Message}",
                    NotificationType.Error));
            }
        }

        private void OpenNoteInput()
        {
            RunOnUi(() =>
            {
                var win = new NoteInputWindow(runningGame?.Name);
                if (win.ShowDialog() == true && !string.IsNullOrWhiteSpace(win.NoteText))
                {
                    SendToTrilium(null, win.NoteText);
                }
            });
        }

        // 背景送出，不擋 UI；失敗以 Playnite 通知回報
        private void SendToTrilium(byte[] pngBytes, string text)
        {
            var service = trilium;
            var game = runningGame;
            if (service == null || game == null)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var gameKey = game.Id.ToString();
                    config.TriliumNoteBindings.TryGetValue(gameKey, out var bound);
                    var noteId = await service.EnsureGameNoteAsync(config.TriliumParentNoteId, game.Name, bound);
                    if (noteId != bound)
                    {
                        config.TriliumNoteBindings[gameKey] = noteId;
                        RunOnUi(() => SavePluginSettings(config));
                    }
                    await service.AppendEntryAsync(noteId, DateTime.Now, pngBytes, text);
                    logger.Info($"GalCompanion 已寫入 Trilium note {noteId}");
                }
                catch (Exception e)
                {
                    logger.Error(e, "GalCompanion Trilium 寫入失敗");
                    RunOnUi(() => PlayniteApi.Notifications.Add(new NotificationMessage(
                        "galcompanion-trilium-error",
                        $"GalCompanion Trilium 寫入失敗：{e.Message}",
                        NotificationType.Error)));
                }
            });
        }

        private static void CopyToClipboard(System.Drawing.Bitmap bmp)
        {
            var hBitmap = bmp.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();

                // 剪貼簿被其他程式短暫鎖住時會丟 COMException，重試幾次
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        Clipboard.SetImage(source);
                        return;
                    }
                    catch (Exception) when (attempt < 3)
                    {
                        Thread.Sleep(50);
                    }
                }
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }
    }
}
