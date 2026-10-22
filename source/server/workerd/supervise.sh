#!/bin/sh
# Runs the relay in workerd and starts a fresh workerd once the relay has sat idle. workerd's allocator (tcmalloc,
# built without its background release thread) keeps the memory of the busiest moment for reuse and never hands it
# back, so after a 300 person evening an idle relay would stay near 220 MB instead of 20 MB. With no client
# connected there is nothing to lose: room state lives in the sockets, and alarms and Jam queues are on disk.
# IDLE_RESTART_MINUTES (default 15, 0 turns it off) is how long the relay must have had no open connection, and
# only a workerd above RESTART_ABOVE_KB of resident memory is restarted. If workerd exits on its own (a crash or
# the container's memory limit), this script exits too and Docker's restart policy takes over.
set -u

idle_minutes="${IDLE_RESTART_MINUTES:-15}"
RESTART_ABOVE_KB=153600
child=0

stop() {
    kill -TERM "$child" 2>/dev/null
    wait "$child"
    exit 0
}
trap stop TERM INT

connections() {
    cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | grep -cE '^ *[0-9]+: [0-9A-F]+:2253 [0-9A-F]+:[0-9A-F]+ 01 '
}

resident_kb() {
    sed -n 's/^VmRSS:[[:space:]]*\([0-9]*\) kB$/\1/p' "/proc/$1/status" 2>/dev/null
}

while true; do
    workerd serve workerd/relay.capnp &
    child=$!
    idle_seconds=0
    while true; do
        sleep 5 &
        wait $!
        if ! kill -0 "$child" 2>/dev/null; then
            wait "$child"
            exit $?
        fi

        if [ "$(connections)" -gt 0 ]; then
            idle_seconds=0
            continue
        fi

        idle_seconds=$((idle_seconds + 5))
        if [ "$idle_minutes" -eq 0 ] || [ "$idle_seconds" -lt $((idle_minutes * 60)) ]; then
            continue
        fi

        resident=$(resident_kb "$child")
        if [ "${resident:-0}" -gt "$RESTART_ABOVE_KB" ]; then
            echo "relay idle for $idle_minutes minutes at $((resident / 1024)) MB, starting a fresh workerd"
            kill -TERM "$child"
            wait "$child"
            break
        fi
    done
done
