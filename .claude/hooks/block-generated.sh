#!/usr/bin/env bash
# PreToolUse (Edit/Write). Blocks edits under **/clients/** or **/generated/**.
# Contract-first rule: change contracts/<module>.vN.yaml and run `tpx gen` instead.
set -euo pipefail

input_json="$(cat)"
file_path="$(printf '%s' "$input_json" | jq -r '.tool_input.file_path // empty')"
[ -z "$file_path" ] && exit 0

normalized="${file_path//\\//}"
if [[ "$normalized" =~ (^|/)clients/ || "$normalized" =~ (^|/)generated/ ]]; then
    echo "Blocked: '$file_path' is generated. Edit the contract in contracts/ and run 'tpx gen' instead of hand-editing generated output." >&2
    exit 2
fi
exit 0
