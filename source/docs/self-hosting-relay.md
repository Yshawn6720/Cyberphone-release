# 自架派對轉送伺服器

Cyberphone 不連官方 Aethernet。MogCast 的看片派對、Music 的 Jam（一起聽）、遊戲的「一起玩」與 Linkpearl 的語音通話改用使用者自己架的轉送伺服器：程式碼在 [`server/`](../server)，是一個 Cloudflare Worker 加上每個房間槽位一個 Durable Object（Hibernation）；看片派對（`PartyRoom`）、Jam（`JamRoom`）、遊戲房（`GameRoom`）和通話房（`CallRoom`）各有一組槽位，數量分別由 `MAX_ROOMS_PARTY`、`MAX_ROOMS_JAM`、`MAX_ROOMS_GAME`、`MAX_ROOMS_CALL` 決定，沒設的房型用 `MAX_ROOMS`。語音通話只在自架 Docker（NAS）上開啟，部署到 Workers 時關閉（見下方「語音通話」）。同一台伺服器也提供「公告」app 與行事曆「遊戲內活動」的內容，這兩份是隨伺服器部署的靜態 JSON，由 Worker 直接回應，不經 Durable Object。依 AGPL-3.0-or-later，任何人都可以自己架一台。

插件只有在要開派對、加入派對、開或加入遊戲房、開或加入通話房、打開派對或 Jam 的附近列表（或在列表上按重新整理）、打開「公告」，或本次啟動後第一次顯示行事曆（含主畫面的行事曆小工具），且手上的內容已超過 6 小時時才連線；沒設定伺服器就完全不連線，「公告」也不會出現在手機上。兩種字串：

- **連線碼**：`<網址>#<邀請碼>`，例如 `party.example.com#K7M2Q9XR4T`（網址省略 `https://`）。填在 MogCast 設定 →「聚會伺服器」，輸入當下就存；想自己開派對的人才需要。邀請碼若含英數字與 `-._~` 以外的字元（例如 `#`、空白），連線碼裡要用 percent-encoding（`#` 寫成 `%23`）；插件產生的連線碼與派對邀請會自動編碼。
- **派對邀請**：`<網址>#<邀請碼>#<房號>`。主持人在派對裡按「複製邀請碼」就會複製這一串。朋友貼到「一起看」分頁的加入欄，插件會自動存好伺服器並加入，不用先改設定；之後再加入只要輸入 6 碼房號。

## 支援範圍

- **看片派對**：建房、用房號加入、主持人狀態同步、兩路影片（A／B）、觀眾播放控制與切換影片（由主持人決定是否允許）、離開、換主持人、審核加入、佇列建議、表情反應、彈幕、移出成員、附近派對、播放失敗回報。找朋友的派對還沒做。
  - 審核加入：主持人在派對設定開啟後，新來的觀眾先待審，主持人核准才進房，拒絕就收到「被拒」並斷線。核准過的人斷線重連不必再審（每房記最近 8 人）；換主持人時，待審的請求交給新主持人。
  - 佇列建議：觀眾送出網址（只收 http／https，最長 2048 字元），主持人採納或略過，結果只回給提出的人。主持人允許來賓加入佇列時，插件會直接採納。
  - 表情反應：6 種，轉給房內其他人。插件每人一次最多連送 4 個，之後每秒 1 個；伺服器另有獨立的反應額度（見下表），超過的丟棄，不會吃掉播放控制的額度。
  - 彈幕：成員送 `stream.danmaku`（`text`，可帶 `screen`），伺服器清掉控制字元、格式字元（零寬字元、bidi 標記、BOM）、孤立的 surrogate 與私用區字元，去掉前後空白，以 code point 截到 60 字，清完是空的就丟棄；再以 `stream.danmakuLine`（`text`、`userId`，有帶 `screen` 時一併附上）轉給房內其他已加入的人，不回給發送者。`userId` 由伺服器依連線蓋上，自己填的會被忽略。`screen` 只收 `a` 或 `b`，表示發送者正在看哪一路影片，收到的插件把彈幕畫在那一路的螢幕上，其他值不轉。主持人的 `stream.state` 可帶 `danmakuEnabled`（沒帶時維持原值，新房預設 `true`），伺服器存在房間狀態裡，關閉時直接丟棄彈幕，並在 `stream.state`、`stream.joined`、`stream.hostChanged` 帶上目前的值。伺服器不保存內容，中途加入的人只看得到加入之後的彈幕。`features` 的位元 2 表示支援彈幕，插件沒看到這個位元就不顯示輸入框、也不送出。彈幕有自己的額度（見下表），不吃播放控制與表情的額度。
  - 兩路影片：主持人的 `stream.state` 除了 A 路的 `url`、`positionSeconds`、`paused`、`screenX`～`screenCurve`，還可帶 B 路的 `urlB`、`positionSecondsB`、`pausedB`、`screenXB`、`screenYB`、`screenZB`、`screenYawB`、`screenPitchB`、`screenRollB`、`screenScaleB`、`screenCurveB`，以及主持人的預設播放方式 `playMode`（只收 `a`、`b`、`both`）。伺服器照白名單逐欄轉發，型別不對或 `playMode` 不認得的欄位直接丟掉，`stream.joined` 與 `stream.hostChanged` 也一起帶上；伺服器不保存這些內容。觀眾的 `stream.control` 與 `stream.playbackFailed` 可帶 `channel`（只收 `a` 或 `b`），伺服器原樣附在給主持人的 `stream.controlRequest`、`stream.viewerFailed` 上，其他值不轉，沒帶時主持人當作 A 路。`features` 的位元 4 表示支援兩路影片（現在送 `15`，位元 8 見下一項）；插件要同時看到位元 1 與位元 4 才開放 B 路，舊伺服器沒有這個位元時 B 路停用。
  - 成員切換影片：主持人可以讓全部成員或個別成員直接換掉 A 或 B 正在播的影片。權限位元是 1 加入佇列、2 控制播放、8 切換影片（4 是伺服器加的「可當主持人」，不能授出）。伺服器把主持人 `stream.state` 的 `guestPermissions`（全部成員）存進房間狀態，沒帶時保留原值，連同 `members` 的個人權限（`flags`）只收 1、2、8 三位。觀眾送 `stream.control`（`action: "load"`、`url`，可帶 `channel`），伺服器先確認這個人有位元 8（全部成員或自己的個人權限）且網址是 http／https、最長 2048 字元，才把含 `url` 的 `stream.controlRequest` 轉給主持人，否則直接丟掉；主持人的插件會再驗一次，播放清單網址一律不收（指向單支影片的除外）。主持人不在（寬限期間）時一樣丟掉。換主持人時伺服器保留全部成員與個人權限，新主持人接手後沿用；成員離開或斷線時，伺服器照舊忘掉他的個人權限，重新加入要主持人再勾（主持人寬限期間離開的不經過這一步，個人權限會留著）。`features` 的位元 8 表示支援切換影片，插件沒看到這個位元就不顯示入口，主持人也不授出位元 8。
  - 移出成員：只有主持人能移出，伺服器通知對方後關閉連線（close code 4003），插件不會自動重連回同一房。身分未驗證，移出不是封鎖：同名同服可以再用房號加入；審核開啟時要重新被核准。
  - 播放失敗回報：觀眾播不了主持人的影片時，插件送 `stream.playbackFailed`（`url` 最長 2048 字元、`reason`，兩路影片時帶 `channel`），伺服器只轉給主持人，成為 `stream.viewerFailed`（`hostId`、`userId`、`url`、`reason` 截到 200 字、`from`，有效的 `channel` 一併附上），房內其他人收不到。
  - 主持人斷線：主持人的連線不是因為送出 `stream.leave` 而關閉時（網路斷線、遊戲當掉、超過額度被斷線），伺服器把房間保留 60 秒，房內已加入的觀眾收到 `stream.hostAway`（`hostId`、`graceSeconds`），插件顯示主持人重連中與倒數，影片照常播放。主持人的插件已經拿到房號時，會用原房號（`?code=<房號>`，同一個角色）自動重連，最多 6 次（重試間隔加起來約 23 到 72 秒，涵蓋 60 秒寬限）；伺服器認出是原主持人就接回同一房（房號、成員、審核與彈幕設定不變），把目前的成員名單與寬限期間送來的加入請求交給主持人，觀眾收到 `stream.hostBack`（`hostId`），播放位置由主持人重連後的 `stream.state` 接續。60 秒內沒接回才結束房間（`stream.ended`，`reason: host_left`）；主持人按離開仍立即結束。寬限期間觀眾照樣可以離開或用房號、附近代碼加入（主持人的座位照樣保留，人數上限不變），加入的人等主持人回來、下一次 `stream.state` 才進房；房間暫時不出現在附近列表；同一個角色改開新房會直接結束等待中的舊房。舊插件不認得 `stream.hostAway`／`stream.hostBack` 會略過，只是看不到提示；舊伺服器沒有寬限，主持人重連會被拒，插件顯示聚會不可用，跟以前一樣主持人斷線即結束。
  - 附近派對：見下方「附近的派對與 Jam」。
