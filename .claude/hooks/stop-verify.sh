#!/usr/bin/env bash
# Stop hook. Runs the affected-modules verify pass via the tpx CLI (not raw dotnet/npm --
# tpx is the stable interface every agent/hook/CI calls).
tpx verify --affected
exit $?
