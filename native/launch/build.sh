#!/bin/bash
# Universal, ad-hoc-signed CamDisplayLaunch (arm64 needs a signature to run at all).
set -euo pipefail
cd "$(dirname "$0")"
clang -arch arm64 -arch x86_64 -O2 -Wall -o CamDisplayLaunch launch.c
codesign --force --sign - CamDisplayLaunch
