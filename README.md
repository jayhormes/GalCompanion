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
- **Trilium 直送**（Phase 2）：每款遊戲對應一則 note，截圖自動 append（時間戳＋圖）；氣泡窗 📝 按鈕可補文字記錄（記漢化問題用）

## 安裝

1. 從 GitHub Actions artifact 或 Release 下載 `GalCompanion.pext`
2. 拖進 Playnite 視窗，或雙擊安裝，重啟 Playnite

## 設定

沒有 UI，直接改 JSON（改完重啟 Playnite 生效）：

```
%AppData%\Playnite\ExtensionsData\80cdee03-e216-4df2-b247-a56056f61543\config.json
```

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
| `TriliumParentNoteId` | 空 | 遊戲筆記的父 note id；第一次記錄時自動在其下建遊戲子 note |
| `TriliumSendScreenshots` | `true` | 截圖自動 append；`false` 則只有 📝 手動記錄才送 |
| `TriliumNoteBindings` | `{}` | gameId → noteId 對應，自動維護；想綁到既有 note 可手動填 |

## 開發

- .NET Framework 4.6.2，需在 Windows 建置：`dotnet build src/GalCompanion.csproj -c Release`
- 單元測試：`dotnet test tests/GalCompanion.Tests/GalCompanion.Tests.csproj`（涵蓋熱鍵解析、路徑組合、全黑偵測、設定預設值；Win32 截圖/剪貼簿/氣泡窗要在真機驗證）
- CI（GitHub Actions）每次 push 先跑測試再產出 `GalCompanion.pext` artifact；打 `v*` tag 會發 Release
- `.pext` 就是 zip：`GalCompanion.dll` + `extension.yaml` 打包即可

## Roadmap

- ~~Phase 2：Trilium ETAPI 直送~~（已實作，待真機驗證）
- Phase 3：存檔跨裝置同步（PC ↔ ROG Ally，NAS 中繼，OnGameStarting pull / OnGameStopped push；決策核心 SyncPlanner 已完成並有測試，傳輸層待接 rclone）
- Phase 4：同步防呆（補推、衝突偵測）
- Phase 5：存檔路徑學習模式（監控首次遊玩的檔案寫入自動生成規則）
- Phase 6：掌機體驗（觸控浮動視窗、手把組合鍵）
