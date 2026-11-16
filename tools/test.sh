#!/bin/bash
# Offline tests for the pure core (no Unity): compiles the Core sources straight into the test.
# Also fails when CamDisplay/Util's vendored PrismLib bootstrap sources drift from PrismLib's
# (PRISMLIB_ROOT, default ../PrismLib-suite; skipped when that checkout is absent).
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="${PRISMLIB_ROOT:-../PrismLib-suite}"
if [ -x "$ROOT/tools/check-vendored.sh" ]; then
    "$ROOT/tools/check-vendored.sh" CamDisplay/Util/PrismBootstrap.cs CamDisplay/Util/PrismGate.cs \
        || { echo "FAIL: copy $ROOT/bootstrap/ over the STALE CamDisplay/Util files" >&2; exit 1; }
fi
mcs -out:/tmp/camdisplaytest.exe tools/CamDisplayTest.cs CamDisplay/Core/*.cs
mono /tmp/camdisplaytest.exe
