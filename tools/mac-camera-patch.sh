#!/bin/bash
# Give ADOFAI camera access on macOS: add NSCameraUsageDescription and re-sign with the stock
# entitlements plus com.apple.security.device.camera. Run with the game closed.
#   mac-camera-patch.sh apply|undo|status [path/to/ADanceOfFireAndIce.app]
# Steam updates and "Verify integrity" restore the stock signature; apply again afterwards.
#
# Signed with a certificate made once per machine, not ad hoc: macOS keeps a permission grant
# (Input Monitoring, Camera) against the signature's designated requirement. Ad hoc that is the
# binary's hash, so every re-sign looked like a new app and the game lost its keyboard hook. A
# certificate's requirement is its leaf hash, the same for every build it signs.
set -euo pipefail

cmd=${1:-}
APP=${2:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce.app"}
BK=${CAMPATCH_BACKUP:-"$HOME/Library/Application Support/CamDisplay/sig-backup"}
STOCK_RE=${CAMPATCH_STOCK_RE:-'^Authority=Developer ID Application'}   # tests point these at a toy app
SD=${CAMPATCH_SIGNING:-"$HOME/Library/Application Support/CamDisplay/signing"}
KC="$SD/CamDisplay.keychain-db"
KP="$SD/password"
CN='CamDisplay Local Signing'
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

# The identity lives in a keychain of its own with a stored password: the patch runs unattended
# after the game exits, so nothing may prompt, and the login keychain is left alone. The cert
# needs no trust setting to sign. /usr/bin/openssl (LibreSSL): Homebrew's OpenSSL 3 writes a
# PKCS#12 that `security import` cannot read.
ensure_identity() {
    [ -f "$KC" ] && [ -s "$KP" ] && return 0
    rm -rf "$SD"; mkdir -p "$SD"; chmod 700 "$SD"
    local t pass; t=$(mktemp -d)
    pass=$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 32)
    printf '[req]\ndistinguished_name=dn\nx509_extensions=ext\nprompt=no\n[dn]\nCN=%s\n[ext]\nbasicConstraints=critical,CA:false\nkeyUsage=critical,digitalSignature\nextendedKeyUsage=critical,codeSigning\n' "$CN" > "$t/cert.cnf"
    /usr/bin/openssl req -x509 -newkey rsa:2048 -nodes -days 7300 -keyout "$t/key.pem" -out "$t/cert.pem" \
        -config "$t/cert.cnf" 2>/dev/null &&
    /usr/bin/openssl pkcs12 -export -inkey "$t/key.pem" -in "$t/cert.pem" -out "$t/id.p12" -passout "pass:$pass" &&
    security create-keychain -p "$pass" "$KC" &&
    security unlock-keychain -p "$pass" "$KC" &&
    security import "$t/id.p12" -k "$KC" -P "$pass" -T /usr/bin/codesign >/dev/null &&
    security set-key-partition-list -S apple-tool:,apple: -s -k "$pass" "$KC" >/dev/null &&
    printf %s "$pass" > "$KP" && chmod 600 "$KP"
    local rc=$?
    rm -rf "$t"
    [ $rc -eq 0 ] || { rm -rf "$SD"; return 1; }
    security lock-keychain "$KC"
}

# codesign finds an identity only in a keychain on the user's search list, even with --keychain,
# so ours joins the list for the one call and the list goes back exactly as it was.
# ponytail: last writer wins if something else edits the search list during the sign.
SAVED_LIST=()
restore_list() {
    [ ${#SAVED_LIST[@]} -gt 0 ] && security list-keychains -d user -s "${SAVED_LIST[@]}"
    SAVED_LIST=()
    [ -f "$KC" ] && security lock-keychain "$KC" 2>/dev/null
    return 0
}
sign_with_identity() {   # $1 = entitlements file
    ensure_identity || return 1
    security unlock-keychain -p "$(cat "$KP")" "$KC" || return 1
    local id; id=$(security find-identity -p codesigning "$KC" | awk '$1 == "1)" { print $2; exit }')
    [ -n "$id" ] || return 1
    local l; while IFS= read -r l; do l=${l#*\"}; SAVED_LIST+=("${l%\"}"); done < <(security list-keychains -d user)
    [ ${#SAVED_LIST[@]} -gt 0 ] || return 1
    security list-keychains -d user -s "${SAVED_LIST[@]}" "$KC"
    local rc=0
    codesign --force --sign "$id" --options runtime --entitlements "$1" "$APP" || rc=1
    restore_list
    return $rc
}

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
    trap 'restore_list; rm -f "$ENT"' EXIT
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
    # Ad hoc still gives the camera; only the grants go stale on the next re-sign.
    if ! sign_with_identity "$ENT"; then
        echo "could not sign with the local certificate; signing ad hoc (macOS will forget the game's permissions on every re-patch)" >&2
        codesign --force --sign - --options runtime --entitlements "$ENT" "$APP" ||
            { cp -p "$BK/Info.plist" "$PL"; echo "re-signing failed; Info.plist restored" >&2; exit 1; }
    fi
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
