#!/bin/bash
# Offline check for mac-camera-patch.sh on a throwaway, ad-hoc-signed app bundle. Never touches
# the real game: the backup dir and the "stock signature" pattern are pointed at test values.
set -u
cd "$(dirname "$0")"
P=./mac-camera-patch.sh
T=$(mktemp -d)
trap 'rm -rf "$T"' EXIT
fail=0
check() { if [ "$1" = "$2" ]; then echo "  ok   $3"; else echo "  FAIL $3 (got '$1', want '$2')"; fail=1; fi; }
export CAMPATCH_BACKUP="$T/backup" CAMPATCH_STOCK_RE='^Signature=adhoc'

APP="$T/Toy.app"
fresh() {   # a "stock" toy app: hardened runtime, one entitlement, no camera
    rm -rf "$APP" "$CAMPATCH_BACKUP"; mkdir -p "$APP/Contents/MacOS"
    printf 'int main(void){return 0;}\n' > "$T/m.c"; clang -o "$APP/Contents/MacOS/Toy" "$T/m.c"
    plutil -create xml1 "$APP/Contents/Info.plist"
    plutil -insert CFBundleExecutable -string Toy "$APP/Contents/Info.plist"
    plutil -insert CFBundleIdentifier -string com.prism.toy "$APP/Contents/Info.plist"
    plutil -insert CFBundleVersion -string 25 "$APP/Contents/Info.plist"
    /usr/libexec/PlistBuddy -c 'Add :com.apple.security.cs.disable-library-validation bool true' "$T/e.plist" >/dev/null 2>&1 || true
    codesign --force --sign - --options runtime --entitlements "$T/e.plist" "$APP" 2>/dev/null
}
ents() { codesign -d --entitlements - --xml "$APP" 2>/dev/null; }

fresh
check "$($P status "$APP")" "stock" "a fresh app reads as stock"
$P apply "$APP" >/dev/null 2>&1
check "$($P status "$APP")" "patched" "apply patches it"
e=$(ents)
check "$(grep -o 'disable-library-validation\|device.camera' <<<"$e" | wc -l | tr -d ' ')" "2" "the stock entitlement is kept and camera added"
check "$(codesign --verify "$APP" 2>&1 && echo valid)" "valid" "the patched app verifies"
$P undo "$APP" >/dev/null 2>&1
check "$($P status "$APP")" "stock" "undo restores the stock app"
check "$(codesign --verify "$APP" 2>&1 && echo valid)" "valid" "the restored app verifies"

# A half-applied patch: Info.plist edited, signature not redone. Its backup must survive.
fresh
$P apply "$APP" >/dev/null 2>&1; $P undo "$APP" >/dev/null 2>&1    # leaves a good backup
plutil -replace NSCameraUsageDescription -string x "$APP/Contents/Info.plist"
$P apply "$APP" >/dev/null 2>&1; rc=$?
check "$([ $rc -ne 0 ] && echo refused)" "refused" "apply refuses a signature that no longer verifies"
check "$(plutil -extract NSCameraUsageDescription raw "$CAMPATCH_BACKUP/Info.plist" >/dev/null 2>&1 && echo dirty || echo clean)" "clean" "and keeps the good backup"

# Entitlements that cannot be read must stop the patch, not sign with an empty set.
fresh
mkdir -p "$T/shim"
printf '#!/bin/bash\n[ "$1" = "-d" ] && [ "$2" = "--entitlements" ] && exit 0   # reading entitlements yields nothing\nexec /usr/bin/codesign "$@"\n' > "$T/shim/codesign"
chmod +x "$T/shim/codesign"
PATH="$T/shim:$PATH" $P apply "$APP" >/dev/null 2>&1; rc=$?
check "$([ $rc -ne 0 ] && echo refused)" "refused" "apply refuses when the entitlements cannot be read"

# Undo on an app that is not patched must not copy a (possibly stale) backup over it.
fresh
$P apply "$APP" >/dev/null 2>&1; $P undo "$APP" >/dev/null 2>&1
printf 'int main(void){return 1;}\n' > "$T/m2.c"; clang -o "$APP/Contents/MacOS/Toy" "$T/m2.c"   # a "game update"
codesign --force --sign - --options runtime --entitlements "$T/e.plist" "$APP" 2>/dev/null
before=$(shasum "$APP/Contents/MacOS/Toy")
$P undo "$APP" >/dev/null 2>&1
check "$(shasum "$APP/Contents/MacOS/Toy")" "$before" "undo leaves an unpatched app alone"

exit $fail
