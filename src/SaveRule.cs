using System.Collections.Generic;

namespace GalCompanion
{
    public class SaveRule
    {
        // 支援 {GameDir}（遊戲安裝目錄）與 %環境變數%，可填檔案或資料夾
        public List<string> Paths { get; set; } = new List<string>();
    }
}
