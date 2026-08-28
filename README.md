# GalCompanion

Playnite 外掛。給 GalGame 玩家的遊玩伴侶：截圖筆記、（規劃中）存檔跨裝置同步。

## 目前功能（v0.1 — Phase 1 MVP）

- 遊戲啟動時顯示**浮動氣泡窗**（不搶焦點）；左側把手可拖曳，位置會記住，透明度可調（`BubbleOpacity`）
- 📷 **左鍵＝記錄**：送 Trilium（啟用時）＋本地歸檔（`SaveToFile` 開啟時）；兩者都沒開就退回剪貼簿
- 📷 **右鍵＝只進剪貼簿**：不落地、不上傳，臨時貼圖用
- 可選全域熱鍵（預設 `Shift+F12`，等同左鍵；config 留空停用）
- 截圖歸檔路徑相容 ExtraMetadata 慣例：`<Playnite設定目錄>\ExtraMetadata\games\<GameId>\screenshots\`
- 遊戲右鍵選單 → GalCompanion → 打開截圖資料夾
- 成功時播系統提示音；沒有遊戲在跑時截到的圖存到 `ExtraMetadata\screenshots\unassigned\`
- **Trilium 直送**（Phase 2）：📷 截圖寫進當天的「遊戲心得」、📝 文字寫進其下的「翻譯問題」子 note；會自動找當天既有的晨間日記掛上去
- **存檔跨裝置同步**（Phase 3）：啟動前自動判定拉/推/衝突、結束後自動推上 NAS、當機漏推下次啟動補推；衝突一律跳對話框不自動覆蓋；遊戲右鍵選單可手動推/拉

## 安裝

1. 從 GitHub Actions artifact 或 Release 下載 `GalCompanion.pext`
2. 拖進 Playnite 視窗，或雙擊安裝，重啟 Playnite

## 設定

**附加元件 → 擴充功能 → GalCompanion** 裡面設定，存檔後即時生效（熱鍵、Trilium、存檔同步都會用新設定重建，不用重啟）。

氣泡窗跑到螢幕外時（換螢幕或改解析度會發生），設定畫面裡有「把氣泡窗移回畫面中央」。下次顯示時也會自動檢查座標，看不到就自己回中央。

設定存在 `%AppData%\Playnite\ExtensionsData\80cdee03-e216-4df2-b247-a56056f61543\config.json`，原本手寫的內容會直接沿用。`SaveRules` 因為是逐遊戲的路徑表，仍然只能改 JSON。

| 欄位 | 預設 | 說明 |
|---|---|---|
| `Hotkey` | `Shift+F12` | 格式 `修飾鍵+按鍵`，如 `Ctrl+Alt+S`、`F9`。修飾鍵：Ctrl / Alt / Shift / Win。按鍵名用 WPF Key 名稱（數字鍵用 D0-D9）。留空 = 不用熱鍵 |
| `ShowBubble` | `true` | 遊戲進行中顯示截圖氣泡窗 |
| `BubbleX` / `BubbleY` | 空 | 氣泡窗位置，拖曳後自動記錄 |
| `CaptureMode` | `auto` | `auto`：先試 PrintWindow，抓到全黑改抓螢幕。`printwindow` / `screencrop` 強制指定 |
| `ClientAreaOnly` | `true` | 只截遊戲畫面，不含視窗標題列邊框 |
| `SaveToFile` | `false` | 左鍵截圖存本地 PNG。Playnite 本身不會顯示它們，只有搭配 Screenshot Visualizer 之類擴充或想留離線備份才需要開 |
| `BubbleOpacity` | `0.55` | 氣泡窗平時透明度（0.1–1.0），滑鼠移上去恆為不透明 |
| `PlaySound` | `true` | 成功播提示音 |
| `ScreenshotRoot` | 空 | 自訂截圖根目錄；留空用 Playnite 的 ExtraMetadata |
| `TriliumEnabled` | `false` | 開啟 Trilium 直送 |
| `TriliumUrl` | 空 | 例 `http://nas:8080`（需 Trilium ≥ 0.61 的 ETAPI） |
| `TriliumToken` | 空 | Trilium → Options → ETAPI 產生 |
| `TriliumParentNoteId` | 空 | **找不到當天日記時**，在這個 note 底下建日期 note |
| `TriliumDateFormat` | `yyyy.MM.dd` | 用來比對既有日記標題的日期格式 |
| `TriliumImpressionsTitle` | `遊戲心得` | 日期底下的心得 note 標題（📷 寫這裡） |
| `TriliumTranslationTitle` | `翻譯問題` | 心得底下的子議題 note 標題（📝 寫這裡） |
| `TriliumSendScreenshots` | `true` | 截圖自動 append；`false` 則只有 📝 手動記錄才送 |
| `SaveSyncEnabled` | `false` | 開啟存檔同步 |
| `RclonePath` | `rclone` | rclone.exe 路徑；在 PATH 裡就不用改 |
| `RcloneRemote` | 空 | rclone remote＋根目錄，例 `nas:playnite-saves` |
| `SaveSyncToleranceSeconds` | `3` | 時間戳比較容差；zip 時間戳解析度 2 秒，勿低於 3 |
| `SaveSyncKeepHistory` | `true` | NAS 端每次推送另存 `history/*.zip`，誤覆蓋可救 |
| `SaveRules` | `{}` | gameId → 存檔路徑規則，見下 |
| `LocaleEmulatorPath` | 空 | LEProc.exe 完整路徑；填了遊戲右鍵選單才會出現 LE 轉換 |
| `LocaleEmulatorProfileGuid` | 空 | LE profile 的 GUID（走 `-runas`）；留空用 LE 預設 profile |