- **Jam**：建立、用 Jam 邀請或 6 碼代碼加入、主持人播放同步、共用佇列（加歌、移除、排序、播到下一首）、來賓播放控制、換主持人、審核加入、來賓權限、表情反應、移出成員、附近的 Jam。Jam 裡按「分享」會複製 `<網址>#<邀請碼>#<代碼>`，朋友貼到 Jam 頁的「貼上」即可加入，伺服器設定與看片派對共用。聊天與邀請好友還沒做；主持人離開即結束 Jam，不會自動交棒。
  - Jam 設定：主持人送 `jam.settings`（`guestPermissions`、`approvalRequired`、`title`、`discoverable`，都可省略），伺服器存進 Jam 的 storage，並把含這些欄位的 `jam.roster` 發給房內每個人。新 Jam 的預設是來賓可加歌也可控制播放（`guestPermissions` 3：1 加歌、2 控制播放與排序）、不必審核、不公開到附近。伺服器照權限擋下來賓的加歌、播放控制與排序；移除只能移自己加的歌，主持人不受限。
  - 審核加入：開啟後新來的來賓先收到 `jam.joinPending`，主持人收到 `jam.joinRequest`（`userId`、`from`），送 `jam.approve` 或 `jam.deny`（帶 `userId`）。拒絕時對方收到 `jam.declined`（`reason: denied`）並斷線；待審的人自己離開時主持人收到 `jam.joinCancelled`。核准過的人斷線重連不必再審（每個 Jam 記最近 8 人），換主持人時待審的請求交給新主持人；關掉審核時，待審的人（從附近列表來的除外）直接進房。
  - 表情反應：`jam.react`（`reaction` 0 到 7），轉給房內其他人成為 `jam.reaction`（`reaction`、`userId`），用跟看片派對一樣的獨立反應額度。
  - 移出成員：主持人送 `jam.kick`（`userId`），對方收到 `jam.kicked` 後以 close code 4003 斷線，插件不會自動重連；伺服器同時忘掉這個人的核准紀錄，審核開啟時再回來要重新被核准。身分未驗證，移出不是封鎖。
