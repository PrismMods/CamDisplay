#!/bin/bash
# Universal, ad-hoc-signed CamDisplayLaunch (arm64 needs a signature to run at all).
set -euo pipefail
cd "$(dirname "$0")"
clang -arch arm64 -arch x86_64 -mmacosx-version-min=12.0 -O2 -Wall -o CamDisplayLaunch launch.c   # the game's own floor
codesign --force --sign - CamDisplayLaunch