### 存檔同步設定

1. 兩台機器都裝 [rclone](https://rclone.org/)，`rclone config` 建同名 remote 指向 NAS（WebDAV 或 SFTP）；外出要通就三台都掛 Tailscale
2. 每款遊戲在 `SaveRules` 填存檔位置（Phase 5 學習模式做好前手填）：

```json
"SaveRules": {
  "<Playnite遊戲Id>": {
    "Paths": [ "{GameDir}\\savedata", "%APPDATA%\\某引擎\\某遊戲" ]
  }
}
```

`{GameDir}` = 遊戲安裝目錄；支援 `%環境變數%`；可填資料夾或單一檔案。遊戲 Id 看遊戲詳細頁網址或用「打開截圖資料夾」看路徑。

行為：
- **啟動前**：比對本機存檔 mtime、NAS manifest、上次同步點 → 遠端較新自動拉（本機先備份）、上次漏推先補推、兩邊都動過跳衝突對話框（用 NAS 的／用本機的／取消啟動）
- **結束後**：打包推上 NAS（`latest.zip`＋`manifest.json`＋history），背景執行
- **Playnite 啟動時**：掃全部規則補推漏掉的
- 同步失敗會問你要不要照樣啟動遊戲（存檔可能不是最新）

### 日區啟動（Locale Emulator）

Playnite 內建就有 Locale Emulator 的模擬器定義（LEProc + `"{ImagePath}"`），原生路徑是「管理模擬器」加 LE 後逐款把啟動動作改成模擬器型 — 每款都要手點。GalCompanion 提供批次版：

1. config 填 `LocaleEmulatorPath`（LEProc.exe 位置）；要指定 profile 就填 `LocaleEmulatorProfileGuid`，留空用 LEGUI 設的預設 profile（記得把預設設成日文）
2. 選取一款或多款遊戲 → 右鍵 → GalCompanion → **改用 Locale Emulator 啟動**：原啟動動作降為備用，插入 LEProc 動作當 Play 鍵
3. **還原直接啟動**可整批退回

### 為什麼不用 Ludusavi？

[ludusavi-playnite](https://github.com/mtkennerly/ludusavi-playnite) + [Ludusavi 雲同步](https://github.com/mtkennerly/ludusavi/blob/master/docs/help/cloud-backup.md)是成熟方案，但遊戲匹配靠 PCGamingWiki 資料庫，GalGame 大多不在庫、每款仍要手填 custom entries；衝突判定也只到備份資料夾層級。此外掛的規則本來就要自填（之後由學習模式自動生成），且要逐遊戲的三時間戳衝突判定，故自建。想先求有可先用 Ludusavi 過渡。

## 開發

- .NET Framework 4.6.2，需在 Windows 建置：`dotnet build src/GalCompanion.csproj -c Release`
- 單元測試：`dotnet test tests/GalCompanion.Tests/GalCompanion.Tests.csproj`（涵蓋熱鍵解析、路徑組合、全黑偵測、設定預設值；Win32 截圖/剪貼簿/氣泡窗要在真機驗證）
- CI（GitHub Actions）每次 push 先跑測試再產出 `GalCompanion.pext` artifact；打 `v*` tag 會發 Release
- `.pext` 就是 zip：`GalCompanion.dll` + `extension.yaml` 打包即可

## Roadmap

- ~~Phase 2：Trilium ETAPI 直送~~（已實作，待真機驗證）
- ~~Phase 3：存檔跨裝置同步~~（已實作，待 rclone/NAS 環境與真機驗證）
- ~~Phase 4：同步防呆（補推、衝突偵測）~~（已併入 Phase 3 實作）
- Phase 5：存檔路徑學習模式（監控首次遊玩的檔案寫入自動生成規則）
- Phase 6：掌機體驗（觸控浮動視窗、手把組合鍵）
