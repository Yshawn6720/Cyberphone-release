# Cyberphone

FFXIV 台服（繁中）用的遊戲內手機插件。本專案是 [Aetherphone](https://github.com/XeldarAlz/FFXIV-Aetherphone)（XeldarAlz，AGPL-3.0-or-later）的**非官方 fork**，與 Aetherphone 及其 Aethernet 服務沒有任何關係。

> 非官方專案，與 SQUARE ENIX 無關。使用第三方插件的風險由使用者自行承擔。
> 有問題請回報到本 repo，**不要**回報給 Aetherphone 原作者。

## 與上游的差異

- **台服環境**：改以台服 Dalamud（API 13／.NET 9）建置。
- **不連官方 Aethernet**：所有連線都經過 `src/Aetherphone/Core/Net/OfficialHostGuard.cs`，官方網域在 DNS 解析前就被擋下；需要登入 Aethernet 的社群類 app（Chirper、Aethergram、Velvet、ChocoChat 等）與 Strats 都已隱藏。`src/Aetherphone.Tests/OfflineGuaranteeTests.cs` 以測試守住這件事。
- **看片派對、Jam、一起玩、公告與行事曆活動走自架伺服器**：MogCast 的看片派對、Music 的 Jam（一起聽）與遊戲的「一起玩」改用自己架的轉送伺服器（[`server/`](server)，Cloudflare Worker＋Durable Object，或用 Docker 跑在自己的機器）。主持人在 MogCast 設定貼上連線碼即可開派對、發起 Jam 或開遊戲房；朋友把主持人複製的邀請貼到「一起看」、Jam 頁或「一起玩」就能加入，不用另外設定。看片派對與 Jam 都支援審核加入、表情反應與移出成員（被移出的人不會自動重連），看片派對另有佇列建議，Jam 的主持人可以設定來賓能不能加歌、能不能控制播放；附近的派對與 Jam 見下一條。「一起玩」支援 Uno、國際象棋、四子棋與舷炮齊射，規則由伺服器裁定；8球檯球、莫古利大戰、迷你高爾夫、幸運抽牌、公開房間列表、配對與排行榜不提供。設定好伺服器後，「公告」app 與行事曆的遊戲內活動也改從同一台伺服器讀取（內容是伺服器上的 JSON，插件快取 6 小時）；沒設定時公告維持隱藏。只有開派對、發起或加入、開或加入遊戲房，打開附近列表或按重新整理，或打開公告、行事曆且快取過期時才連線，沒設定就維持離線。同一個網路同時開的同類房間有上限（伺服器的 `MAX_ROOMS_PER_ADDRESS`，預設 3 間），超過時插件顯示「你的網路已開太多房間」與上限，請先關掉一間再開，不會自動重試。架設與內容維護方式見 [`docs/self-hosting-relay.md`](docs/self-hosting-relay.md)。
- **附近的派對與 Jam**：主持人在派對或 Jam 的設定裡開啟讓附近玩家看到（每個房間預設關閉）後，伺服器才會把這間房列給同一區域、同一伺服器的玩家；列表只顯示主持人的角色名稱與人數（Jam 另有標題與正在播的歌），不會給出房間的邀請碼。在「一起看」或 Jam 頁打開附近列表時查詢一次，之後只有按重新整理才會再查，同一個網路每 15 秒最多一次，不會在背景輪詢。點列表裡的房間會直接送出加入請求，不用貼邀請；不論主持人有沒有開審核加入，都要等主持人同意才會進房，同意後才拿到真正的邀請碼。派對觀眾播放失敗時，只有主持人會看到是誰、哪個連結播不了。舊版插件開的派對不會出現在附近列表。
- **看片派對彈幕**：伺服器支援時，MogCast 播放頁的表情列下方多一個彈幕輸入框，一則最多 60 字並顯示剩餘字數，按 Enter 送出；同一派對的人會看到彈幕在影片上緣由右往左捲過，世界螢幕、全螢幕視窗與手機上的影片都會畫，自己送的加外框。彈幕只從這個輸入框送出，不轉發遊戲聊天；伺服器只轉送不保存，中途加入的人看不到加入前的彈幕。每人一次最多連送 3 則，之後每 1.75 秒 1 則。螢幕頁的「顯示彈幕」可關掉自己這台的彈幕並調整行數（1 到 5 行，預設 3 行），和聊天氣泡各自開關；成員選單的「隱藏此人的彈幕」只在這場派對有效。主持人可在派對設定關閉「允許彈幕」，伺服器就不再轉送，成員的輸入框停用並顯示「主持人已關閉彈幕」。伺服器還是舊版時不顯示輸入框；舊版插件照常看片，只是看不到彈幕。
- **台服新聞與精選電台走自架伺服器，台服不查 Lodestone**：Lodestone 沒有台服的新聞與角色，以前台服的新聞 app 拿到的是北美 Lodestone 的新聞。現在設定好伺服器後，台服的新聞 app 改讀伺服器上的 `server/content/news-tw.json`，由 `server/scripts/fetch-tw-news.mjs` 從 [FF14 繁體中文版官網](https://www.ffxiv.com.tw/web/news/news_list.aspx)的消息列表整理而來（只留標題、日期、分類、連結與 120 字內的摘要，不存全文與圖片），分頭條新聞、公告、維護、更新四類，沒有「狀態」分頁，文章頁的按鈕改成「在官方網站閱讀」；沒設定伺服器時只顯示「新聞需要聚會伺服器」，不會退回北美新聞。Music 的電台頁多了「精選電台」，清單是伺服器上的 `server/content/radio.json`（名稱、串流網址、類型、官網），取代上游要登入 Aethernet 的社群電台；沒設定伺服器時整段不出現。兩者都只在打開新聞頁或電台頁、且本機快取超過 6 小時時才連線，主畫面的新聞小工具只讀快取不連線。伺服器還是沒有台服新聞、精選電台或附近列表的舊版時，這三處會顯示「你的聚會伺服器還不支援…，請伺服器管理者更新」，不會誤報成連不上。台服角色的 Lodestone 頭像與查詢比照國服停用，不再一直載入。更新新聞與編輯電台清單的方式見 [`docs/self-hosting-relay.md`](docs/self-hosting-relay.md)。
- **語音通話只走自架 Docker 伺服器**：Linkpearl 多了「通話」分頁，按「開通話房」後複製邀請給朋友，朋友貼到同一頁就能加入，每房最多 4 人。開房的人要先在通話頁按「同意」，新加入的人才會拿到位子、聽到與送出聲音，按「拒絕」對方會看到「房主沒有同意你加入」；開房的人不在通話裡時，新來的人會直接被擋下並看到「房主不在通話中」，之前被同意過的人斷線重連不用再等。舊版插件開的通話房維持不用同意。只做邀請制，不能直接撥號、沒有來電響鈴與在線名單；控制中心的通話開關、Spotlight 與 `call` 指令維持隱藏。伺服器以 `CALLS_ENABLED` 控制，部署到 Cloudflare Workers 時關閉（語音幀會很快吃光免費額度），只有用 Docker 跑在自己機器（NAS）時開啟；伺服器沒開時插件顯示「無法使用通話」且不重試。沒開通話房就不連線，離開通話立刻斷線。頻寬需求見 [`docs/self-hosting-relay.md`](docs/self-hosting-relay.md)。
- **世界螢幕跟著插件 UI 隱藏**：MogCast 的世界螢幕改在 Dalamud 的 UI 繪製時機畫出，不再 hook DirectX 的 Present，在插件列表把 Cyberphone 關掉再打開時，遊戲的繪製流程裡不會殘留插件的 hook。代價是 Dalamud 隱藏插件視窗時，世界螢幕也會一起隱藏：隱藏遊戲 UI 時、集體動作（GPose）與過場動畫中都會（後兩者依手機設定「集體動作時顯示」「過場動畫時顯示」決定）。
- **遊戲內滿版看片**：MogCast 播放器的「全螢幕」會覆蓋整個遊戲畫面，隨視窗尺寸自動調整；「完整顯示」保留整部影片並依比例留黑邊，「填滿裁切」維持比例填滿畫面、裁掉超出的部分，不拉伸。控制列有播放/暫停、進度條與音量，移動滑鼠時顯示，閒置約 2.5 秒後整列自動隱藏；音量與手機播放器共用同一個設定，派對觀眾也能調自己的音量。滿版期間手機暫時不畫，退出後自動恢復，不會在畫面上留下手機邊框。按 Esc 或「退出全螢幕」返回，Esc 不需先顯示控制列，亦保留 Dalamud 原生關窗熱鍵處理；空白鍵切換播放/暫停。滿版只影響自己的觀看方式，不改世界螢幕位置或其他成員的觀看模式；派對觀眾的播放/暫停與跳轉仍依主持人授權。
- **世界螢幕一鍵大銀幕**：MogCast 螢幕位置頁的「大銀幕」會依目前鏡頭方向，把世界螢幕放到鏡頭正前方、放大到約占畫面 85%，並改成平面、正對鏡頭。螢幕仍固定在世界中，前方的人物與場景照樣會擋住它，不會跟著鏡頭移動；之後可繼續用移動、微調與尺寸調整，位置也會跟其他擺放一樣被記住。派對觀眾按下後會關閉「跟隨主持人的螢幕」，改用自己的位置。
- **品牌**：顯示名稱、指令（`/cyberphone`、`/cphone`）與 app 圖示都換成 Cyberphone 自己的；圖示由 `tools/icon-generator` 從 Phosphor Icons（MIT）重新產生。
- **素材**：上游中需另行授權的素材已移除，清單見 [`NOTICE`](NOTICE)。

完整修改紀錄見 [`NOTICE`](NOTICE) 的修改聲明；上游原始 README 保留在 [`docs/upstream/UPSTREAM-README.md`](docs/upstream/UPSTREAM-README.md)。

## 建置

需要 .NET 9 SDK 與台服 Dalamud（預設讀 `%APPDATA%\FFXIVSimpleLauncher\Dalamud\Injector`，可用 `DALAMUD_HOME` 覆寫）：

```bash
dotnet build src/Aetherphone/Aetherphone.csproj -c Release
dotnet test src/Aetherphone.Tests -c Release
```

## 授權

**AGPL-3.0-or-later**，條文見 [`LICENSE.md`](LICENSE.md)；第三方元件見 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)。對應原始碼＝本 repo 中與發佈版本號相符的 commit。Aetherphone／Aethernet 名稱與上游圖示不在授權範圍內（見 [`TRADEMARK.md`](TRADEMARK.md)），本專案不使用。
