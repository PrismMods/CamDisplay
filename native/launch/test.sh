#!/bin/bash
# Offline check for CamDisplayLaunch: arguments, exit codes, #! scripts and signal forwarding.
set -u; cd "$(dirname "$0")"; L=./CamDisplayLaunch; fail=0
check() { if [ "$1" = "$2" ]; then echo "  ok   $3"; else echo "  FAIL $3 (got '$1', want '$2')"; fail=1; fi; }
check "$($L /bin/echo a 'b c')" "a b c" "passes arguments through unchanged"
$L /bin/sh -c 'exit 7'; check "$?" "7" "returns the child's exit code"
printf '#!/bin/sh\necho script-ran\n' > /tmp/cdl-script.sh; chmod +x /tmp/cdl-script.sh
check "$($L /tmp/cdl-script.sh)" "script-ran" "runs a #! script"
$L /bin/sh -c 'kill -TERM $$'; check "$?" "143" "reports a signalled child as 128+signo"
$L /bin/sleep 30 & lp=$!; sleep 0.5; kill -TERM $lp; wait $lp; check "$?" "143" "forwards SIGTERM to the child"
pgrep -f '^/bin/sleep 30$' >/dev/null && { echo "  FAIL child survived SIGTERM"; fail=1; } || echo "  ok   child gone after SIGTERM"
$L /nonexistent 2>/dev/null; check "$?" "127" "a missing program exits 127"
exit $fail