- **一起玩（遊戲房）**：支援 Uno、國際象棋、四子棋、舷炮齊射（Broadside）。主持人在遊戲 app 的「一起玩」分頁選遊戲開房，房間卡上的「複製」會複製 `<網址>#<邀請碼>#<房號>`；朋友貼到「一起玩」的加入欄即可加入，伺服器設定與看片派對共用（沒設定時貼上邀請會自動存好）。規則由伺服器裁定：插件只送「想做的動作」，伺服器檢查輪次與合法性後把新盤面發給房內每個人，Uno 手牌與舷炮齊射的船位只發給本人。
  - 誰能做什麼：只有主持人能開局（大廳至少 2 人）與移出成員；輪到的人才能出牌、落子或開火，否則回「還沒輪到你」；每個動作帶著動作序號，序號過時的回「牌桌已經繼續了」，不會重複套用。一局進行中不收新玩家（回「這個房間正在進行一局」），斷線的玩家保留座位，重連後回到同一局。
  - 計時：Uno、四子棋、舷炮齊射每手 30 秒（舷炮齊射擺船 90 秒），國際象棋每方 10 分鐘、每走一步加 5 秒。伺服器不排程計時器：時間到之後，房內的玩家端每 2 秒最多送一次「時間到」請求，伺服器確認真的超時才判定：Uno 代抽（有疊加就抽整疊）並換人，四子棋與國際象棋判超時方輸，舷炮齊射擺船逾時自動擺好、開火逾時換人，連續逾時 3 次判輸。
  - 離開：玩家在大廳離開就讓出座位；在局中按離開算棄權，由其他人繼續或直接判勝。主持人離開或斷線即結束房間，不會交棒，房內其他人會看到「這個房間已關閉」。
  - 沒做：8球檯球、莫古利大戰、迷你高爾夫、幸運抽牌（需要伺服器物理模擬或另一套規則）、公開房間列表、配對、排行榜與分數上傳；這些入口在轉送模式下都藏起來。
- **語音通話（只在自架 Docker）**：Linkpearl 的「通話」分頁。按「開通話房」取得房號並自動進房，按「複製邀請」複製 `<網址>#<邀請碼>#<房號>`；朋友貼到同一頁的加入欄按「加入」，伺服器設定與看片派對共用（沒設定時貼上邀請會自動存好）。每房最多 4 人，任何人都能隨時離開，最後一人離開房間就消失。
  - 只做邀請制：不能直接撥給聯絡人、沒有來電響鈴、沒有在線名單；這些需要每個插件常駐連線，轉送模式下都藏起來。控制中心的通話開關、Spotlight 與 `call` 指令也維持隱藏。身分仍是自稱的「角色名@伺服器」，不驗證。
  - 沒開通話房時 0 連線；按「離開通話」立刻關連線、不重連。網路斷線時插件會用房號重連（最多 3 次），20 秒內沒接回就結束通話。獨自在房裡 30 分鐘沒人加入，插件會自動離開。
  - 伺服器沒開通話時，`?kind=call` 一律以「拒絕」回覆（見「認證與連線」，close code 4403），插件只試一次就顯示「無法使用通話」，不重試。
  - 伺服器只轉發語音：不解碼、不寫 storage、不排程 alarm，只檢查每幀大小與標頭、把送出者的座位號寫進標頭（不能冒充別人說話），再轉給同房其他人。
- **公告**：設定好連線碼後「公告」app 會出現，打開時讀取伺服器的公告清單。沒有推播通知，也不會在背景定時檢查；未讀標記只在打開 app 後更新。
- **行事曆遊戲內活動**：設定好連線碼後，行事曆的「遊戲內活動」分組與月曆標記改用伺服器的活動清單。

插件在使用轉送伺服器時會把還沒做的入口藏起來；其他社群功能一律不提供。

### 附近的派對與 Jam

- 主持人自己決定要不要讓附近的人看到，每個房間預設關閉：看片派對由主持人的 `stream.state` 同時帶 `discoverable: true` 與 `nearbyOptIn: true`（以及 `territoryId`、`worldId`）開啟，兩者缺一或帶 `false` 就是關閉；1.3.4.0 以前的插件在自架伺服器上沒有這個開關卻預設送 `discoverable: true`，伺服器只認新插件才會送的 `nearbyOptIn`，所以舊插件開的派對一律不會被列出。Jam 由 `jam.settings` 的 `discoverable` 開關，主持人另外在開啟期間透過 Jam 連線送 `jam.nearby`（`territoryId`、`worldId`），位置只放在主持人的連線 attachment，不寫 storage。
- 開啟時伺服器替房間產生一組「附近代碼」，跟房號指向同一個槽位但不是房號；關掉後這組代碼立刻失效。附近列表只給附近代碼，不給房號。
- 查詢：`GET /rooms/nearby?kind=party|jam&territory=<區域 ID>&world=<伺服器 ID>`，帶 `Authorization: Bearer <邀請碼>`（錯誤回 401，`kind` 不是 party／jam 或缺少區域、伺服器回 400）。Worker 交給該房型的 `SlotRegistry`，它問過每個槽位後只回開啟附近、且主持人在同一區域同一伺服器的房間，最多 20 間。看片派對回 `{"type":"stream.nearby.roster","nearbyStreams":[{"hostId":"","name","world":"","displayName","handle":"","avatarUrl":null,"code","memberCount"}]}`；Jam 回 `{"type":"jam.nearby.roster","nearbyJams":[{"jamId","code","title","hostId":"","hostDisplayName","hostHandle":"","hostAvatarUrl":null,"memberCount","track"}]}`。只透露主持人的角色名稱（不含伺服器與角色 ID）、人數，Jam 另有標題與正在播的歌。
- 限速：每個來源 IP、每種房型一個額度，容量 4，每 15 秒補 1；超過回 429 `{"error":"rate_limited","retryAfterSeconds":N}` 並帶 `Retry-After`。插件只在打開附近列表時查一次，之後只有按重新整理才再查，而且自己每 15 秒最多一次，不會在背景輪詢。
- 加入：點列表裡的房間時，插件用 `wss://<網址>/rt?nearby=<附近代碼>`（Jam 另加 `kind=jam`）連線，再送一般的 `stream.join`／`jam.join`（帶附近代碼）。看片派對：1.3.7.0 起主持人的 `stream.state` 帶 `nearbyFollowsApproval: true`，從附近列表來的人跟用房號加入的人一樣照 `approvalRequired` 處理，沒開審核就直接進房，派對進行中把審核關掉時待審的人立刻進房；目前的主持人沒帶這個欄位（1.3.6.0 以前的插件，它們的設定頁寫著附近的人一律要核准）時，從附近列表來的人不論房間有沒有開審核，一律先待審。換主持人時這個欄位歸零，等新主持人的 `stream.state` 再帶。Jam 從附近列表來的人一律先待審。進房時收到的 `stream.joined`／`jam.joined` 才帶真正的房號，插件之後重連就改用房號。房間沒開啟附近、已經關掉，或拿房號當附近代碼時，升級回 404。
- 換主持人：看片派對的附近開關跟著房間走，伺服器保留附近代碼，附近列表改顯示新主持人的角色名稱；`stream.hostChanged` 的 `discoverable` 由伺服器依房間目前有沒有開啟附近填入，不沿用前主持人 state 裡的值（1.3.4.0 以前的插件預設送 `true`）。新主持人的插件看到 `features` 有位元 8 時沿用這個值，設定頁顯示為開啟，可以自己關掉；舊插件接手時會在第一個 `stream.state` 把附近關掉。

