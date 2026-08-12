#!/usr/bin/env bash
# Compose bilingual notes and neutral first-launch instructions.
#
# Inputs (env): VERSION, BASE_VERSION
# Output: build/body.md

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${HERE}/lib.sh"
cd "$(repo_root)"

require_env VERSION BASE_VERSION

EN="ReleaseNotes/${BASE_VERSION}_en.md"
ZH="ReleaseNotes/${BASE_VERSION}_zh.md"
OUT="build/body.md"

[ -f "$EN" ] || die "Missing ${EN}"
[ -f "$ZH" ] || die "Missing ${ZH}"

mkdir -p build
{
    cat "$EN"
    printf '\n\n## Installation\n\n'
    printf '%s\n' \
        'Open the DMG and drag LyricsX to Applications.' \
        'If macOS blocks the first launch, open System Settings, choose Privacy & Security, and select Open Anyway for LyricsX.'
    printf '\n\n---\n\n'
    cat "$ZH"
    printf '\n\n## 安装\n\n'
    printf '%s\n' \
        '打开 DMG，然后将 LyricsX 拖到“应用程序”文件夹。' \
        '如果 macOS 阻止首次打开，请打开“系统设置”中的“隐私与安全性”，然后为 LyricsX 选择“仍要打开”。'
} > "$OUT"

log_info "Wrote ${OUT} ($(wc -l < "$OUT" | tr -d ' ') lines)"
