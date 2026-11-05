#!/bin/bash
# Build and package CamDisplay for release.
#   ./release.sh            check + package the current version
#   ./release.sh <ver>      also stamp <ver> into AssemblyInfo, MelonInfo and Info.json
# Produces CamDisplay.zip (MelonLoader) and CamDisplayUmm.zip (UMM).
# Ships the latest PUBLISHED PrismLib (lib/update-prismlib.sh): release PrismLib first. For a
# packaging check against the local PrismLib build, run with CAMDISPLAY_LOCAL_PRISMLIB=1.
set -euo pipefail
cd "$(dirname "$0")"
G=${ADOFAI_ROOT:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"}
VER=${1:-}

if [ -n "$VER" ]; then
    NUM=$(sed -E 's/^([0-9]+(\.[0-9]+)*).*/\1/' <<<"$VER")
    while [ "$(tr -cd . <<<"$NUM" | wc -c | tr -d ' ')" -lt 3 ]; do NUM="$NUM.0"; done
    sed -i '' -E "s/Assembly(File)?Version\(\"[^\"]*\"\)/Assembly\1Version(\"$NUM\")/" CamDisplay/Properties/AssemblyInfo.cs
    sed -i '' -E "s/(MelonInfo\(typeof\(CamDisplay.LoaderMl\), \"CamDisplay\", )\"[^\"]*\"/\1\"$VER\"/" CamDisplay/LoaderMl.cs
    jq --arg v "$VER" '.Version = $v' Info.json > Info.json.tmp && mv Info.json.tmp Info.json
    echo "stamped $VER"
fi

[ "${CAMDISPLAY_LOCAL_PRISMLIB:-0}" = 1 ] || lib/update-prismlib.sh
bash native/launch/build.sh
bash native/launch/test.sh
bash tools/test.sh
bash tools/test-mac-camera-patch.sh

for T in ML UMM; do
    xbuild CamDisplay/CamDisplay.csproj /p:Configuration=Release /p:LoaderTarget=$T /p:GameRoot="$G" /verbosity:quiet > /dev/null
done
ML=CamDisplay/bin/Release/CamDisplay.dll
UMM=CamDisplay/bin/umm/Release/CamDisplayUmm.dll
../PrismLib/tools/check-bridge.sh "$PWD/$UMM" CamDisplay.PrismBridge Init Tick OpenLayoutEditor Shutdown CamTarget ToggleSettings RefreshSettings
for d in "$ML" "$UMM"; do
    if strings "$d" | grep -q 'SELFTEST'; then echo "ERROR: $d contains the DEBUG self-test" >&2; exit 1; fi
done

rm -rf build/stage CamDisplay.zip CamDisplayUmm.zip
mkdir -p build/stage/ml/Mods build/stage/ml/UserLibs build/stage/ml/UserData/CamDisplay build/stage/umm/CamDisplay
cp "$ML" build/stage/ml/Mods/CamDisplay.dll
cp lib/PrismLib.UI.dll build/stage/ml/UserLibs/
cp lib/PrismLib.dll build/stage/ml/UserData/CamDisplay/PrismLib.bundle   # never a .dll (PrismLib/CLAUDE.md)
cp "$UMM" Info.json lib/PrismLib.UI.dll build/stage/umm/CamDisplay/
cp lib/PrismLib.dll build/stage/umm/CamDisplay/PrismLib.bundle
(cd build/stage/ml && zip -qr ../../../CamDisplay.zip .)
(cd build/stage/umm && zip -qr ../../../CamDisplayUmm.zip .)
echo "built CamDisplay.zip and CamDisplayUmm.zip ($(jq -r .Version Info.json))"
