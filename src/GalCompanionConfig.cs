using System;
using System.Collections.Generic;

namespace GalCompanion
{
    // Playnite の Load/SavePluginSettings が ExtensionsData/<id>/config.json を読み書きする。
    // 設定 UI からバインドするので ObservableObject を継承する。
    public class GalCompanionConfig : ObservableObject
    {
        private string hotkey = "Shift+F12";
        private bool showBubble = true;
        private double? bubbleX;
        private double? bubbleY;
        private string captureMode = "auto";
        private bool clientAreaOnly = true;
        private bool saveToFile;
        private bool playSound = true;
        private bool captureWithNote = true;
        private double bubbleOpacity = 0.55;
        private string screenshotRoot = string.Empty;
        private bool triliumEnabled;
        private string triliumUrl = string.Empty;
        private string triliumToken = string.Empty;
        private string triliumParentNoteId = string.Empty;
        private bool triliumSendScreenshots = true;
        private string triliumDateFormat = "yyyy.MM.dd";
        private bool triliumNotePerGame = true;
        private string triliumImpressionsTitle = TriliumTitles.DefaultImpressions;
        private string triliumTranslationTitle = "翻譯問題";
        private bool saveSyncEnabled;
        private bool playtimeSyncEnabled;
        private string rclonePath = "rclone";
        private string rcloneRemote = string.Empty;
        private int saveSyncToleranceSeconds = 3;
        private bool saveSyncKeepHistory = true;
        private Dictionary<string, SaveRule> saveRules = new Dictionary<string, SaveRule>();
        private string localeEmulatorPath = string.Empty;
        private string localeEmulatorProfileGuid = string.Empty;

        // 留空 = 不註冊熱鍵，只用氣泡窗
        public string Hotkey
        {
            get => hotkey;
            set => SetValue(ref hotkey, value);
        }

        public bool ShowBubble
        {
            get => showBubble;
            set => SetValue(ref showBubble, value);
        }

        public double? BubbleX
        {
            get => bubbleX;
            set => SetValue(ref bubbleX, value);
        }

        public double? BubbleY
        {
            get => bubbleY;
            set => SetValue(ref bubbleY, value);
        }

        // auto | printwindow | screencrop
        public string CaptureMode
        {
            get => captureMode;
            set => SetValue(ref captureMode, value);
        }

        public bool ClientAreaOnly
        {
            get => clientAreaOnly;
            set => SetValue(ref clientAreaOnly, value);
        }

        // 本地 PNG 歸檔只有搭配 Screenshot Visualizer 之類擴充或離線備份才有用，預設關
        public bool SaveToFile
        {
            get => saveToFile;
            set => SetValue(ref saveToFile, value);
        }

        // true = 📷 を押すと先に入力欄が出て、もう一度押すと本文つきで送る。
        // その場で一言残せるようにするための 2 段押し。何も書かなければ素の截圖と同じ。
        public bool CaptureWithNote
        {
            get => captureWithNote;
            set => SetValue(ref captureWithNote, value);
        }

        public bool PlaySound
        {
            get => playSound;
            set => SetValue(ref playSound, value);
        }

        // 氣泡窗平時透明度（0.1–1.0），滑鼠移上去恆為 1.0
        public double BubbleOpacity
        {
            get => bubbleOpacity;
            set => SetValue(ref bubbleOpacity, value);
        }

        // 留空 = <Playnite 設定目錄>\ExtraMetadata
        public string ScreenshotRoot
        {
            get => screenshotRoot;
            set => SetValue(ref screenshotRoot, value);
        }

        public bool TriliumEnabled
        {
            get => triliumEnabled;
            set => SetValue(ref triliumEnabled, value);
        }

        // 例 http://nas:8080；token 在 Trilium Options → ETAPI 產生
        public string TriliumUrl
        {
            get => triliumUrl;
            set => SetValue(ref triliumUrl, value);
        }

        public string TriliumToken
        {
            get => triliumToken;
            set => SetValue(ref triliumToken, value);
        }

        // 遊戲筆記的父 note；第一次記錄時在其下自動建該遊戲的子 note
        public string TriliumParentNoteId
        {
            get => triliumParentNoteId;
            set => SetValue(ref triliumParentNoteId, value);
        }

        // 截圖是否自動 append 到 Trilium（false = 只有 📝 手動記錄才送）
        public bool TriliumSendScreenshots
        {
            get => triliumSendScreenshots;
            set => SetValue(ref triliumSendScreenshots, value);
        }

        // 只有 Trilium 的日期筆記端點不可用時，才拿這個格式去比對標題
        public string TriliumDateFormat
        {
            get => triliumDateFormat;
            set => SetValue(ref triliumDateFormat, value);
        }

        // 日期底下的心得 note 標題（📷 截圖寫這裡）
        // true = 心得筆記標題前面自動加上遊戲名，變成「XXX 遊戲心得」，每款遊戲各一則。
        // 想自己決定位置就在標題裡寫 {game}，那時這個開關不生效
        public bool TriliumNotePerGame
        {
            get => triliumNotePerGame;
            set => SetValue(ref triliumNotePerGame, value);
        }

        public string TriliumImpressionsTitle
        {
            get => triliumImpressionsTitle;
            set => SetValue(ref triliumImpressionsTitle, value);
        }

        // 心得底下的子議題 note 標題（📝 文字寫這裡）
        public string TriliumTranslationTitle
        {
            get => triliumTranslationTitle;
            set => SetValue(ref triliumTranslationTitle, value);
        }

