#!/usr/bin/env bash
# Refresh the PrismLib DLLs this mod builds against, to the latest published release.
#   PrismLib.dll     the shared half. Shipped inside the zip as PrismLib.bundle: PrismBootstrap
#                    installs it into the shared PrismLib folder when it is newer than what is
#                    there, so a first launch needs no network; the feed updates it afterwards.
#   PrismLib.UI.dll  SHIPPED with the mod: the UI half needs no shared instance across mods, so
#                    it is an ordinary dependency and deploy/release copy it into the mod folder.
#   Facet.<n>.dll    the engine PrismLib.UI stands on, shipped beside it. Fetched when the feed
#                    has a "facet" entry; otherwise any copy already here is kept (PrismLib's
#                    ./dev.sh puts one here on a dev machine).
set -euo pipefail
cd "$(dirname "$0")"
FEED=$(curl -fsS https://raw.githubusercontent.com/PrismMods/PrismLib/main/prismlib.json)
VER=$(jq -r .version <<<"$FEED")
FACET=$(jq -r '.facet.name // empty' <<<"$FEED")
# The feed's name becomes a path here: only Facet.<major>.<minor>.dll, never one that leaves lib/.
[ -z "$FACET" ] || [[ "$FACET" =~ ^Facet\.[0-9]+\.[0-9]+\.dll$ ]] || { echo "feed $VER names Facet '$FACET'; refusing" >&2; exit 1; }

# Every file is fetched and checked against the feed before any replaces the old one: this is code
# that ships to players, and a half-updated lib/ would pair one release's PrismLib.UI with
# another's Facet.
trap 'rm -f PrismLib.dll.tmp PrismLib.UI.dll.tmp ${FACET:+"$FACET.tmp"}' EXIT
fetch() { # url sha256 file
    curl -fsSLo "$3.tmp" "$1"
    if [ "$(shasum -a 256 "$3.tmp" | cut -d' ' -f1)" != "$2" ]; then
        echo "SHA-256 mismatch for $3 (feed $VER); kept the old copies" >&2
        exit 1
    fi
}
fetch "$(jq -r .url <<<"$FEED")" "$(jq -r .sha256 <<<"$FEED")" PrismLib.dll
fetch "$(jq -r .ui.url <<<"$FEED")" "$(jq -r .ui.sha256 <<<"$FEED")" PrismLib.UI.dll
[ -z "$FACET" ] || fetch "$(jq -r .facet.url <<<"$FEED")" "$(jq -r .facet.sha256 <<<"$FEED")" "$FACET"
for f in PrismLib.dll PrismLib.UI.dll ${FACET:+"$FACET"}; do mv "$f.tmp" "$f"; done
if [ -n "$FACET" ]; then
    echo "PrismLib + PrismLib.UI + Facet now $VER"
else
    echo "PrismLib + PrismLib.UI now $VER; the feed has no Facet yet (kept any lib/Facet.*.dll)"
fi
