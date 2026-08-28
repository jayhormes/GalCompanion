using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
        private SaveSyncService saveSync;
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
            if (config.SaveRules == null)
            {
                config.SaveRules = new Dictionary<string, SaveRule>();
            }

            if (config.TriliumEnabled
                && !string.IsNullOrWhiteSpace(config.TriliumUrl)
                && !string.IsNullOrWhiteSpace(config.TriliumToken))
            {
                trilium = new TriliumService(new TriliumClient(config.TriliumUrl, config.TriliumToken));
                logger.Info("GalCompanion Trilium 整合已啟用");
            }

            if (config.SaveSyncEnabled && !string.IsNullOrWhiteSpace(config.RcloneRemote))
            {
                saveSync = new SaveSyncService(
                    new RcloneRunner(config.RclonePath),
                    new SyncStateStore(Path.Combine(GetPluginUserDataPath(), "syncstate")),
                    config.RcloneRemote,
                    Environment.MachineName,
                    TimeSpan.FromSeconds(Math.Max(3, config.SaveSyncToleranceSeconds)),
                    Path.Combine(GetPluginUserDataPath(), "syncwork"),
                    config.SaveSyncKeepHistory);
                logger.Info("GalCompanion 存檔同步已啟用");
                Task.Run(() => CatchUpPush());
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

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (saveSync == null || args?.Game == null)
            {
                return;
            }
            var paths = ResolveRulePaths(args.Game);
            if (paths == null)
            {
                return;
            }

            try
            {
                var gameId = args.Game.Id.ToString();
                var plan = saveSync.Plan(gameId, paths);
                switch (plan.Action)
                {
                    case SyncAction.Pull:
                        saveSync.Pull(gameId, paths, plan.Remote);
                        logger.Info($"GalCompanion 已拉取存檔：{args.Game.Name}（{plan.Remote.Device} @ {plan.Remote.TimestampUtc:u}）");
                        break;
                    case SyncAction.Push:
                        // 上次漏推（當機/斷網），先補推再玩
                        saveSync.Push(gameId, paths);
                        logger.Info($"GalCompanion 啟動前補推存檔：{args.Game.Name}");
                        break;
                    case SyncAction.Conflict:
                        HandleConflict(args, paths, plan);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"GalCompanion 存檔同步失敗：{args.Game.Name}");
                var result = PlayniteApi.Dialogs.ShowMessage(
                    $"「{args.Game.Name}」存檔同步失敗：{e.Message}\n\n仍要啟動遊戲嗎？（本地存檔可能不是最新）",
                    "GalCompanion",
                    MessageBoxButton.YesNo);
                if (result == MessageBoxResult.No)
                {
                    args.CancelStartup = true;
                }
            }
        }

        private void HandleConflict(OnGameStartingEventArgs args, List<string> paths, SyncPlan plan)
        {
            var localText = plan.LocalMtimeUtc == null
                ? "（無）"
                : plan.LocalMtimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var text = $"「{args.Game.Name}」兩邊存檔都有變動：\n\n" +
                       $"本機：{localText}（{Environment.MachineName}）\n" +
                       $"NAS：{plan.Remote.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm}（{plan.Remote.Device}）\n\n" +
                       "是＝用 NAS 的（本機現況會先備份）\n否＝用本機的\n取消＝不啟動遊戲";
            var result = PlayniteApi.Dialogs.ShowMessage(text, "GalCompanion 存檔衝突", MessageBoxButton.YesNoCancel);
            if (result == MessageBoxResult.Yes)
            {
                saveSync.Pull(args.Game.Id.ToString(), paths, plan.Remote);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                args.CancelStartup = true;
            }
            // 否：不動本機，遊戲結束照常推；NAS 舊版留在 history
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
            PushSaves(args.Game);
        }

        private List<string> ResolveRulePaths(Game game)
        {
            SaveRule rule;
            if (!config.SaveRules.TryGetValue(game.Id.ToString(), out rule))
            {
                return null;
            }
            var paths = SavePathResolver.Resolve(rule.Paths, game.InstallDirectory);
            return paths.Count == 0 ? null : paths;
        }

        private void PushSaves(Game game)
        {
            if (saveSync == null || game == null)
            {
                return;
            }
            var paths = ResolveRulePaths(game);
            if (paths == null)
            {
                return;
            }
            var gameId = game.Id.ToString();
            var gameName = game.Name;
            Task.Run(() =>
            {
                try
                {
                    saveSync.Push(gameId, paths);
                    logger.Info($"GalCompanion 存檔已推送：{gameName}");
                }
                catch (Exception e)
                {
                    logger.Error(e, $"GalCompanion 存檔推送失敗：{gameName}");
                    RunOnUi(() => PlayniteApi.Notifications.Add(new NotificationMessage(
                        "galcompanion-sync-push-error",
                        $"GalCompanion 存檔推送失敗（{gameName}）：{e.Message}，下次啟動 Playnite 會自動補推",
                        NotificationType.Error)));
                }
            });
        }

        // Playnite 啟動時掃一輪：上次沒推成功的（當機、斷網）補推
        private void CatchUpPush()
        {
            foreach (var pair in config.SaveRules)
            {
                try
                {
                    if (!Guid.TryParse(pair.Key, out var gameGuid))
                    {
                        continue;
                    }
                    var game = PlayniteApi.Database.Games.Get(gameGuid);
                    if (game == null)
                    {
                        continue;
                    }
                    var paths = SavePathResolver.Resolve(pair.Value.Paths, game.InstallDirectory);
                    if (paths.Count == 0)
                    {
                        continue;
                    }
                    var plan = saveSync.Plan(pair.Key, paths);
                    if (plan.Action == SyncAction.Push)
                    {
                        saveSync.Push(pair.Key, paths);
                        logger.Info($"GalCompanion 補推存檔：{game.Name}");
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, $"GalCompanion 補推失敗：{pair.Key}");
                }
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

            if (!string.IsNullOrWhiteSpace(config.LocaleEmulatorPath))
            {
                yield return new GameMenuItem
                {
                    Description = "改用 Locale Emulator 啟動",
                    MenuSection = "GalCompanion",
                    Action = a => ConvertToLocaleEmulator(a.Games)
                };
                yield return new GameMenuItem
                {
                    Description = "還原直接啟動",
                    MenuSection = "GalCompanion",
                    Action = a => RevertLocaleEmulator(a.Games)
                };
            }

            if (saveSync != null && args.Games.Count == 1 && ResolveRulePaths(args.Games[0]) != null)
            {
                yield return new GameMenuItem
                {
                    Description = "立即推送存檔",
                    MenuSection = "GalCompanion",
                    Action = a => PushSaves(a.Games[0])
                };
                yield return new GameMenuItem
                {
                    Description = "從遠端拉取存檔（覆蓋本機，先備份）",
                    MenuSection = "GalCompanion",
                    Action = a => ManualPull(a.Games[0])
                };
            }
        }

        // 原本的啟動動作降為備用，插入 LEProc 動作當 Play；支援多選批次
        private void ConvertToLocaleEmulator(List<Game> games)
        {
            var converted = 0;
            var skipped = 0;
            foreach (var game in games)
            {
                var original = game.GameActions?.FirstOrDefault(x => x.Type == GameActionType.File && x.IsPlayAction);
                if (original == null
                    || game.GameActions.Any(x => x.Name == LocaleEmulatorActions.ActionName))
                {
                    skipped++;
                    continue;
                }
                original.IsPlayAction = false;
                game.GameActions.Insert(0, new GameAction
                {
                    Name = LocaleEmulatorActions.ActionName,
                    Type = GameActionType.File,
                    Path = config.LocaleEmulatorPath,
                    Arguments = LocaleEmulatorActions.BuildArguments(config.LocaleEmulatorProfileGuid, original.Path),
                    WorkingDir = string.IsNullOrWhiteSpace(original.WorkingDir) ? "{InstallDir}" : original.WorkingDir,
                    IsPlayAction = true
                });
                PlayniteApi.Database.Games.Update(game);
                converted++;
            }
            PlayniteApi.Dialogs.ShowMessage(
                $"已轉換 {converted} 款、跳過 {skipped} 款（沒有可轉的檔案啟動動作，或已轉過）。",
                "GalCompanion");
        }

        private void RevertLocaleEmulator(List<Game> games)
        {
            var reverted = 0;
            foreach (var game in games)
            {
                var leActions = game.GameActions?
                    .Where(x => x.Name == LocaleEmulatorActions.ActionName)
                    .ToList();
                if (leActions == null || leActions.Count == 0)
                {
                    continue;
                }
                foreach (var action in leActions)
                {
                    game.GameActions.Remove(action);
                }
                var first = game.GameActions.FirstOrDefault(x => x.Type == GameActionType.File);
                if (first != null)
                {
                    first.IsPlayAction = true;
                }
                PlayniteApi.Database.Games.Update(game);
                reverted++;
            }
            PlayniteApi.Dialogs.ShowMessage($"已還原 {reverted} 款。", "GalCompanion");
        }

        private void ManualPull(Game game)
        {
            var paths = ResolveRulePaths(game);
            if (paths == null)
            {
                return;
            }
            try
            {
                var gameId = game.Id.ToString();
                var plan = saveSync.Plan(gameId, paths);
                if (plan.Remote == null)
                {
                    PlayniteApi.Dialogs.ShowMessage($"「{game.Name}」遠端沒有存檔。", "GalCompanion");
                    return;
                }
                var result = PlayniteApi.Dialogs.ShowMessage(
                    $"用 NAS 的存檔（{plan.Remote.Device} @ {plan.Remote.TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm}）覆蓋本機？\n本機現況會先備份。",
                    "GalCompanion",
                    MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    saveSync.Pull(gameId, paths, plan.Remote);
                    PlayniteApi.Dialogs.ShowMessage($"「{game.Name}」存檔已拉取。", "GalCompanion");
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"GalCompanion 手動拉取失敗：{game.Name}");
                PlayniteApi.Dialogs.ShowMessage($"拉取失敗：{e.Message}", "GalCompanion");
            }
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