## 需求

- Cloudflare 帳號（免費方案即可）。
- Node.js 22 以上與 pnpm。

## 部署

以下指令在 bash 執行：

```bash
cd server
pnpm install
pnpm test                          # 伺服器測試
npx wrangler login                 # 登入要部署的 Cloudflare 帳號
npx wrangler whoami                # 確認帳號
npx wrangler secret put INVITE_CODE   # 輸入邀請碼（不會寫進 repo）
npx wrangler deploy                # 部署到 workers.dev，輸出網址 https://cyberphone-relay.<子網域>.workers.dev
```

- 同一個 Cloudflare 帳號內，`wrangler.toml` 的 `[[ratelimits]] namespace_id` 必須唯一；帳號裡已有別的 Worker 用了 `4101`、`4102` 或 `4103` 時要改成沒用過的數字。
- 沒設 `INVITE_CODE` 時，所有請求都回 500，不會開放。
- `INVITE_CODE` 可以放多組碼，用逗號分隔（例如 `社群A的碼,社群B的碼`），每組各自有效；碼本身不能含逗號。

把連線碼私下交給要一起開派對的朋友；只想加入的人，直接傳派對邀請給他就好。

## 改在自己的機器跑（Docker＋Cloudflare Tunnel）

不想吃 Cloudflare Workers 額度時，可以在 NAS 或任何 x86-64／arm64 的 Docker 主機上用 `workerd serve`（Cloudflare Workers 的開源執行環境）跑同一份程式，對外經 Cloudflare Tunnel 提供 https 網址。image 建置時用 `wrangler deploy --dry-run` 打包，執行時只有 workerd，設定在 `server/workerd/relay.capnp`，對應 `wrangler.toml` 的 Durable Object、限速與變數（兩邊要一起改）；`[[ratelimits]]` 由 `server/workerd/ratelimit.js` 依同樣的每分鐘固定視窗計數。主機重開機時進行中的派對與通話會中斷。

1. 在 Cloudflare Zero Trust 建一個 Tunnel，Public hostname 指向 `http://relay:8787`，path 設成 `^/(rt|rooms/nearby|auth/challenge|auth/verify|me|content/announcements|content/calendar|content/news-tw|content/radio)$`，其餘路徑回 404，只公開插件會用到的路徑。通話走同一個 `/rt`，不必另加路徑或 hostname；附近列表的 `/rooms/nearby` 與 `/content/` 底下的內容都由 `gateway` 送到 `relay-main`，`server/gateway.conf` 不用改。
2. 把 `server/` 複製到主機（不含 `node_modules`），同目錄放兩個不進 repo 的檔：`.env` 寫 `TUNNEL_TOKEN=<Tunnel token>`，`relay.env` 寫 `INVITE_CODE=<邀請碼>`（多組用逗號分隔）。
3. 在該目錄執行 `docker compose up -d --build --remove-orphans`（`--remove-orphans` 只在從單一 `relay` 容器的舊版升級時需要）。compose 不對外開 port，只有 cloudflared 連得到伺服器。

compose 有四個服務：`gateway`（nginx，網路別名 `relay`，Tunnel 連的就是它）、`relay-main`（看片派對、Jam、一起玩、公告、行事曆、台服新聞與精選電台）、`relay-call`（只跑語音通話）和 `tunnel`。`gateway` 依 `/rt` 的 `kind` 參數分流：`kind=call` 送到 `relay-call`，其餘送到 `relay-main`，設定在 `server/gateway.conf`。通話拆成獨立的程序，是因為 4 人一直同時說話的通話房開到 12 間就會讓 workerd 當掉，同一個程序裡的所有房間都會一起斷線；拆開後通話當掉只會斷通話（實測：通話容器當掉時，同一台的 170 人派對、Jam、遊戲 0 斷線）。兩個程序各自計算限速（見「上限與額度」）。

記憶體與健康檢查：`relay-main` 上限 1.5 GB、`relay-call` 上限 1 GB（compose 的 `mem_limit`），Docker 另外允許各自再用同樣大小的 swap。到上限時 kernel 會殺掉 workerd，容器跟著結束、由 Docker 重新啟動，只有那個容器裡的房間會斷線，不會拖垮整台主機；workerd 自己當掉時也一樣。workerd 內建的記憶體配置器（tcmalloc，編譯時沒有帶背景釋放）會留著最忙時用過的記憶體重複使用，不還給系統。所以容器的啟動腳本 `server/workerd/supervise.sh` 會在連續 `IDLE_RESTART_MINUTES` 分鐘（預設 15，設 `0` 關閉）沒有任何連線、且 workerd 超過 150 MB 時，重新啟動 workerd。這時沒有人在線，房間狀態本來就跟著連線結束，鬧鐘與 Jam 歌單存在磁碟上，不受影響。實測（單一程序，285 人尖峰連跑 4 輪）：啟動時 20 MB，每輪結束後閒置 204、214、219、222 MB，閒置 15 分鐘後回到 19 MB。改用 `workerd serve` 前用 `wrangler dev` 跑，同樣的測試每輪多約 70 MB，而且不會降回來。`relay-main`、`relay-call` 每 30 秒用 `GET /me`（預期 401）做健康檢查，`gateway` 用 `GET /healthz`（回 204，Tunnel 的 path 設定不會放行這個路徑）。連續 3 次失敗時，`docker ps` 會顯示 `unhealthy`，但 Docker 不會自動重啟容器。

