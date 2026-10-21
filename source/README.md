# Cyberphone

FFXIV 台服（繁中）用的遊戲內手機插件。本專案是 [Aetherphone](https://github.com/XeldarAlz/FFXIV-Aetherphone)（XeldarAlz，AGPL-3.0-or-later）的**非官方 fork**，與 Aetherphone 及其 Aethernet 服務沒有任何關係。

> 非官方專案，與 SQUARE ENIX 無關。使用第三方插件的風險由使用者自行承擔。
> 有問題請回報到本 repo，**不要**回報給 Aetherphone 原作者。

## 與上游的差異

- **台服環境**：改以台服 Dalamud（API 13／.NET 9）建置。
- **不連官方 Aethernet**：所有連線都經過 `src/Aetherphone/Core/Net/OfficialHostGuard.cs`，官方網域在 DNS 解析前就被擋下；需要登入 Aethernet 的社群類 app（Chirper、Aethergram、Velvet、ChocoChat 等）、Strats、公告都已隱藏。`src/Aetherphone.Tests/OfflineGuaranteeTests.cs` 以測試守住這件事。
- **看片派對走自架伺服器**：MogCast 的看片派對改用自己架的轉送伺服器（[`server/`](server)，Cloudflare Worker＋Durable Object，或用 Docker 跑在自己的機器）。主持人在 MogCast 設定貼上連線碼即可開派對；朋友把主持人複製的派對邀請貼到「一起看」就能加入，不用另外設定。只有開派對或加入時才連線，沒設定就維持離線。架設方式見 [`docs/self-hosting-relay.md`](docs/self-hosting-relay.md)。
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
