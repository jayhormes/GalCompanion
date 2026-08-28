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

        public bool CopyToClipboard { get; set; } = true;

        public bool SaveToFile { get; set; } = true;

        public bool PlaySound { get; set; } = true;

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

        // gameId → noteId，自動維護
        public Dictionary<string, string> TriliumNoteBindings { get; set; } = new Dictionary<string, string>();
    }
}