換邀請碼：改 `relay.env` 後再執行一次 `docker compose up -d`（`restart` 不會重讀 `relay.env`）。

限制：邀請碼錯、房號不存在、房間人數已滿這類在 WebSocket 握手就被拒的情況，cloudflared 一律回 502，不會轉交伺服器的 401／404／409。插件因此只能顯示一般連線錯誤，觀眾端會先重試 3 次、重連中的看片派對主持人會先重試 6 次才放棄；部署在 Workers 上沒有這個問題。房型上限已滿、限速與通話關閉不受影響：伺服器會先完成握手再送出拒絕原因（見「認證與連線」）。通話房滿員與房號不符是連上之後由伺服器用 `call.declined` 回覆，也不受這個限制。

### 語音通話

開關是環境變數 `CALLS_ENABLED`（`1` 或 `true` 為開，其他值或沒設為關）。`wrangler.toml` 的 `[vars]` 設 `"0"`，所以部署到 Workers 時一定是關的；Docker 版的變數只看環境變數（`relay.capnp` 不讀 `wrangler.toml`，沒設的變數用程式預設值），`server/compose.yaml` 在 `relay-call` 服務設 `"1"`、`relay-main` 設 `"0"`。要在 Docker 上關掉通話，把 `relay-call` 的值改成 `"0"` 後執行 `docker compose up -d`。不要在 Workers 上開：理由見「上限與額度」。

頻寬（以實際幀大小計）：插件每 20 毫秒送一幀 Opus（28 kbps VBR，平均約 70 bytes），加 20 bytes 標頭約 90 bytes，有說話才送（靜音或按了靜音不送）。一個正在說話的人每秒 50 幀：上傳到伺服器約 4.8 KB/s（含 WebSocket 遮罩標頭），伺服器轉給每位聽的人約 4.6 KB/s。下表是 NAS 端的 WebSocket 流量；加上 TLS、TCP/IP 與 Tunnel 的封包開銷，實際網路流量約再乘 1.6。

| 情境（1 小時） | NAS 下行（收） | NAS 上行（送） |
|---|---|---|
| 2 人，一次一人說話 | 4.8 KB/s，約 17 MB | 4.6 KB/s，約 17 MB |
| 2 人，兩人一直同時說話（上限） | 9.6 KB/s，約 35 MB | 9.2 KB/s，約 33 MB |
| 4 人，一次一人說話 | 4.8 KB/s，約 17 MB | 13.8 KB/s，約 50 MB |
| 4 人，四人一直同時說話（上限） | 19.2 KB/s，約 69 MB | 55.2 KB/s，約 199 MB |

每位玩家說話時上傳約 4.8 KB/s（38 kbps），下載是同時說話人數乘 4.6 KB/s。

## 換邀請碼

```bash
cd server
npx wrangler secret put INVITE_CODE
```

輸入新碼即生效：舊碼立刻失效，用舊碼簽出的 token 也一起作廢。更新 secret 會讓 Worker 重新部署，進行中的派對會中斷。

分給不同社群時，可以每個社群一組碼，用逗號分隔放進同一個 `INVITE_CODE`（Docker 版放在 `relay.env`）。某組碼外流時，只要把那一組從清單拿掉再部署：拿掉的碼和用它簽出的 token 立刻失效，其他組不受影響。

## 公告、行事曆、台服新聞與電台內容

內容放在 `server/content/`，隨伺服器一起部署（Workers 打包進 Worker；Docker 映像在建置時複製進去），不寫 storage：

- `announcements.json`：`{"items": [...]}`，每則有 `id`（不重複的字串）、`title`、`body`（可用 `\n` 換行）、`createdAtUnix`（發布時間，Unix 秒）。插件依 `createdAtUnix` 由新到舊排，比使用者上次看過的還新就標成未讀。
- `calendar.json`：活動陣列，每筆有 `id`（不重複的正整數，決定顏色）、`name`、`begin`、`end`（ISO 8601 並帶時區，例如 `2026-10-22T09:00:00+08:00`）。`end` 不能早於 `begin`，否則該筆不顯示。

附的範例是一則「歡迎使用 Cyberphone」公告與一筆示範活動，請換成自己的內容。改完先跑測試，再重新部署才會生效：

```bash
cd server
pnpm test                 # 檢查兩份檔案的格式、id 不重複、時間合理
npx wrangler deploy       # 部署在 Workers 時
```

在 Docker 主機上跑時，把改好的 `content/` 複製到主機上的 `server/` 目錄，在該目錄執行 `docker compose up -d --build`（內容在建置映像時複製進去，只 `restart` 不會更新）。

伺服器對每份內容回 `ETag` 與 `Cache-Control: private, max-age=21600`，帶相同 `If-None-Match` 時回 304。插件把內容存在插件設定資料夾（`relay_announcements.json`、`relay_calendar.json`、`relay_news_tw.json`、`relay_radio.json`），6 小時內直接用本機那份，過期才帶 `If-None-Match` 重新檢查；讀取失敗時繼續顯示上次那份。所以改完內容不會立刻出現：公告要等使用者手上那份超過 6 小時後再打開「公告」；行事曆要等手上那份超過 6 小時後，下次啟動插件並顯示行事曆時才更新；台服新聞與電台清單要等手上那份超過 6 小時後再打開新聞 app 或電台頁。重新部署（或重建 Docker 容器）會中斷進行中的派對，挑沒人開派對時改。

### 台服新聞（`news-tw.json`）

