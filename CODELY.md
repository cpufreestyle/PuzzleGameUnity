

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-09-06 20:09:57] [project] PuzzleGameUnity 仓库：本地是浅克隆（.git/shallow 截断于 5b98f59，无法直接 push 完整历史）；2026-09-06 起远端默认分支为 main 且历史被压成单提交 680d67f（内容=本地旧 3651d69），旧 origin/master 跟踪已废弃。**Why:** push 报 "remote unpack failed / did not receive expected object" 即浅克隆缺父对象所致。**How to apply:** 推送用 `git push origin main`（上游已指向 origin/main）；若再有历史重写，先 `git fetch origin main` 比对，把新提交 rebase --onto 远端提交后再推。
- [2026-09-06 21:57:58] [project] 本机访问 GitHub：HTTPS(443) 直连不可靠（git 报 "Error in the HTTP2 framing layer"/连接超时，api.github.com 403），SSH 通道（git@github.com）稳定可用。**Why:** 2026-09-06 克隆 minigame-tuanjie-transform-sdk 时 HTTPS 两次失败、SSH 一次成功。**How to apply:** git clone/添加 UPM 包一律用 SSH URL；UPM git 包也可先手动 SSH 克隆到 .codely-cli/extensions/ 再以 file: 引用（与 TJGenerators 同模式）。
- [2026-09-06 22:27:08] [project] 微信小游戏工具链（2026-09-06）：① 团结 `BuildTarget.WeixinMiniGame` 只产出 wasm player（index.html+.br），不含 game.json，转小程序必须用微信转换SDK（WXSDK，入口 `WXConvertCore.DoExport(true)`，产物在 DST/minigame，`WXExportError` 是 WXConvertCore 的嵌套枚举，配置资产路径硬编码为 `Assets/WX-WASM-SDK-V2/Editor/` 需预先建目录）；② 原版 wechat-miniprogram/minigame-unity-webgl-transform 已被 GitHub 下架，可用 fork 是 minigame-tuanjie-transform-sdk（已克隆到 .codely-cli/extensions/ 作源缓存）；③ SDK 的两个 asmdef 引用不存在的 `GUID:39e0a8d7...`/`Unity.InstantGame.Editor`，需删掉（有 #if UNITY_INSTANTGAME 保护，安全）；④ **关键坑：file: 引用（manifest 或 Packages/ 内嵌）在本机团结上会静默失败——包注册成功但内容永不导入、无任何报错，删缓存也无效；必须按官方手册把 SDK 直接放进 Assets/**（Editor+Runtime→Assets/WX-WASM-SDK，WebGLTemplates→Assets/WebGLTemplates）。**How to apply:** 批处理转换脚本 Assets/Editor/WeixinMinigameConvert.cs；AppID 占位 touristappid；输出 Builds/WeChatMiniGame/minigame；用户习惯把构建产物提交进仓库（Builds/ 已含 apk 和 wasm 输出）。


### Reference

