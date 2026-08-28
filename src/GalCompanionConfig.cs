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
    }
}
