# PreToolUse (Edit/Write). Blocks edits under **/clients/** or **/generated/**.
# Contract-first rule: change contracts/<module>.vN.yaml and run `tpx gen` instead.
$inputJson = [Console]::In.ReadToEnd() | ConvertFrom-Json
$filePath = $inputJson.tool_input.file_path
if (-not $filePath) { exit 0 }

$normalized = $filePath -replace '\\', '/'
if ($normalized -match '(^|/)clients/' -or $normalized -match '(^|/)generated/') {
    [Console]::Error.WriteLine("Blocked: '$filePath' is generated. Edit the contract in contracts/ and run 'tpx gen' instead of hand-editing generated output.")
    exit 2
}
exit 0
