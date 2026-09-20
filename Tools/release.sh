#!/usr/bin/env bash
# 一条命令发布流水线：字体子集烘焙 → 微信小游戏构建转换 → 包体红线检查 → 可选 preview/upload
# 用法:
#   Tools/release.sh              # 烘焙+构建+包体检查
#   Tools/release.sh preview      # 额外生成预览二维码（Builds/wx_qr.png）
#   Tools/release.sh upload       # 额外上传开发版本（VER 环境变量指定版本号，默认 1.2.2）
set -euo pipefail

PROJ="$(cd "$(dirname "$0")/.." && pwd)"
TJ="/Applications/Tuanjie/Hub/Editor/2022.3.62t12/Tuanjie.app/Contents/MacOS/Tuanjie"
CLI="/Applications/wechatwebdevtools.app/Contents/MacOS/cli"
MG="$PROJ/Builds/WeChatMiniGame/minigame"
MODE="${1:-build}"
VER="${VER:-1.2.2}"
mkdir -p "$PROJ/Logs"

step() { echo; echo "== $* =="; }

step "1/4 字体子集烘焙（UI 中文字符集）"
"$TJ" -batchmode -quit -nographics -projectPath "$PROJ" \
  -executeMethod TmpFontSubsetBake.Execute -logFile "$PROJ/Logs/bake.log"
grep -q "BAKED" "$PROJ/Logs/bake.log" || { echo "❌ 字体烘焙失败"; exit 1; }
grep -E "BAKED" "$PROJ/Logs/bake.log" | head -1

step "2/4 微信小游戏构建转换"
"$TJ" -batchmode -quit -nographics -projectPath "$PROJ" \
  -executeMethod WeixinMinigameConvert.Convert -logFile "$PROJ/Logs/convert.log"
grep -q "CONVERT SUCCESS" "$PROJ/Logs/convert.log" || { echo "❌ 构建转换失败"; exit 1; }

step "3/4 包体红线检查（主包 ≤4MB，总包 ≤20MB）"
# 微信不计入 webgl.wasm.symbols*（本地调试符号），统计时排除
sym_kb=$(( $(du -sk "$MG/webgl.wasm.symbols.unityweb" 2>/dev/null | cut -f1 || echo 0) + $(du -sk "$MG/webgl.wasm.symbols.unityweb.br" 2>/dev/null | cut -f1 || echo 0) ))
total_kb=$(du -sk "$MG" | cut -f1)
sub_kb=$(( $(du -sk "$MG/data-package" 2>/dev/null | cut -f1 || echo 0) + $(du -sk "$MG/wasmcode" 2>/dev/null | cut -f1 || echo 0) ))
main_kb=$(( total_kb - sub_kb - sym_kb ))
up_total_kb=$(( main_kb + sub_kb ))
echo "main=${main_kb}KB  sub=$((sub_kb))KB  uploaded_total=${up_total_kb}KB  (symbols ${sym_kb}KB 不计入)"
if [ "$main_kb" -gt 4096 ]; then echo "❌ 主包超 4MB"; exit 1; fi
if [ "$up_total_kb" -gt 20480 ]; then echo "❌ 上传总量超 20MB（本地分包上限）——需要上 CDN"; exit 1; fi
if [ "$up_total_kb" -gt 19456 ]; then echo "⚠️ 上传总量已超 19MB，距 20MB 上限不足 1MB，新增资源须走 CDN"; fi
echo "✅ 包体达标"

step "4/4 分发（mode=$MODE）"
case "$MODE" in
  preview)
    "$CLI" preview --project "$MG" --qr-format image --qr-output "$PROJ/Builds/wx_qr.png"
    open "$PROJ/Builds/wx_qr.png"
    ;;
  upload)
    "$CLI" upload --project "$MG" -v "$VER" -d "${DESC:-拼图更新}"
    ;;
  *) echo "本地构建完成，未分发";;
esac
echo "🎉 release done"
