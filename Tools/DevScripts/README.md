# DevScripts

开发期调试脚本（独立零依赖，除标注外仅需 python3 + websocket-client）。

| 脚本 | 用途 |
| --- | --- |
| `cdp_targets.py` | 列出微信开发者工具 CDP(9223) 的全部调试目标，确认 `小游戏运行时`/gamePage webview 存活 |
| `sim_probe.py` | 附加模拟器 webview：抓 console、截图（`--out`）、点击（`--tap x,y`）、求值（`--eval`）。**不要用 `--reload`**，它会杀掉 webview 目标；要重启会话用 `cli open --project Builds/WeChatMiniGame/minigame` |
| `strip_zombie_pieces.py` | 清理场景里被编辑器固化的运行时 `piece-X-Y` 对象（按 `--- !u!` 文档边界做 YAML 手术，级联删组件并过滤 SceneRoots）。用法：`python3 strip_zombie_pieces.py <场景文件>`，改前必备份 |
| `extract_essentials.py` | 手动解包 .unitypackage 到 Assets/（保 .meta/GUID）。用法：`python3 extract_essentials.py <包路径> <工程Assets绝对路径>`。背景：`AssetDatabase.ImportPackage` 在 `-nographics` 批处理下会静默失效 |

前置：开发者工具需带 CDP 启动
`open -a wechatwebdevtools --args --remote-debugging-port=9223`
（websocket-client 必须以 `suppress_origin=True` 连接，脚本已处理）。

相关坑与流程详见 CODELY.md 项目记忆（预览码时效、Timeout 重启、TMP 中文字体子集管线）。
