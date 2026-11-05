#!/bin/bash
# Give ADOFAI camera access on macOS: add NSCameraUsageDescription and re-sign (ad-hoc) with the
# stock entitlements plus com.apple.security.device.camera. Run with the game closed.
#   mac-camera-patch.sh apply|undo|status [path/to/ADanceOfFireAndIce.app]
# Steam updates and "Verify integrity" restore the stock signature; apply again afterwards.
set -euo pipefail

cmd=${1:-}
APP=${2:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app"}
BK="$HOME/Library/Application Support/CamDisplay/sig-backup"
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
stock_signed() { local sig; sig=$(codesign -dvv "$APP" 2>&1 || true); grep -q '^Authority=Developer ID Application' <<<"$sig"; }
version() { plutil -extract CFBundleVersion raw "$1" 2>/dev/null || echo "?"; }

case "$cmd" in
status)
    if is_patched; then echo patched; else echo stock; fi
    ;;
apply)
    if is_patched; then echo "already patched"; exit 0; fi
    # Only a stock-signed app is a backup worth keeping; refreshing it each time keeps it in step
    # with game updates, and a re-run on a patched app never overwrites it.
    if stock_signed; then
        rm -rf "$BK" && mkdir -p "$BK"
        cp -p "$PL" "$BK/Info.plist"
        cp -Rp "$APP/Contents/_CodeSignature" "$BK/_CodeSignature"
        cp -p "$APP/Contents/MacOS/$EXE" "$BK/$EXE"
    else
        echo "warning: app is not stock-signed; keeping the existing backup (if any)" >&2
    fi
    ENT=$(mktemp -t camdisplay-ent)
    codesign -d --entitlements :- "$APP" > "$ENT" 2>/dev/null || true
    [ -s "$ENT" ] || printf '<?xml version="1.0" encoding="UTF-8"?>\n<plist version="1.0"><dict/></plist>\n' > "$ENT"
    /usr/libexec/PlistBuddy -c 'Delete :com.apple.security.device.camera' "$ENT" >/dev/null 2>&1 || true
    /usr/libexec/PlistBuddy -c 'Add :com.apple.security.device.camera bool true' "$ENT"
    plutil -replace NSCameraUsageDescription -string "$USAGE" "$PL"
    codesign --force --sign - --options runtime --entitlements "$ENT" "$APP"
    rm -f "$ENT"
    codesign --verify "$APP"
    echo patched
    ;;
undo)
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
