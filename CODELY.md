

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-09-06 20:09:57] [project] PuzzleGameUnity 仓库：本地是浅克隆（.git/shallow 截断于 5b98f59，无法直接 push 完整历史）；2026-09-06 起远端默认分支为 main 且历史被压成单提交 680d67f（内容=本地旧 3651d69），旧 origin/master 跟踪已废弃。**Why:** push 报 "remote unpack failed / did not receive expected object" 即浅克隆缺父对象所致。**How to apply:** 推送用 `git push origin main`（上游已指向 origin/main）；若再有历史重写，先 `git fetch origin main` 比对，把新提交 rebase --onto 远端提交后再推。
- [2026-09-06 21:57:58] [project] 本机访问 GitHub：HTTPS(443) 直连不可靠（git 报 "Error in the HTTP2 framing layer"/连接超时，api.github.com 403），SSH 通道（git@github.com）稳定可用。**Why:** 2026-09-06 克隆 minigame-tuanjie-transform-sdk 时 HTTPS 两次失败、SSH 一次成功。**How to apply:** git clone/添加 UPM 包一律用 SSH URL；UPM git 包也可先手动 SSH 克隆到 .codely-cli/extensions/ 再以 file: 引用（与 TJGenerators 同模式）。

### Reference

