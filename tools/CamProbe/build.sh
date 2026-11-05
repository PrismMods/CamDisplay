#!/bin/bash
# Build the CamProbe dev mod (UMM); --install copies it into <game>/UMMMods/CamProbe/ with a fresh log.
set -euo pipefail
cd "$(dirname "$0")"
G=${ADOFAI_ROOT:-"$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"}
M="$G/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed"
mcs -target:library -out:CamProbe.dll -nostdlib -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/netstandard.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.AudioModule.dll" -r:"$M/UnityEngine.IMGUIModule.dll" -r:"$G/UserLibs/UnityModManager.dll" CamProbe.cs
if [ "${1:-}" = "--install" ]; then
    mkdir -p "$G/UMMMods/CamProbe"
    cp CamProbe.dll Info.json "$G/UMMMods/CamProbe/"
    rm -f "$G/UMMMods/CamProbe/probe.log"
    echo "installed to $G/UMMMods/CamProbe"
fi
