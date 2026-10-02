param([switch]$BuildUpstream)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$proof = Join-Path $repo '.tools/forge-proof'
$upstream = Join-Path $proof 'upstream'
$revision = (Get-Content "$repo/Tools/ForgeProof/forge-version.txt" -Raw).Trim()
if ($BuildUpstream -or !(Test-Path "$proof/runtime/bridge-config.json")) {
    & "$repo/Tools/ForgeProof/Build-Bridge.ps1"
    if ($LASTEXITCODE) { throw 'Forge prerequisites failed.' }
}
if ((git -C $upstream rev-parse HEAD).Trim() -ne $revision) { throw 'Forge revision mismatch.' }
& python -X utf8 "$PSScriptRoot/Audit-EffectMarkerCoverage.py"
if ($LASTEXITCODE) { throw 'Forge effect marker storage changed; review and extend marker coverage before packaging.' }
$runtime = Join-Path $repo 'ForgeRuntime'
New-Item -ItemType Directory -Force "$runtime/classes", "$runtime/lib" | Out-Null
Get-ChildItem -LiteralPath "$runtime/lib" -Filter '*.jar' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
Get-ChildItem "$proof/runtime/lib/*.jar" | Copy-Item -Destination "$runtime/lib" -Force
$sources = @(Get-ChildItem "$PSScriptRoot/java/mtgb/bridge/*.java" | ForEach-Object FullName)
& javac -encoding UTF-8 -cp "$runtime/lib/*" -d "$runtime/classes" @sources
if ($LASTEXITCODE) { throw 'Forge bridge compilation failed.' }
$config = Get-Content "$proof/runtime/bridge-config.json" -Raw | ConvertFrom-Json
Push-Location "$upstream/forge-gui"
try {
    & $config.java '-Djava.awt.headless=true' -cp "$runtime/classes;$runtime/lib/*" mtgb.bridge.ForgeCatalog "$upstream/forge-gui" "$runtime/catalog.jsonl" 2> "$runtime/catalog-build.log"
    if ($LASTEXITCODE) { throw "Catalog export failed; see $runtime/catalog-build.log" }
} finally { Pop-Location }
$hash = (Get-FileHash "$runtime/catalog.jsonl" -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeFiles = @(Get-ChildItem "$runtime/classes", "$runtime/lib" -File -Recurse | Sort-Object FullName)
$hashLines = @($runtimeFiles | ForEach-Object { $_.FullName.Substring($runtime.Length).Replace('\','/') + ':' + (Get-FileHash $_.FullName -Algorithm SHA256).Hash })
$runtimeHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashLines -join "`n")))).ToLowerInvariant()
@{java=$config.java; assets="$upstream/forge-gui"; revision=$revision; protocol=1; catalogHash=$hash; runtimeHash=$runtimeHash} |
    ConvertTo-Json | Set-Content "$runtime/runtime.json" -Encoding utf8NoBOM
Write-Output "Forge runtime built: $runtime"
& python -X utf8 "$PSScriptRoot/Package-Runtime.py"
if ($LASTEXITCODE) { throw 'Portable Forge runtime packaging failed.' }
