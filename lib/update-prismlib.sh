#!/usr/bin/env bash
# Refresh the PrismLib DLLs this mod builds against, to the latest published release.
#   PrismLib.dll     the shared half. Shipped inside the zip as PrismLib.bundle: PrismBootstrap
#                    installs it into the shared PrismLib folder when it is newer than what is
#                    there, so a first launch needs no network; the feed updates it afterwards.
#   PrismLib.UI.dll  SHIPPED with the mod: the UI half needs no shared instance across mods, so
#                    it is an ordinary dependency and deploy/release copy it into the mod folder.
#   Facet.<n>.dll    the engine PrismLib.UI stands on, shipped beside it. Fetched when the feed
#                    has a "facet" entry; otherwise any copy already here is kept (PrismLib's
#                    tools/update-facet.sh makes one on a dev machine).
set -euo pipefail
cd "$(dirname "$0")"
FEED=$(curl -fsS https://raw.githubusercontent.com/PrismMods/PrismLib/main/prismlib.json)
VER=$(jq -r .version <<<"$FEED")

# Checked against the feed before it replaces anything: this is code that ships to players.
fetch() { # url sha256 file
    curl -fsSLo "$3.tmp" "$1"
    if [ "$(shasum -a 256 "$3.tmp" | cut -d' ' -f1)" != "$2" ]; then
        rm -f "$3.tmp"
        echo "SHA-256 mismatch for $3 (feed $VER); kept the old copy" >&2
        exit 1
    fi
    mv "$3.tmp" "$3"
}
fetch "$(jq -r .url <<<"$FEED")" "$(jq -r .sha256 <<<"$FEED")" PrismLib.dll
fetch "$(jq -r .ui.url <<<"$FEED")" "$(jq -r .ui.sha256 <<<"$FEED")" PrismLib.UI.dll
if [ "$(jq -r '.facet.url // empty' <<<"$FEED")" ]; then
    fetch "$(jq -r .facet.url <<<"$FEED")" "$(jq -r .facet.sha256 <<<"$FEED")" "$(jq -r .facet.name <<<"$FEED")"
    echo "PrismLib + PrismLib.UI + Facet now $VER"
else
    echo "PrismLib + PrismLib.UI now $VER; the feed has no Facet yet (kept any lib/Facet.*.dll)"
fi
