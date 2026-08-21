# PostToolUse (Edit/Write). Fast per-file check: build the touched .NET project,
# or type-check the touched Angular project. Direct dotnet/tsc calls on purpose --
# this is the fast per-save loop, distinct from `tpx verify --affected` at Stop.
$inputJson = [Console]::In.ReadToEnd() | ConvertFrom-Json
$filePath = $inputJson.tool_input.file_path
if (-not $filePath) { exit 0 }

function Find-Ancestor {
    param([string]$StartPath, [string]$Pattern)
    $dir = Split-Path -Parent $StartPath
    while ($dir -and (Test-Path $dir)) {
        if (Get-ChildItem -Path $dir -Filter $Pattern -File -ErrorAction SilentlyContinue) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

if ($filePath -match '\.cs$') {
    $projDir = Find-Ancestor -StartPath $filePath -Pattern '*.csproj'
    if ($projDir) {
        Push-Location $projDir
        dotnet build --nologo -v quiet
        $exitCode = $LASTEXITCODE
        Pop-Location
        if ($exitCode -ne 0) { exit 2 }
    }
}
elseif ($filePath -match '\.ts$') {
    $projDir = Find-Ancestor -StartPath $filePath -Pattern 'tsconfig.json'
    if ($projDir) {
        Push-Location $projDir
        npx tsc --noEmit
        $exitCode = $LASTEXITCODE
        Pop-Location
        if ($exitCode -ne 0) { exit 2 }
    }
}
exit 0