        // 遊玩時數同步。走同一個 rclone remote，但機制跟存檔不同：
        // 每台只寫自己的檔案，讀的時候把所有機器的檔案取聯集，不會有衝突要問
        public bool PlaytimeSyncEnabled
        {
            get => playtimeSyncEnabled;
            set => SetValue(ref playtimeSyncEnabled, value);
        }

        public bool SaveSyncEnabled
        {
            get => saveSyncEnabled;
            set => SetValue(ref saveSyncEnabled, value);
        }

        // rclone.exe 路徑；在 PATH 裡就留預設
        public string RclonePath
        {
            get => rclonePath;
            set => SetValue(ref rclonePath, value);
        }

        // rclone remote 加根目錄，例 "nas:playnite-saves"
        public string RcloneRemote
        {
            get => rcloneRemote;
            set => SetValue(ref rcloneRemote, value);
        }

        // zip 時間戳解析度 2 秒，容差必須 ≥ 3
        public int SaveSyncToleranceSeconds
        {
            get => saveSyncToleranceSeconds;
            set => SetValue(ref saveSyncToleranceSeconds, value);
        }

        // NAS 端每次推送另存 history/*.zip
        public bool SaveSyncKeepHistory
        {
            get => saveSyncKeepHistory;
            set => SetValue(ref saveSyncKeepHistory, value);
        }

        // gameId → 存檔路徑規則
        public Dictionary<string, SaveRule> SaveRules
        {
            get => saveRules;
            set => SetValue(ref saveRules, value);
        }

        // LEProc.exe 完整路徑；填了遊戲右鍵選單才會出現 LE 轉換
        public string LocaleEmulatorPath
        {
            get => localeEmulatorPath;
            set => SetValue(ref localeEmulatorPath, value);
        }

        // LE profile GUID（留空用 LE 預設 profile）
        public string LocaleEmulatorProfileGuid
        {
            get => localeEmulatorProfileGuid;
            set => SetValue(ref localeEmulatorProfileGuid, value);
        }

        public GalCompanionConfig Clone()
        {
            var clone = new GalCompanionConfig
            {
                Hotkey = Hotkey,
                ShowBubble = ShowBubble,
                BubbleX = BubbleX,
                BubbleY = BubbleY,
                CaptureMode = CaptureMode,
                ClientAreaOnly = ClientAreaOnly,
                SaveToFile = SaveToFile,
                PlaySound = PlaySound,
                CaptureWithNote = CaptureWithNote,
                BubbleOpacity = BubbleOpacity,
                ScreenshotRoot = ScreenshotRoot,
                TriliumEnabled = TriliumEnabled,
                TriliumUrl = TriliumUrl,
                TriliumToken = TriliumToken,
                TriliumParentNoteId = TriliumParentNoteId,
                TriliumSendScreenshots = TriliumSendScreenshots,
                TriliumDateFormat = TriliumDateFormat,
                TriliumNotePerGame = TriliumNotePerGame,
                TriliumImpressionsTitle = TriliumImpressionsTitle,
                TriliumTranslationTitle = TriliumTranslationTitle,
                SaveSyncEnabled = SaveSyncEnabled,
                PlaytimeSyncEnabled = PlaytimeSyncEnabled,
                RclonePath = RclonePath,
                RcloneRemote = RcloneRemote,
                SaveSyncToleranceSeconds = SaveSyncToleranceSeconds,
                SaveSyncKeepHistory = SaveSyncKeepHistory,
                LocaleEmulatorPath = LocaleEmulatorPath,
                LocaleEmulatorProfileGuid = LocaleEmulatorProfileGuid,
                SaveRules = new Dictionary<string, SaveRule>()
            };

            if (SaveRules != null)
            {
                foreach (var pair in SaveRules)
                {
                    clone.SaveRules[pair.Key] = pair.Value;
                }
            }
            return clone;
        }

        /// <summary>保存前の検証。使っていない機能はエラーにしない。</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(Hotkey))
            {
                try
                {
                    uint modifiers, vk;
                    HotkeyListener.ParseHotkey(Hotkey, out modifiers, out vk);
                }
                catch (Exception ex)
                {
                    errors.Add("熱鍵：" + ex.Message);
                }
            }

            if (CaptureMode != "auto" && CaptureMode != "printwindow" && CaptureMode != "screencrop")
            {
                errors.Add("截圖模式只能是 auto / printwindow / screencrop。");
            }

            if (BubbleOpacity < 0.1 || BubbleOpacity > 1.0)
            {
                errors.Add("氣泡窗透明度要在 0.1 到 1.0 之間。");
            }

            if (TriliumEnabled)
            {
                if (string.IsNullOrWhiteSpace(TriliumUrl))
                {
                    errors.Add("Trilium：要填伺服器網址。");
                }
                else if (!IsHttpUrl(TriliumUrl))
                {
                    errors.Add("Trilium：網址要以 http:// 或 https:// 開頭。");
                }

                if (string.IsNullOrWhiteSpace(TriliumToken))
                {
                    errors.Add("Trilium：要填 ETAPI token。");
                }

                if (string.IsNullOrWhiteSpace(TriliumDateFormat))
                {
                    errors.Add("Trilium：日期格式不能留空。");
                }
            }

            if (SaveSyncEnabled && string.IsNullOrWhiteSpace(RcloneRemote))
            {
                errors.Add("存檔同步：要填 rclone remote。");
            }

            if (SaveSyncToleranceSeconds < 3)
            {
                errors.Add("存檔同步容差不能小於 3 秒（zip 時間戳解析度是 2 秒）。");
            }

            return errors;
        }

        internal static bool IsHttpUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out uri))
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        public static double ClampOpacity(double value)
        {
            if (value < 0.1)
            {
                return 0.1;
            }
            return value > 1.0 ? 1.0 : value;
        }
    }
}
