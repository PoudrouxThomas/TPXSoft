#!/bin/bash
# SessionStart hook. Installs tools/tpx as a real dotnet global tool so `tpx`
# resolves for this session's own tool calls AND for anything that doesn't go
# through Claude Code's per-session env plumbing -- a subagent's own subprocess,
# or the remote /schedule routine's own subprocess. A locally-built binary
# pushed onto PATH only via $CLAUDE_ENV_FILE didn't reach those (that was the
# bug this replaces -- PLAN.md §0.5). Global install is machine-wide and
# persistent, at the cost of `tpx worktree new` parallel agents sharing one
# global install (last SessionStart to run wins) -- accepted tradeoff, revisit
# if that collision is ever observed in practice.
set -uo pipefail

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(pwd)}"
TPX_PROJ="$PROJECT_DIR/tools/tpx"
NUPKG_DIR="$TPX_PROJ/.nupkg"
TOOLS_DIR="${DOTNET_TOOLS_DIR:-$HOME/.dotnet/tools}"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "session-start: dotnet not found on PATH, skipping tpx install" >&2
    exit 0
fi

if ! dotnet pack "$TPX_PROJ" -o "$NUPKG_DIR" --nologo -v quiet >&2; then
    echo "session-start: tpx pack failed, leaving tpx off PATH" >&2
    exit 0
fi

if ! dotnet tool update --global --add-source "$NUPKG_DIR" TPXSoft.Tpx >&2; then
    echo "session-start: tpx global tool install failed, leaving tpx off PATH" >&2
    exit 0
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    {
        echo "export PATH=\"$TOOLS_DIR:\$PATH\""
        # No .NET 9 runtime is guaranteed present (e.g. SDK 10-only images) --
        # verified recipe from PLAN.md §0.5.
        echo 'export DOTNET_ROLL_FORWARD=Major'
    } >> "$CLAUDE_ENV_FILE"
fi

exit 0
