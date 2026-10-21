# 自架看片派對轉送伺服器

Cyberphone 不連官方 Aethernet。MogCast 的看片派對改用使用者自己架的轉送伺服器：程式碼在 [`server/`](../server)，是一個 Cloudflare Worker 加上每個房間槽位一個 Durable Object（Hibernation）。依 AGPL-3.0-or-later，任何人都可以自己架一台。

插件只有在要開派對或加入派對時才連線，沒設定伺服器就完全不連線。兩種字串：

- **連線碼**：`<網址>#<邀請碼>`，例如 `party.example.com#K7M2Q9XR4T`（網址省略 `https://`）。填在 MogCast 設定 →「聚會伺服器」，輸入當下就存；想自己開派對的人才需要。邀請碼若含英數字與 `-._~` 以外的字元（例如 `#`、空白），連線碼裡要用 percent-encoding（`#` 寫成 `%23`）；插件產生的連線碼與派對邀請會自動編碼。
- **派對邀請**：`<網址>#<邀請碼>#<房號>`。主持人在派對裡按「複製邀請碼」就會複製這一串。朋友貼到「一起看」分頁的加入欄，插件會自動存好伺服器並加入，不用先改設定；之後再加入只要輸入 6 碼房號。

## 只做看片派對

支援建房、用房號加入、主持人狀態同步、觀眾播放控制（由主持人決定是否允許）、離開、換主持人。審核加入、佇列建議、表情反應、移出成員、附近派對、播放失敗回報、Jam 與其他社群功能都還沒做，插件在使用轉送伺服器時會把這些入口藏起來。

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

- 同一個 Cloudflare 帳號內，`wrangler.toml` 的 `[[ratelimits]] namespace_id` 必須唯一；帳號裡已有別的 Worker 用了 `4101` 時要改一個沒用過的數字。
- 沒設 `INVITE_CODE` 時，所有請求都回 500，不會開放。

把連線碼私下交給要一起開派對的朋友；只想加入的人，直接傳派對邀請給他就好。

## 改在自己的機器跑（Docker＋Cloudflare Tunnel）

不想吃 Cloudflare Workers 額度時，可以在 NAS 或任何 x86-64／arm64 的 Docker 主機上用 `wrangler dev` 跑同一份程式，對外經 Cloudflare Tunnel 提供 https 網址。`wrangler dev` 是開發用的執行環境，官方不保證長期穩定；主機重開機時進行中的派對會中斷。

1. 在 Cloudflare Zero Trust 建一個 Tunnel，Public hostname 指向 `http://relay:8787`，path 設成 `^/(rt|auth/challenge|auth/verify|me)$`，其餘路徑回 404。`wrangler dev` 另外開著 `/cdn-cgi/local/explorer` 等除錯介面，不能對外公開，所以一定要設 path。
2. 把 `server/` 複製到主機（不含 `node_modules`），同目錄放兩個不進 repo 的檔：`.env` 寫 `TUNNEL_TOKEN=<Tunnel token>`，`relay.env` 寫 `INVITE_CODE=<邀請碼>`。
3. 在該目錄執行 `docker compose up -d --build`。compose 不對外開 port，只有 cloudflared 連得到伺服器。

換邀請碼：改 `relay.env` 後再執行一次 `docker compose up -d`（`restart` 不會重讀 `relay.env`）。

限制：WebSocket 握手被拒時（邀請碼錯、房號不存在、房間已滿、限速），cloudflared 一律回 502，不會轉交伺服器的 401／404／409／429。插件因此只能顯示一般連線錯誤，觀眾端會先重試 3 次才放棄；部署在 Workers 上沒有這個問題。

## 換邀請碼

```bash
cd server
npx wrangler secret put INVITE_CODE
```

輸入新碼即生效：舊碼立刻失效，用舊碼簽出的 token 也一起作廢。更新 secret 會讓 Worker 重新部署，進行中的派對會中斷。

## 認證與連線

- 插件只在按「開始派對」或用房號加入時連線：`wss://<網址>/rt?create=1` 或 `?code=<房號>`，另帶 `name`、`world`（角色名與母伺服器），邀請碼放在 `Authorization: Bearer` header。邀請碼錯誤由 Worker 直接回 401，不會碰到 Durable Object。
- 身分只用「角色名@伺服器」，不驗證，同名同服可以冒用。
- 另外照上游規格保留 token 流程：`POST /auth/challenge`（`{name, world, invite}`）→ `POST /auth/verify`（`{challengeId}`）回 `{ok, token, user}`，之後 `GET /me` 或 `/rt` 帶 `Authorization: Bearer <token>`。呼叫者仍要提供邀請碼與房間參數，未修改的上游 client 不能直接使用。

## 上限與額度

Cloudflare 免費方案每天有 Workers 10 萬次、Durable Objects 10 萬次請求，整個帳號共用。伺服器的設計是不開派對就 0 請求：沒有排程、沒有常駐連線；房間狀態只放在 WebSocket attachment，不寫 storage，每房最多一個關房用的 alarm。

| 項目 | 值 | 超過時 |
|---|---|---|
| 同時房間數 | `MAX_ROOMS`（`wrangler.toml` 的 `[vars]`，預設 3） | 建房回 503 |
| 每房人數（含主持人） | 6 | 加入回 409 |
| 主持人訊息速率 | 容量 40，每 2 秒補 1 | 超過的訊息丟棄；一分鐘內丟超過 30 則就斷線 |
| 觀眾訊息速率 | 容量 10，每 10 秒補 1 | 同上 |
| 單則訊息 | 文字 64 KiB（UTF-8）；不收二進位訊息 | 斷線 |
| 房間壽命 | 6 小時；主持人獨處 30 分鐘 | 房間結束 |
| 通過認證的 `/rt` 新連線 | 每個 Cloudflare 節點每分鐘 10 次 | 回 429 |

主持人斷線就結束房間；斷線沒傳到伺服器（舊連線還開著）時，同一個角色再開房會結束自己的舊房並改用新房號，不必等 6 小時。觀眾斷線後自動重連，連續失敗 3 次就放棄（連線維持 60 秒以上會重新計數）。

沒有邀請碼的請求每次仍會算 1 次 Workers 請求，這部分無法由程式封頂；不要公開網址。

## 開發

```bash
cd server
pnpm test         # vitest + @cloudflare/vitest-pool-workers
pnpm typecheck
npx wrangler dev  # 本機 http://127.0.0.1:8787；插件只接受 https，或 loopback 的 http
```

本機 `wrangler dev` 需要在 `server/.dev.vars` 寫一行 `INVITE_CODE=<測試用碼>`（已列在 `.gitignore`）。
