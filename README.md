# Cyberphone 下載

Cyberphone 是 FFXIV 台服用的 Dalamud 插件（遊戲內手機），改自 XeldarAlz 的 Aetherphone，授權 AGPL-3.0-or-later。本 repo 放插件庫設定、編譯好的檔案（在 release）與公開原始碼（在 [`source/`](source)），與 Aetherphone 官方無關，也不連官方 Aethernet。

## 安裝（Dalamud 插件庫）

1. 遊戲內輸入 `/xlsettings` → Experimental →「Custom Plugin Repositories」，貼上下面這條網址，勾選啟用，按儲存：

   ```
   https://raw.githubusercontent.com/Yshawn6720/Cyberphone-release/main/pluginmaster.json
   ```

2. `/xlplugins` → 搜尋 Cyberphone → 安裝。開啟指令 `/cyberphone` 或 `/cphone`。

之後有新版本，Dalamud 會自己提示更新。之前用 Dev Plugin Locations 載入過 Cyberphone 的人，先把那條路徑移除再安裝，兩份同時存在會載入失敗。

## 手動下載

- 插件：[Cyberphone.zip](https://github.com/Yshawn6720/Cyberphone-release/releases/latest/download/Cyberphone.zip)，解壓整個資料夾，在「Dev Plugin Locations」填 `Cyberphone.dll` 的完整路徑。
- 對應原始碼：每個 release 裡的 `Cyberphone-source-<commit>.zip`。

## 原始碼

[`source/`](source) 是 Cyberphone 的公開原始碼，授權 **AGPL-3.0-or-later**（[`source/LICENSE.md`](source/LICENSE.md)），修改聲明見 [`source/NOTICE`](source/NOTICE)，第三方元件見 [`source/THIRD-PARTY-NOTICES.md`](source/THIRD-PARTY-NOTICES.md)。目前對應原始碼 repo 的 commit `c7e0e405bad8f76097a2eb672093cde67bb0947c`。

內容包含建置插件需要的程式碼與專案檔（`src/`）、自架看片派對伺服器（`server/`，架設方式見 [`source/docs/self-hosting-relay.md`](source/docs/self-hosting-relay.md)）、素材產生工具（`tools/`）與授權檔；建置方式見 [`source/README.md`](source/README.md)。

## 一起看派對

主持人在 MogCast 的「一起看」開始聚會後按「複製邀請碼」，把整串傳給你；貼到「一起看」的加入欄就會加入。之後同一台伺服器只要輸入 6 碼房號。

## 授權

AGPL-3.0-or-later，見 [LICENSE.md](LICENSE.md)、[NOTICE](NOTICE)、[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。每個 release 都附上產生該版本的完整原始碼。
