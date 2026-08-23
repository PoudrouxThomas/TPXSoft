#!/bin/bash
# SessionStart hook. Builds tools/tpx and puts it on PATH for this session so
# `tpx` is callable immediately -- closes PLAN.md §0.5's second deferred item
# (the first, .NET SDK provisioning, belongs in the environment's own setup
# script and is out of scope here).
set -uo pipefail

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
TPX_PROJ="$PROJECT_DIR/tools/tpx"
TPX_OUT="$TPX_PROJ/bin/Release/net9.0"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "session-start: dotnet not found on PATH, skipping tpx build" >&2
    exit 0
fi

if ! dotnet build -c Release --nologo -v quiet "$TPX_PROJ" >&2; then
    echo "session-start: tpx build failed, leaving tpx off PATH" >&2
    exit 0
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    {
        echo "export PATH=\"$TPX_OUT:\$PATH\""
        # No .NET 9 runtime is guaranteed present (e.g. SDK 10-only images) --
        # verified recipe from PLAN.md §0.5.
        echo 'export DOTNET_ROLL_FORWARD=Major'
    } >> "$CLAUDE_ENV_FILE"
fi

exit 0
