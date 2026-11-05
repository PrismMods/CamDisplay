#!/bin/bash
# Offline check for CamDisplayLaunch: arguments, exit codes, #! scripts, signals, same pid.
set -u; cd "$(dirname "$0")"; L=./CamDisplayLaunch; fail=0
check() { if [ "$1" = "$2" ]; then echo "  ok   $3"; else echo "  FAIL $3 (got '$1', want '$2')"; fail=1; fi; }
check "$($L /bin/echo a 'b c')" "a b c" "passes arguments through unchanged"
$L /bin/sh -c 'exit 7'; check "$?" "7" "returns the child's exit code"
printf '#!/bin/sh\necho script-ran\n' > /tmp/cdl-script.sh; chmod +x /tmp/cdl-script.sh
check "$($L /tmp/cdl-script.sh)" "script-ran" "runs a #! script"
$L /bin/sh -c 'kill -TERM $$'; check "$?" "143" "reports a signalled child as 128+signo"
$L /bin/sleep 30 & lp=$!; sleep 0.5; kill -TERM $lp; wait $lp; check "$?" "143" "SIGTERM to the launched pid stops the program"
pgrep -f '^/bin/sleep 30$' >/dev/null && { echo "  FAIL program survived SIGTERM"; fail=1; } || echo "  ok   program gone after SIGTERM"
$L /nonexistent 2>/dev/null; check "$?" "127" "a missing program exits 127"
# Steam tracks (and asks to quit) the pid it started, so the program must BE that pid.
$L /bin/sh -c 'echo $$ > /tmp/cdl-pid' & lp=$!; wait $lp
check "$(cat /tmp/cdl-pid)" "$lp" "the program runs in the launcher's own process"
exit $fail
