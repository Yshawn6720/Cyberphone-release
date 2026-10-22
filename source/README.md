# Cyberphone

FFXIV 台服（繁中）用的遊戲內手機插件。本專案是 [Aetherphone](https://github.com/XeldarAlz/FFXIV-Aetherphone)（XeldarAlz，AGPL-3.0-or-later）的**非官方 fork**，與 Aetherphone 及其 Aethernet 服務沒有任何關係。

> 非官方專案，與 SQUARE ENIX 無關。使用第三方插件的風險由使用者自行承擔。
> 有問題請回報到本 repo，**不要**回報給 Aetherphone 原作者。

## 與上游的差異

- **台服環境**：改以台服 Dalamud（API 13／.NET 9）建置。
- **不連官方 Aethernet**：所有連線都經過 `src/Aetherphone/Core/Net/OfficialHostGuard.cs`，官方網域在 DNS 解析前就被擋下；需要登入 Aethernet 的社群類 app（Chirper、Aethergram、Velvet、ChocoChat 等）與 Strats 都已隱藏。`src/Aetherphone.Tests/OfflineGuaranteeTests.cs` 以測試守住這件事。
- **看片派對、Jam、一起玩、公告與行事曆活動走自架伺服器**：MogCast 的看片派對、Music 的 Jam（一起聽）與遊戲的「一起玩」改用自己架的轉送伺服器（[`server/`](server)，Cloudflare Worker＋Durable Object，或用 Docker 跑在自己的機器）。主持人在 MogCast 設定貼上連線碼即可開派對、發起 Jam 或開遊戲房；朋友把主持人複製的邀請貼到「一起看」、Jam 頁或「一起玩」就能加入，不用另外設定。看片派對支援審核加入、佇列建議、表情反應與移出成員；附近派對不提供。「一起玩」支援 Uno、國際象棋、四子棋與舷炮齊射，規則由伺服器裁定；8球檯球、莫古利大戰、迷你高爾夫、幸運抽牌、公開房間列表、配對與排行榜不提供。設定好伺服器後，「公告」app 與行事曆的遊戲內活動也改從同一台伺服器讀取（內容是伺服器上的 JSON，插件快取 6 小時）；沒設定時公告維持隱藏。只有開派對、發起或加入、開或加入遊戲房，或打開公告、行事曆且快取過期時才連線，沒設定就維持離線。同一個網路同時開的同類房間有上限（伺服器的 `MAX_ROOMS_PER_ADDRESS`，預設 3 間），超過時插件顯示「你的網路已開太多房間」與上限，請先關掉一間再開，不會自動重試。架設與內容維護方式見 [`docs/self-hosting-relay.md`](docs/self-hosting-relay.md)。
- **語音通話只走自架 Docker 伺服器**：Linkpearl 多了「通話」分頁，按「開通話房」後複製邀請給朋友，朋友貼到同一頁就能加入，每房最多 4 人。開房的人要先在通話頁按「同意」，新加入的人才會拿到位子、聽到與送出聲音，按「拒絕」對方會看到「房主沒有同意你加入」；開房的人不在通話裡時，新來的人會直接被擋下並看到「房主不在通話中」，之前被同意過的人斷線重連不用再等。舊版插件開的通話房維持不用同意。只做邀請制，不能直接撥號、沒有來電響鈴與在線名單；控制中心的通話開關、Spotlight 與 `call` 指令維持隱藏。伺服器以 `CALLS_ENABLED` 控制，部署到 Cloudflare Workers 時關閉（語音幀會很快吃光免費額度），只有用 Docker 跑在自己機器（NAS）時開啟；伺服器沒開時插件顯示「無法使用通話」且不重試。沒開通話房就不連線，離開通話立刻斷線。頻寬需求見 [`docs/self-hosting-relay.md`](docs/self-hosting-relay.md)。
- **世界螢幕跟著插件 UI 隱藏**：MogCast 的世界螢幕改在 Dalamud 的 UI 繪製時機畫出，不再 hook DirectX 的 Present，在插件列表把 Cyberphone 關掉再打開時，遊戲的繪製流程裡不會殘留插件的 hook。代價是 Dalamud 隱藏插件視窗時，世界螢幕也會一起隱藏：隱藏遊戲 UI 時、集體動作（GPose）與過場動畫中都會（後兩者依手機設定「集體動作時顯示」「過場動畫時顯示」決定）。
- **遊戲內滿版看片**：MogCast 播放器的「全螢幕」會覆蓋整個遊戲畫面，隨視窗尺寸自動調整；「完整顯示」保留整部影片並依比例留黑邊，「填滿裁切」維持比例填滿畫面、裁掉超出的部分，不拉伸。控制列有播放/暫停、進度條與音量，移動滑鼠時顯示，閒置約 2.5 秒後整列自動隱藏；音量與手機播放器共用同一個設定，派對觀眾也能調自己的音量。滿版期間手機暫時不畫，退出後自動恢復，不會在畫面上留下手機邊框。按 Esc 或「退出全螢幕」返回，Esc 不需先顯示控制列，亦保留 Dalamud 原生關窗熱鍵處理；空白鍵切換播放/暫停。滿版只影響自己的觀看方式，不改世界螢幕位置或其他成員的觀看模式；派對觀眾的播放/暫停與跳轉仍依主持人授權。
- **世界螢幕一鍵大銀幕**：MogCast 螢幕位置頁的「大銀幕」會依目前鏡頭方向，把世界螢幕放到鏡頭正前方、放大到約占畫面 85%，並改成平面、正對鏡頭。螢幕仍固定在世界中，前方的人物與場景照樣會擋住它，不會跟著鏡頭移動；之後可繼續用移動、微調與尺寸調整，位置也會跟其他擺放一樣被記住。派對觀眾按下後會關閉「跟隨主持人的螢幕」，改用自己的位置。
- **品牌**：顯示名稱、指令（`/cyberphone`、`/cphone`）與 app 圖示都換成 Cyberphone 自己的；圖示由 `tools/icon-generator` 從 Phosphor Icons（MIT）重新產生。
- **素材**：上游中需另行授權的素材已移除，清單見 [`NOTICE`](NOTICE)。

完整修改紀錄見 [`NOTICE`](NOTICE) 的修改聲明；上游原始 README 保留在 [`docs/upstream/UPSTREAM-README.md`](docs/upstream/UPSTREAM-README.md)。

## 暫時狀態

插件檔名（`Cyberphone.dll`）與 Dalamud `InternalName` 已是 Cyberphone；assembly 名稱與程式命名空間暫時仍是 `Aetherphone`：ImageSharp 的授權金鑰綁定 assembly 名稱，等本專案取得自己的 SixLabors 金鑰後再改。

## 建置

需要 .NET 9 SDK 與台服 Dalamud（預設讀 `%APPDATA%\FFXIVSimpleLauncher\Dalamud\Injector`，可用 `DALAMUD_HOME` 覆寫）：

```bash
dotnet build src/Aetherphone/Aetherphone.csproj -c Release
dotnet test src/Aetherphone.Tests -c Release
```

## 授權

**AGPL-3.0-or-later**，條文見 [`LICENSE.md`](LICENSE.md)；第三方元件見 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)。對應原始碼＝本 repo 中與發佈版本號相符的 commit。Aetherphone／Aethernet 名稱與上游圖示不在授權範圍內（見 [`TRADEMARK.md`](TRADEMARK.md)），本專案不使用。
