#!/bin/bash
# Give ADOFAI camera access on macOS: add NSCameraUsageDescription and re-sign (ad-hoc) with the
# stock entitlements plus com.apple.security.device.camera. Run with the game closed.
#   mac-camera-patch.sh apply|undo|status [path/to/ADanceOfFireAndIce.app]
# Steam updates and "Verify integrity" restore the stock signature; apply again afterwards.
set -euo pipefail

cmd=${1:-}
APP=${2:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app"}
BK=${CAMPATCH_BACKUP:-"$HOME/Library/Application Support/CamDisplay/sig-backup"}
STOCK_RE=${CAMPATCH_STOCK_RE:-'^Authority=Developer ID Application'}   # tests point both at a toy app
USAGE='CamDisplay shows your hand cam in-game.'
PL="$APP/Contents/Info.plist"
[ -f "$PL" ] || { echo "no app at $APP" >&2; exit 2; }
EXE=$(plutil -extract CFBundleExecutable raw "$PL")

# Capture first, then grep: `codesign | grep -q` under pipefail fails when grep exits early.
is_patched() {
    local ent; ent=$(codesign -d --entitlements - "$APP" 2>/dev/null || true)
    grep -q 'com.apple.security.device.camera' <<<"$ent" &&
        plutil -extract NSCameraUsageDescription raw "$PL" >/dev/null 2>&1
}
# Stock = the developer's signature AND it still verifies: a half-applied patch keeps the
# Authority line but fails --verify, and must never become the backup.
stock_signed() {
    codesign --verify "$APP" 2>/dev/null || return 1
    local sig; sig=$(codesign -dvv "$APP" 2>&1 || true); grep -q "$STOCK_RE" <<<"$sig"
}
version() { plutil -extract CFBundleVersion raw "$1" 2>/dev/null || echo "?"; }

case "$cmd" in
status)
    if is_patched; then echo patched; else echo stock; fi
    ;;
apply)
    if is_patched; then echo "already patched"; exit 0; fi
    # Neither stock nor ours (a half-applied patch, a partial update): patching over it would
    # back up the wrong thing. Steam's Verify integrity gets back to stock.
    stock_signed || { echo "the game's signature is not the stock one; run Steam > Verify integrity first" >&2; exit 1; }
    ENT=$(mktemp -t camdisplay-ent)
    trap 'rm -f "$ENT"' EXIT
    # --xml, not the deprecated ':-' form; an empty read must stop us, never sign with no entitlements.
    codesign -d --entitlements - --xml "$APP" > "$ENT" 2>/dev/null || true
    [ -s "$ENT" ] || { echo "could not read the game's entitlements; nothing changed" >&2; exit 1; }
    /usr/libexec/PlistBuddy -c 'Delete :com.apple.security.device.camera' "$ENT" >/dev/null 2>&1 || true
    /usr/libexec/PlistBuddy -c 'Add :com.apple.security.device.camera bool true' "$ENT"
    # Refreshed from every stock app, so it follows game updates; a patched app never reaches here.
    rm -rf "$BK" && mkdir -p "$BK"
    cp -p "$PL" "$BK/Info.plist"
    cp -Rp "$APP/Contents/_CodeSignature" "$BK/_CodeSignature"
    cp -p "$APP/Contents/MacOS/$EXE" "$BK/$EXE"
    plutil -replace NSCameraUsageDescription -string "$USAGE" "$PL"
    codesign --force --sign - --options runtime --entitlements "$ENT" "$APP" ||
        { cp -p "$BK/Info.plist" "$PL"; echo "re-signing failed; Info.plist restored" >&2; exit 1; }
    codesign --verify "$APP"
    echo patched
    ;;
undo)
    # An unpatched app (e.g. after a Steam update) is already stock; a backup copied over it
    # could be from an older build with the same CFBundleVersion.
    is_patched || { echo "not patched; nothing to undo"; exit 0; }
    [ -f "$BK/Info.plist" ] || { echo "no backup; use Steam > Verify integrity"; exit 1; }
    if [ "$(version "$BK/Info.plist")" != "$(version "$PL")" ]; then
        echo "backup is from game build $(version "$BK/Info.plist"), app is $(version "$PL"); use Steam > Verify integrity"
        exit 1
    fi
    cp -p "$BK/Info.plist" "$PL"
    rm -rf "$APP/Contents/_CodeSignature"
    cp -Rp "$BK/_CodeSignature" "$APP/Contents/_CodeSignature"
    cp -p "$BK/$EXE" "$APP/Contents/MacOS/$EXE"
    codesign --verify "$APP"
    codesign -dvv "$APP" 2>&1 | grep '^Authority=' | head -1
    ;;
*)
    echo "usage: $(basename "$0") apply|undo|status [app]" >&2
    exit 2
    ;;
esac
