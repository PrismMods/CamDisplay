#!/bin/bash
# Build (Debug) and install CamDisplay into the local game for testing.
#   ./deploy.sh [ML|UMM]   default: ML when MelonLoader is installed, else UMM
# Never leaves both installed: the other loader's copy is removed.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p build
G=${ADOFAI_ROOT:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"}
T=${1:-}
[ -z "$T" ] && { [ -f "$G/MelonLoader/net35/MelonLoader.dll" ] && T=ML || T=UMM; }
[ -f lib/PrismLib.dll ] && [ -f lib/PrismLib.UI.dll ] && [ -f lib/Facet.0.1.dll ] || lib/update-prismlib.sh
[ -f lib/Facet.0.1.dll ] || { echo "lib/Facet.0.1.dll is missing. Get it with PrismLib's tools/update-facet.sh (dev machine) or from the next PrismLib release (then lib/update-prismlib.sh)." >&2; exit 1; }
[ -f native/launch/CamDisplayLaunch ] || native/launch/build.sh   # embedded into the mod
# Quiet on success; on failure, show the compiler errors instead of exiting silently.
xbuild CamDisplay/CamDisplay.csproj /p:LoaderTarget="$T" /p:GameRoot="$G" /verbosity:quiet > build/deploy-xbuild.log 2>&1 \
    || { grep -E ': error ' build/deploy-xbuild.log >&2; echo "build failed; nothing deployed" >&2; exit 1; }

if [ "$T" = ML ]; then
    rm -rf "$G/UMMMods/CamDisplay"
    mkdir -p "$G/Mods" "$G/UserLibs" "$G/UserData/CamDisplay"
    cp CamDisplay/bin/Debug/CamDisplay.dll "$G/Mods/CamDisplay.dll"
    cp lib/PrismLib.UI.dll lib/Facet.0.1.dll "$G/UserLibs/"
    cp lib/PrismLib.dll "$G/UserData/CamDisplay/PrismLib.bundle"   # never a .dll: see PrismLib/CLAUDE.md
    cmp -s CamDisplay/bin/Debug/CamDisplay.dll "$G/Mods/CamDisplay.dll"
    echo "Deployed ML build to $G/Mods/CamDisplay.dll"
else
    rm -f "$G/Mods/CamDisplay.dll"
    D="$G/UMMMods/CamDisplay"
    mkdir -p "$D"
    cp CamDisplay/bin/umm/Debug/CamDisplayUmm.dll Info.json lib/PrismLib.UI.dll lib/Facet.0.1.dll "$D/"
    cp lib/PrismLib.dll "$D/PrismLib.bundle"
    cmp -s CamDisplay/bin/umm/Debug/CamDisplayUmm.dll "$D/CamDisplayUmm.dll"
    echo "Deployed UMM build to $D"
fi
