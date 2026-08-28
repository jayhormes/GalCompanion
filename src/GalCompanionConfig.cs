using System.Collections.Generic;

namespace GalCompanion
{
    public class GalCompanionConfig
    {
        // 留空 = 不註冊熱鍵，只用氣泡窗
        public string Hotkey { get; set; } = "Shift+F12";

        public bool ShowBubble { get; set; } = true;

        public double? BubbleX { get; set; }

        public double? BubbleY { get; set; }

        // auto | printwindow | screencrop
        public string CaptureMode { get; set; } = "auto";

        public bool ClientAreaOnly { get; set; } = true;

        // 本地 PNG 歸檔只有搭配 Screenshot Visualizer 之類擴充或離線備份才有用，預設關
        public bool SaveToFile { get; set; } = false;

        public bool PlaySound { get; set; } = true;

        // 氣泡窗平時透明度（0.1–1.0），滑鼠移上去恆為 1.0
        public double BubbleOpacity { get; set; } = 0.55;

        // 留空 = <Playnite 設定目錄>\ExtraMetadata
        public string ScreenshotRoot { get; set; } = string.Empty;

        public bool TriliumEnabled { get; set; } = false;

        // 例 http://nas:8080；token 在 Trilium Options → ETAPI 產生
        public string TriliumUrl { get; set; } = string.Empty;

        public string TriliumToken { get; set; } = string.Empty;

        // 遊戲筆記的父 note；第一次記錄時在其下自動建該遊戲的子 note
        public string TriliumParentNoteId { get; set; } = string.Empty;

        // 截圖是否自動 append 到 Trilium（false = 只有 📝 手動記錄才送）
        public bool TriliumSendScreenshots { get; set; } = true;

        // 日期節點的標題格式。用來比對既有晨間日記（例「2026.08.28 星期五 (Week35) - 晨間日記」）
        public string TriliumDateFormat { get; set; } = "yyyy.MM.dd";

        // 日期底下的心得 note 標題（📷 截圖寫這裡）
        public string TriliumImpressionsTitle { get; set; } = "遊戲心得";

        // 心得底下的子議題 note 標題（📝 文字寫這裡）
        public string TriliumTranslationTitle { get; set; } = "翻譯問題";

        public bool SaveSyncEnabled { get; set; } = false;

        // rclone.exe 路徑；在 PATH 裡就留預設
        public string RclonePath { get; set; } = "rclone";

        // rclone remote 加根目錄，例 "nas:playnite-saves"
        public string RcloneRemote { get; set; } = string.Empty;

        // zip 時間戳解析度 2 秒，容差必須 ≥ 3
        public int SaveSyncToleranceSeconds { get; set; } = 3;

        // NAS 端每次推送另存 history/*.zip
        public bool SaveSyncKeepHistory { get; set; } = true;

        // gameId → 存檔路徑規則
        public Dictionary<string, SaveRule> SaveRules { get; set; } = new Dictionary<string, SaveRule>();

        // LEProc.exe 完整路徑；填了遊戲右鍵選單才會出現 LE 轉換
        public string LocaleEmulatorPath { get; set; } = string.Empty;

        // LE profile GUID（留空用 LE 預設 profile）
        public string LocaleEmulatorProfileGuid { get; set; } = string.Empty;

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
