#!/bin/bash
# Build (Debug) and install CamDisplay into the local game for testing.
#   ./deploy.sh [ML|UMM]   default: ML when MelonLoader is installed, else UMM
# Never leaves both installed: the other loader's copy is removed.
set -euo pipefail
cd "$(dirname "$0")"
G=${ADOFAI_ROOT:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"}
T=${1:-}
[ -z "$T" ] && { [ -f "$G/MelonLoader/net35/MelonLoader.dll" ] && T=ML || T=UMM; }
[ -f lib/PrismLib.dll ] && [ -f lib/PrismLib.UI.dll ] || lib/update-prismlib.sh
[ -f native/launch/CamDisplayLaunch ] || native/launch/build.sh   # embedded into the mod
xbuild CamDisplay/CamDisplay.csproj /p:LoaderTarget="$T" /p:GameRoot="$G" /verbosity:quiet > /dev/null

if [ "$T" = ML ]; then
    rm -rf "$G/UMMMods/CamDisplay"
    mkdir -p "$G/Mods" "$G/UserLibs" "$G/UserData/CamDisplay"
    cp CamDisplay/bin/Debug/CamDisplay.dll "$G/Mods/CamDisplay.dll"
    cp lib/PrismLib.UI.dll "$G/UserLibs/PrismLib.UI.dll"
    cp lib/PrismLib.dll "$G/UserData/CamDisplay/PrismLib.bundle"   # never a .dll: see PrismLib/CLAUDE.md
    cmp -s CamDisplay/bin/Debug/CamDisplay.dll "$G/Mods/CamDisplay.dll"
    echo "Deployed ML build to $G/Mods/CamDisplay.dll"
else
    rm -f "$G/Mods/CamDisplay.dll"
    D="$G/UMMMods/CamDisplay"
    mkdir -p "$D"
    cp CamDisplay/bin/umm/Debug/CamDisplayUmm.dll Info.json lib/PrismLib.UI.dll "$D/"
    cp lib/PrismLib.dll "$D/PrismLib.bundle"
    cmp -s CamDisplay/bin/umm/Debug/CamDisplayUmm.dll "$D/CamDisplayUmm.dll"
    echo "Deployed UMM build to $D"
fi
