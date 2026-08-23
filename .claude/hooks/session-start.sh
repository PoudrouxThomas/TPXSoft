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

# The csproj pins Version=0.1.0, so a bare `dotnet pack` always produces the
# same package identity -- `dotnet tool update --global` then sees no new
# version and silently keeps whatever binary was installed first, even after
# source changes. Stamp a monotonically increasing NuGet package version
# (UTC epoch seconds as the 4th part) on every pack, via -p:PackageVersion
# rather than -p:Version -- Version also drives AssemblyVersion, which is
# capped at 65534 per part and rejects an epoch-sized number outright.
# Clear the local feed first so it can't keep picking a stale nupkg left
# over from an earlier session.
TPX_PACKAGE_VERSION="0.1.0.$(date -u +%s)"
rm -rf "$NUPKG_DIR"
if ! dotnet pack "$TPX_PROJ" -o "$NUPKG_DIR" -p:PackageVersion="$TPX_PACKAGE_VERSION" --nologo -v quiet >&2; then
    echo "session-start: tpx pack failed, leaving tpx off PATH" >&2
    exit 0
fi

if ! dotnet tool update --global --add-source "$NUPKG_DIR" TPXSoft.Tpx >&2; then
    echo "session-start: tpx global tool install failed, leaving tpx off PATH" >&2
    exit 0
fi

# `tpx gen` shells out to the `nswag` CLI (modules/*/nswag.json) to regenerate
# shared/clients/csharp from contracts/. Install it the same way as tpx
# itself so it resolves for this session, subagents, and the /schedule
# routine's subprocess. Non-fatal: a failed install just means `tpx gen`
# reports "nswag CLI is not installed" instead of silently working.
if ! dotnet tool update --global NSwag.ConsoleCore >&2; then
    echo "session-start: NSwag.ConsoleCore install failed, 'tpx gen' won't find nswag on PATH" >&2
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    {
        echo "export PATH=\"$TOOLS_DIR:\$PATH\""
        # No .NET 9 runtime is guaranteed present (e.g. SDK 10-only images) --
        # verified recipe from PLAN.md §0.5.
        echo 'export DOTNET_ROLL_FORWARD=Major'
    } >> "$CLAUDE_ENV_FILE"
fi

# .mcp.json launches each module's MCP server as a prebuilt DLL via
# `dotnet exec` (not `dotnet run`, which rebuilds -- and lock-contends with
# `tpx verify` -- on every session start). Build it here so the binary exists
# before the client tries to connect. Non-fatal: a stale/missing binary just
# means that module's MCP tools aren't available this session.
AUTH_MCP_PROJ="$PROJECT_DIR/modules/auth/src/TPXSoft.Auth.Mcp/TPXSoft.Auth.Mcp.csproj"
if [ -f "$AUTH_MCP_PROJ" ]; then
    dotnet build "$AUTH_MCP_PROJ" --nologo -v quiet >&2 || \
        echo "session-start: TPXSoft.Auth.Mcp build failed, tpxsoft-auth MCP server may not connect" >&2
fi

exit 0