Lodestone 沒有台服（繁體中文版）的新聞，所以台服客戶端的新聞 app 只讀這份，沒設定伺服器時只顯示「新聞需要聚會伺服器」，不會退回北美 Lodestone；其他客戶端照舊讀 Lodestone，不會用到它。格式是 `{"source", "updatedAt", "topics", "notices", "maintenance", "updates"}`，四個清單各自由新到舊，每則有 `id`（`tw-` 加官網的文章編號）、`url`、`title`、`time`（ISO 8601 帶時區），可選的 `description`（120 字內的摘要），維護公告另有 `start`、`end`（公告的維護時段）。插件把 `updatedAt` 當成「更新於」的時間，台服沒有「狀態」分頁。

這份檔案由 `server/scripts/fetch-tw-news.mjs` 從 FF14 繁體中文版官網的消息列表（`https://www.ffxiv.com.tw/web/news/news_list.aspx`）產生：官網的 `category=1`（活動）、`4`（其他）、`3`（維護）、`2`（更新）分別放進頭條新聞、公告、維護、更新。每類讀前 2 頁、留最新 12 則；舊檔裡已有的文章直接沿用，只有新文章（以及還沒抓到時段的維護公告）才讀內頁取摘要與時段；每次請求至少間隔 1.5 秒，User-Agent 是 `CyberphoneRelayNews/1.0`。只留標題、日期、分類、連結與摘要，不存全文與圖片；標題與摘要裡的 em dash、en dash 會換成連字號（repo 的 CI 不允許這兩個字元）。官網沒有 robots.txt（回 404）。結束代碼：`0` 內容有變並已寫檔、`3` 沒有變化（不動檔案）、`1` 失敗（不寫檔，伺服器繼續提供舊檔）；官網改版讓列表讀不到東西時也是 `1`。

手動更新：

```bash
cd server
node scripts/fetch-tw-news.mjs   # 寫到 content/news-tw.json，也可以在後面指定別的輸出檔
pnpm test                        # 檢查格式
npx wrangler deploy              # 部署在 Workers 時
```

在 NAS 上定時更新（主機不必裝 Node，用 Docker 的 `node:22-alpine` 跑一次就丟），在主機的 `server/` 目錄放一支腳本，例如 `update-news.sh`：

```bash
#!/bin/sh
cd /path/to/server || exit 1
docker run --rm -v "$PWD:/work" -w /work node:22-alpine node scripts/fetch-tw-news.mjs
if [ $? -eq 0 ]; then
  docker compose up -d --build relay-main
fi
```

把路徑換成主機上 `server/` 的實際位置，再用 NAS 的排程（例如 Synology 的「任務排程表」或 cron）以 root 執行。只有內容有變（結束代碼 `0`）才重建 `relay-main`；重建會中斷 `relay-main` 上進行中的派對、Jam 與遊戲房（通話在 `relay-call`，不受影響），所以建議每天 1 到 2 次、挑沒人在用的時段（例如清晨）。

### 精選電台（`radio.json`）

設定好伺服器後，Music 的電台頁會多一段「精選電台」，清單就是這份；沒設定時整段不出現，也不連線。格式是 `{"stations": [...]}`，每台有 `name`（顯示名稱）、`streamUrl`（直接的音訊串流網址，http 或 https；插件的播放器支援 MP3、AAC、Ogg Opus 與 HLS，不支援 Ogg Vorbis）、`genre`（顯示在名稱下方）、`homepage`（電台官網，滑鼠移到該列時可按地球圖示開啟，開啟前會先詢問）。同一個 `streamUrl` 只能出現一次；名稱空白或串流網址不是 http、https 的那台，插件會略過。附的 10 台（SomaFM、Radio Paradise、KEXP、Nightwave Plaza、Rainwave）都是公開、免費收聽的串流；要加台時，請確認對方允許用第三方播放器收聽。改完一樣先跑 `pnpm test`，再重新部署（Docker：在主機的 `server/` 目錄執行 `docker compose up -d --build relay-main`）。

## 認證與連線

