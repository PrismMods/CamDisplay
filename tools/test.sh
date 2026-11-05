#!/bin/bash
# Offline tests for the pure core (no Unity): compiles the Core sources straight into the test.
set -euo pipefail
cd "$(dirname "$0")/.."
mcs -out:/tmp/camdisplaytest.exe tools/CamDisplayTest.cs CamDisplay/Core/*.cs
mono /tmp/camdisplaytest.exe
