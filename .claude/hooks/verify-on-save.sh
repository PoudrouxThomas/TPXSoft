#!/usr/bin/env bash
# PostToolUse (Edit/Write). Fast per-file check: build the touched .NET project,
# or type-check the touched Angular project. Direct dotnet/tsc calls on purpose --
# this is the fast per-save loop, distinct from `tpx verify --affected` at Stop.
set -uo pipefail

input_json="$(cat)"
file_path="$(printf '%s' "$input_json" | jq -r '.tool_input.file_path // empty')"
[ -z "$file_path" ] && exit 0

find_ancestor() {
    local dir pattern
    dir="$(dirname -- "$1")"
    pattern="$2"
    while [ -n "$dir" ] && [ -d "$dir" ]; do
        if compgen -G "$dir/$pattern" > /dev/null 2>&1; then
            printf '%s' "$dir"
            return 0
        fi
        local parent
        parent="$(dirname -- "$dir")"
        [ "$parent" = "$dir" ] && break
        dir="$parent"
    done
    return 1
}

case "$file_path" in
    *.cs)
        if proj_dir="$(find_ancestor "$file_path" '*.csproj')"; then
            (cd "$proj_dir" && dotnet build --nologo -v quiet)
            exit_code=$?
            [ "$exit_code" -ne 0 ] && exit 2
        fi
        ;;
    *.ts)
        if proj_dir="$(find_ancestor "$file_path" 'tsconfig.json')"; then
            (cd "$proj_dir" && npx tsc --noEmit)
            exit_code=$?
            [ "$exit_code" -ne 0 ] && exit 2
        fi
        ;;
esac
exit 0