- 插件只在按「開始派對」「發起 Jam」、在「一起玩」開房、按「開通話房」，或用房號加入時連線：`wss://<網址>/rt?create=1` 或 `?code=<房號>`（Jam 另加 `kind=jam`，遊戲房另加 `kind=game`，通話房另加 `kind=call`），另帶 `name`、`world`（角色名與母伺服器），邀請碼放在 `Authorization: Bearer` header。邀請碼錯誤由 Worker 直接回 401，不會碰到 Durable Object。遊戲房連上後，主持人送 `game.create`（帶遊戲種類）、加入者送 `game.join`（帶房號）完成握手，15 秒內沒回應插件就放棄並顯示連線錯誤。
- 拒絕：房型上限已滿、同一個網路開太多房、限速、通話關閉這四種，Worker 會完成 WebSocket 握手（不經 Durable Object），送一則 `{"type":"relay.refused","reason":…}` 後用 close code 關閉：`rooms_full`（帶 `maxRooms`，該房型上限）4503、`address_rooms`（帶 `maxRooms`，每個來源 IP 在該房型可同時開的房數）4409、`rate_limited`（帶 `retryAfterSeconds: 60`）4429、`calls_disabled` 4403。插件收到後立刻放棄、不重試，依原因顯示「伺服器已滿」「同一個網路已開太多房間」「連線太頻繁，{n} 秒後再試」或「無法使用通話」；限速時狀態列的限速提示也會倒數。這樣經過 Cloudflare Tunnel 也看得到原因。舊版伺服器回的 HTTP 503／429／403 照同樣方式處理。
- 開房分配：每種房型有一個 `SlotRegistry` Durable Object 負責分配房間槽位，一次只處理一個開房請求，先問過該房型每個槽位目前的主持人與開房 IP 再決定。同一個角色已經在主持該房型的房間時，新的開房會送到同一個槽位，結束舊房、換新房號；同一個來源 IP 已經開了 `MAX_ROOMS_PER_ADDRESS` 間時回 `address_rooms`。房間的開房 IP 跟著房間到結束為止（主持權轉移也不變）。`SlotRegistry` 只在記憶體保留剛分配、主持人還沒連上的槽位（最多 10 秒），不寫 storage；每次開房多花 2 次 Durable Object 請求，外加每個槽位各 1 次查詢。
- 通話房連上後，每人送 `call.join`（加入者帶房號），伺服器回 `call.roster`（房號、通話 ID、成員與座位號），之後成員變動都會重發；房號錯回 `call.declined`（`reason: code`），滿 4 人回 `call.declined`（`reason: full`），房間到 6 小時回 `call.ended`，三者都會關連線，插件不重連。語音是二進位幀：1 byte 版本、16 bytes 通話 ID、1 byte 座位號、2 bytes 序號，再接一個 Opus 封包。開房的人斷線後用伺服器給的房號重連，和加入者一樣。20 秒內沒收到 `call.roster` 插件就放棄並顯示連線錯誤。
- 通話審核：新版插件的 `call.join` 都帶 `approvalRequired: true`，伺服器只採用開房者（建立通話的角色，房間記為 `hostId`）第一次送的 `call.join`；舊版插件不帶這個欄位，那通電話維持拿到房號就能直接加入。審核開啟時，其他人送 `call.join` 會先收到 `{"type":"call.joinPending"}`，在核准前沒有座位號、收不到語音幀與 `call.roster`，自己送的語音幀也會被丟棄。開房者收到 `{"type":"call.joinRequest","userId":…,"from":{"userId","name","world","displayName","slot":-1,"state":"pending","muted":false}}`，回 `{"type":"call.approve","userId":…}` 讓對方入座並重發 `call.roster`（已滿 4 人則對方收 `call.declined`，`reason: full`），回 `{"type":"call.deny","userId":…}` 則對方收 `call.declined`（`reason: denied`）並斷線；等待中的人自己離開時，開房者收到 `{"type":"call.joinCancelled","userId":…}`。只有開房者能核准或拒絕，其他成員送的會被忽略。開房者不在通話中時，新來的人直接收 `call.declined`（`reason: host_away`），開房者離開時正在等待的人也一起收到 `host_away`：讓人無限期等待需要計時器或 alarm（會寫 storage），改由其他成員放行又會越過開房者的決定，所以直接拒絕。被核准過的角色（每通記最近 8 人）斷線重連不必再審，開房者不在時也一樣；開房者用房號重連不需審核，並會收到仍在等待的請求。審核狀態和其他通話資料一樣只放在 WebSocket attachment，不寫 storage。等待審核時插件暫停 20 秒的連線逾時，畫面顯示「正在等待房主…」，可按取消離開。同一個角色再開通話房時，以 `hostId` 判斷是否結束舊房（所有人收 `call.ended`，`reason: ended`），開房者用房號重連後仍算；開房者已不在、通話由其他人撐著時照舊回 409。
- 內容文件：`GET /content/announcements`、`GET /content/calendar`、`GET /content/news-tw`、`GET /content/radio`，同樣帶 `Authorization: Bearer <邀請碼>`，邀請碼錯誤回 401。
- 附近列表：`GET /rooms/nearby` 與 `/rt?nearby=<附近代碼>`，見上方「附近的派對與 Jam」。
- 身分只用「角色名@伺服器」，不驗證，同名同服可以冒用；知道房號的人用看片派對主持人的角色名連線，會被當成主持人重連而接手房間。
- 另外照上游規格保留 token 流程：`POST /auth/challenge`（`{name, world, invite}`）→ `POST /auth/verify`（`{challengeId}`）回 `{ok, token, user}`，之後 `GET /me` 或 `/rt` 帶 `Authorization: Bearer <token>`。呼叫者仍要提供邀請碼與房間參數，未修改的上游 client 不能直接使用。

## 上限與額度

Cloudflare 免費方案每天有 Workers 10 萬次、Durable Objects 10 萬次請求，整個帳號共用。伺服器的設計是不開派對就 0 請求：沒有排程、沒有常駐連線；看片派對、遊戲房與通話房的房間狀態只放在 WebSocket attachment，不寫 storage（看片派對的主持人斷線時例外：寬限的 60 秒內房間狀態寫在一個 storage key，主持人回來或房間結束就刪掉），看片派對與遊戲房每房最多一個關房用的 alarm（遊戲房只在開房、有人加入或最後一位客人離開時重設，出牌與落子都不寫 storage），通話房完全不寫 storage、不排程 alarm。Jam 的佇列與最後播放狀態放在一個 storage key，只在換歌、暫停、跳轉或佇列變動時寫入；主持人每 30 秒一次的心跳只轉送、不寫入。

彈幕和表情一樣只轉送、不寫 storage：每則送出算 1 則收到的 WebSocket 訊息，轉發給其他人不計費。插件每人一次最多連送 3 則、之後每 1.75 秒 1 則，6 人房一直送滿每小時約 1.2 萬則，即約 620 次 Durable Object 請求；伺服器的彈幕額度把不守規矩的 client 封在每人每分鐘約 70 則（通過 40 則加上不會被斷線的 30 則丟棄量）。

語音通話只開在自架 Docker，是因為在 Workers 上很貴：Durable Object 收到的 WebSocket 訊息每 20 則算 1 次請求，一個正在說話的人每秒送 50 幀，1 小時約 9,000 次。一次一人說話時不論 2 人或 4 人都是每小時約 9,000 次（約 11 小時用光全帳號每天的 10 萬次）；2 人一直同時說話約 1.8 萬次，4 人一直同時說話約 3.6 萬次，不到 3 小時就用光。Workers 上 `CALLS_ENABLED` 是關的，插件按「開通話房」只會換到 1 次 Worker 請求（回拒絕原因 `calls_disabled`），不會建立 Durable Object，所以是 0 次 Durable Object 請求。

