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
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GalCompanion
{
    public class GalCompanionPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("80cdee03-e216-4df2-b247-a56056f61543");

        private ConfigViewModel settings;
        private GalCompanionConfig config => settings?.Settings;
        private HotkeyListener hotkey;
        private BubbleWindow bubble;
        private TriliumService trilium;

        // 日期＋種別 → noteId。同じ日に何度も撮るので解決結果を使い回す
        private readonly Dictionary<string, string> triliumNoteCache = new Dictionary<string, string>();
        private SaveSyncService saveSync;
        private Game runningGame;

        // 📷 の 2 段押し（入力欄 → 送出）
        private readonly CaptureComposer composer;
        private ComposerWindow composerWindow;
        // 入力欄にフォーカスが移るとゲームが前景から外れるので、開いた時点の窓を覚えておく
        private IntPtr composeTarget;

        // 入力欄を閉じてから撮るまでの待ち。screencrop で入力欄が写り込むのを防ぐ
        private const int CaptureSettleMs = 120;

        public GalCompanionPlugin(IPlayniteAPI api) : base(api)
        {
            // Load/SavePluginSettings は ExtensionsData/<id>/config.json を読み書きするので、
            // 手で書いた config.json もそのまま引き継がれる
            settings = new ConfigViewModel(
                () => LoadPluginSettings<GalCompanionConfig>(),
                saved =>
                {
                    SavePluginSettings(saved);
                    ApplySettings();
                });

            // 本文の行き先が Trilium しかないので、そこが無効なら 1 回押しの素の截圖に戻す
            composer = new CaptureComposer(
                () => config?.CaptureWithNote == true
                    && config.TriliumSendScreenshots
                    && trilium != null);

            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SettingsView(ResetBubblePosition) { DataContext = settings };
        }

        // 設定保存後に呼ぶ。ホットキー・Trilium・同期を今の設定で作り直す
        private void ApplySettings()
        {
            hotkey?.Dispose();
            hotkey = null;
            trilium?.Dispose();
            trilium = null;
            saveSync = null;
            lock (triliumNoteCache)
            {
                triliumNoteCache.Clear();
            }

            InitTrilium();
            InitSaveSync();
            InitHotkey();

            // 透明度などは作り直さないと反映されないので、表示中なら作り直す
            RunOnUi(() =>
            {
                var wasVisible = bubble != null && bubble.IsVisible;
                CancelComposer();
                CloseBubbleCore();
                if (wasVisible && runningGame != null)
                {
                    ShowBubble();
                }
            });
        }

        /// <summary>気泡ウィンドウの保存座標を捨て、表示中なら即座に中央へ戻す。</summary>
        private void ResetBubblePosition()
        {
            settings.ResetBubblePosition();
            SavePluginSettings(config);
            RunOnUi(() =>
            {
                if (bubble == null)
                {
                    return;
                }
                double left, top;
                BubblePlacement.Center(bubble.ActualWidth, bubble.ActualHeight, WorkArea(), out left, out top);
                bubble.Left = left;
                bubble.Top = top;
            });
        }

        private static ScreenRect WorkArea()
        {
            var area = SystemParameters.WorkArea;
            return new ScreenRect(area.Left, area.Top, area.Width, area.Height);
        }

        private static ScreenRect VirtualScreen()
        {
            return new ScreenRect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            InitTrilium();
            InitSaveSync();
            InitHotkey();
        }

        private void InitTrilium()
        {
            if (config.TriliumEnabled
                && !string.IsNullOrWhiteSpace(config.TriliumUrl)
                && !string.IsNullOrWhiteSpace(config.TriliumToken))
            {
                trilium = new TriliumService(
                    new TriliumClient(config.TriliumUrl, config.TriliumToken),
                    config.TriliumDateFormat,
                    config.TriliumImpressionsTitle,
                    config.TriliumTranslationTitle);
                logger.Info("GalCompanion Trilium 整合已啟用");
            }
        }

        private void InitSaveSync()
        {
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
        }

        private void InitHotkey()
        {
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
                        OpenNoteInput,
                        GalCompanionConfig.ClampOpacity(config.BubbleOpacity));
                    bubble.Moved += (x, y) =>
                    {
                        config.BubbleX = x;
                        config.BubbleY = y;
                        SavePluginSettings(config);
                    };
                    // 解像度やモニタ構成が変わると保存座標が画面外に出るので毎回検証する
                    double left, top;
                    BubblePlacement.Resolve(
                        config.BubbleX, config.BubbleY,
                        bubble.ActualWidth, bubble.ActualHeight,
                        VirtualScreen(), WorkArea(), out left, out top);
                    bubble.Left = left;
                    bubble.Top = top;
                }
                bubble.Show();
            });
        }

        private void HideBubble()
        {
            RunOnUi(() =>
            {
                CancelComposer();
                bubble?.Hide();
            });
        }

        private void CloseBubble()
        {
            RunOnUi(CloseBubbleCore);
        }

        // UI スレッド上で呼ぶこと
        private void CloseBubbleCore()
        {
            CancelComposer();
            bubble?.Close();
            bubble = null;
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
            RunOnUi(() =>
            {
                switch (composer.Press())
                {
                    case CapturePress.OpenComposer:
                        OpenComposer();
                        break;
                    case CapturePress.Commit:
                        CommitComposer();
                        break;
                    default:
                        Capture(false, null, IntPtr.Zero);
                        break;
                }
            });
        }

        private void CaptureClipboard()
        {
            // 入力欄が開いている間は前景がゲームではないので、控えておいた窓を撮る
            Capture(true, null, composerWindow != null ? composeTarget : IntPtr.Zero);
        }

        /// <summary>入力欄を気泡の隣に出す。ここではまだ撮らない。</summary>
        private void OpenComposer()
        {
            // 入力欄にフォーカスが移る前に、今のゲーム窓を控えておく
            composeTarget = NativeMethods.GetForegroundWindow();

            var win = new ComposerWindow(runningGame?.Name);
            win.Committed += CommitComposer;
            win.Cancelled += CancelComposer;
            composerWindow = win;

            // 既定位置(0,0)で一瞬光るのを避けるため、出す前に概算で置いてから実寸で直す
            PlaceComposer(win);
            win.Show();
            PlaceComposer(win);
            win.Activate();
            bubble?.SetComposing(true);
        }

        private void PlaceComposer(ComposerWindow win)
        {
            // SizeToContent なので Show 前は実寸が無い。その時は概算で置く
            var height = win.ActualHeight > 0 ? win.ActualHeight : ComposerPlacement.NominalHeight;

            if (bubble == null)
            {
                double cx, cy;
                BubblePlacement.Center(win.Width, height, WorkArea(), out cx, out cy);
                win.Left = cx;
                win.Top = cy;
                return;
            }

            var anchor = new ScreenRect(
                bubble.Left, bubble.Top,
                bubble.ActualWidth > 0 ? bubble.ActualWidth : BubblePlacement.NominalWidth,
                bubble.ActualHeight > 0 ? bubble.ActualHeight : BubblePlacement.NominalHeight);

            double left, top;
            ComposerPlacement.Resolve(anchor, win.Width, height, WorkArea(), out left, out top);
            win.Left = left;
            win.Top = top;
        }

        /// <summary>入力欄の本文を添えて撮る。UI スレッド上で呼ぶこと。</summary>
        private void CommitComposer()
        {
            var win = composerWindow;
            if (win == null)
            {
                return;
            }
            composer.Cancel();
            var text = win.Text;
            var target = composeTarget;

            // 先に閉じてゲームを前面に戻す。screencrop で入力欄が写り込まないように
            CloseComposerCore();
            SettleBeforeCapture(target);

            Capture(false, text, target);
        }

        /// <summary>書きかけを捨てて閉じる。UI スレッド上で呼ぶこと。</summary>
        private void CancelComposer()
        {
            composer.Cancel();
            CloseComposerCore();
        }

        private void CloseComposerCore()
        {
            var win = composerWindow;
            composerWindow = null;
            if (win == null)
            {
                return;
            }

            win.Committed -= CommitComposer;
            win.Cancelled -= CancelComposer;
            win.Close();
            bubble?.SetComposing(false);

            // フォーカスを奪ったままだとゲームの入力が効かないので必ず戻す
            var target = composeTarget;
            composeTarget = IntPtr.Zero;
            if (target != IntPtr.Zero && NativeMethods.IsWindow(target))
            {
                NativeMethods.SetForegroundWindow(target);
            }
        }

        // 閉じた入力欄が画面から消えるまで待つ。UI スレッドを少し止めるが体感できる長さではない
        private static void SettleBeforeCapture(IntPtr target)
        {
            if (target == IntPtr.Zero)
            {
                return;
            }
            PumpRender();
            Thread.Sleep(CaptureSettleMs);
            PumpRender();
        }

        // Render 優先度までしか流さない。Input を流すと連打が割り込んで
        // 撮っている最中に入力欄がもう一度開いてしまう
        private static void PumpRender()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || !dispatcher.CheckAccess())
            {
                return;
            }
            dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Render);
        }

        private void Capture(bool clipboardOnly, string note, IntPtr target)
        {
            try
            {
                var hwnd = target != IntPtr.Zero ? target : NativeMethods.GetForegroundWindow();
                using (var bmp = CaptureService.CaptureWindow(hwnd, config.CaptureMode, config.ClientAreaOnly))
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
                            SendToTrilium(TriliumTarget.Impressions, pngBytes, note);
                            recorded = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(note))
                        {
                            // 送り先が消えた状態で本文だけ握りつぶすと書いた分が消えるので知らせる
                            logger.Warn("GalCompanion：Trilium が無効なので補充描述を送れませんでした");
                            PlayniteApi.Notifications.Add(new NotificationMessage(
                                "galcompanion-note-dropped",
                                "GalCompanion：Trilium 沒有啟用，補充描述沒有送出。",
                                NotificationType.Error));
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
            if (trilium == null)
            {
                // ボタンは常に出しておき、押されたら設定場所を案内する
                PlayniteApi.Dialogs.ShowMessage(
                    "還沒設定 Trilium，所以記錄沒有地方可以寫。\n\n"
                    + "到「附加元件 → 擴充功能 → GalCompanion」的 Trilium 區塊，"
                    + "勾選啟用並填入伺服器網址與 ETAPI token。",
                    "GalCompanion");
                return;
            }

            RunOnUi(() =>
            {
                var win = new NoteInputWindow(runningGame?.Name);
                if (win.ShowDialog() == true && !string.IsNullOrWhiteSpace(win.NoteText))
                {
                    SendToTrilium(TriliumTarget.Translation, null, win.NoteText);
                }
            });
        }

        // 背景送出，不擋 UI；失敗以 Playnite 通知回報
        private void SendToTrilium(TriliumTarget target, byte[] pngBytes, string text)
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
                    var now = DateTime.Now;
                    var cacheKey = service.FormatDate(now) + "|" + target;
                    string noteId;
                    lock (triliumNoteCache)
                    {
                        triliumNoteCache.TryGetValue(cacheKey, out noteId);
                    }
                    if (string.IsNullOrEmpty(noteId))
                    {
                        noteId = await service.ResolveTargetNoteAsync(now, config.TriliumParentNoteId, target);
                        lock (triliumNoteCache)
                        {
                            triliumNoteCache[cacheKey] = noteId;
                        }
                    }
                    await service.AppendEntryAsync(noteId, now, game.Name, pngBytes, text);
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