附近列表每查一次是 1 次 Workers 請求，加上該房型 `SlotRegistry` 的 1 次與每個槽位各 1 次 Durable Object 請求（例如 `compose.yaml` 的派對 32 槽就是 33 次），只在玩家打開附近列表或按重新整理時發生；插件自己每 15 秒最多查 1 次，伺服器對每個來源 IP 先給 4 次、之後每 15 秒 1 次（見下表，額度放在 `SlotRegistry` 的記憶體，它閒置被回收時會重新計算）。開放附近的 Jam 主持人只在換區域或伺服器時多送 1 則 `jam.nearby`；看片派對的位置跟著原本的主持人狀態一起送，不另外加訊息。Jam 的主持人設定與核准名單跟佇列放在同一個 storage key，只在設定、核准或移出時多寫一次。

內容文件不經 Durable Object，也不寫 storage：每位使用者每份內容每 6 小時最多 1 次 Workers 請求（期間內重開遊戲也直接用本機那份）。公告與行事曆合計每人每天最多約 8 次；台服新聞只有台服客戶端打開新聞 app 時才讀，精選電台只有打開電台頁時才讀，各自每人每天最多約 4 次。不打開這些頁面就是 0 次。304 回應沒有內文，只吃請求數。

遊戲房的流量跟著玩家操作走，沒有逐幀同步：插件一次只送一個動作，等伺服器回覆（或 8 秒逾時）才送下一個；伺服器每接受一個動作，回給出手的人一則結果，再給房內每人發一則新盤面（Uno 與舷炮齊射另發一則本人手牌或船位）。插件端每人另有上限（一次最多 12 則，之後每 0.6 秒 1 則），低於伺服器的額度，正常遊玩不會被丟。以 1 小時、每 10 秒一手估算：一房約 360 個動作，伺服器收約 400 則（含握手與「時間到」請求）；2 人四子棋發約 1,100 則，4 人 Uno 發約 3,200 則。Durable Object 的 WebSocket 訊息只算收到的、每 20 則計 1 次請求，所以不論 2 人或 4 人都約 20 次，加上每人連線 1 次；Worker 請求只有每人連線那 1 次。房間用 Hibernation，只有處理訊息的那幾毫秒計入 duration；storage 寫入每房約 3 到 4 次（開房、第一位客人加入、最後一位客人離開時各重設一次 alarm，關房刪除 alarm）。

| 項目 | 值 | 超過時 |
|---|---|---|
| 同時房間數（每種房型） | `MAX_ROOMS_PARTY`、`MAX_ROOMS_JAM`、`MAX_ROOMS_GAME`、`MAX_ROOMS_CALL`，沒設的用 `MAX_ROOMS`（`wrangler.toml` 的 `[vars]` 設 3）；每個值最多 32。`compose.yaml` 設派對 32、Jam 16、一起玩 32、通話 8 | 建房被拒（`rooms_full`，close code 4503） |
| 每房人數（含主持人） | 6 | 加入回 409 |
| 通話房人數 | 4 | 回 `call.declined`（`reason: full`） |
| 主持人訊息速率 | 容量 40，每 2 秒補 1 | 超過的訊息丟棄；一分鐘內丟超過 30 則就斷線 |
| 觀眾訊息速率 | 容量 10，每 10 秒補 1 | 同上 |
| 表情反應速率（每人，與上面分開計） | 容量 6，每秒補 1 | 同上 |
| 彈幕速率（每人，與上面分開計） | 容量 4，每 1.5 秒補 1 | 同上 |
| 遊戲房訊息速率（每人） | 容量 20，每 0.5 秒補 1 | 同上 |
| 通話房文字訊息速率（每人） | 容量 10，每 2 秒補 1 | 同上 |
| 通話房語音幀速率（每人） | 容量 50，每秒補 60 | 超過的幀丟棄；10 秒內丟超過 200 幀就斷線（close code 1008） |
| 遊戲房人數 | Uno 6，其他 2 | 加入回「房間已滿」 |
| 單則訊息 | 文字 64 KiB（UTF-8，通話房 1 KiB）；只有通話房收二進位，每幀最多 1,295 bytes（20 bytes 標頭加 Opus 上限 1,275） | 斷線 |
| 房間壽命 | 6 小時；主持人獨處 30 分鐘（通話房由插件在獨處 30 分鐘時自動離開） | 房間結束 |
| 通過認證的 `/rt` 新連線 | 每個 Cloudflare 節點每分鐘：每個角色 10 次、每個來源 IP（`CF-Connecting-IP`）30 次、全站合計 600 次。Docker 上 `relay-main` 與 `relay-call` 各算一份 | 被拒（`rate_limited`，close code 4429） |
| 每個角色同時主持的房（每種房型） | 1 | 再開房會結束自己的舊房、換新房號 |
| 每個來源 IP 同時開的房（每種房型） | `MAX_ROOMS_PER_ADDRESS`，沒設是 3（`wrangler.toml` 為 3；Docker 沒另設也是 3）。沒有 `CF-Connecting-IP` 的連線（例如區網直連）全部算同一個來源 | 被拒（`address_rooms`，close code 4409） |
| 附近列表查詢（每個來源 IP、每種房型） | 容量 4，每 15 秒補 1；每次最多回 20 間 | 回 429（`rate_limited`，帶 `retryAfterSeconds` 與 `Retry-After`） |

看片派對的主持人斷線時，房間保留 60 秒等主持人重連（見上方「看片派對」的「主持人斷線」），逾時才結束。斷線沒傳到伺服器（舊連線還開著）時，主持人的插件重連會取代舊連線；同一個角色再開房則結束自己的舊房並改用新房號，不必等 6 小時。觀眾斷線後自動重連，連續失敗 3 次就放棄（連線維持 60 秒以上會重新計數）。

沒有邀請碼的請求每次仍會算 1 次 Workers 請求，這部分無法由程式封頂；不要公開網址。

## 開發

```bash
cd server
pnpm test         # vitest + @cloudflare/vitest-pool-workers
pnpm typecheck
npx wrangler dev  # 本機 http://127.0.0.1:8787；插件只接受 https，或 loopback 的 http
```

本機 `wrangler dev` 需要在 `server/.dev.vars` 寫一行 `INVITE_CODE=<測試用碼>`（已列在 `.gitignore`）。
