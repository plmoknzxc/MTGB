param([switch]$BuildUpstream)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$proof = Join-Path $repo '.tools/forge-proof'
$upstream = Join-Path $proof 'upstream'
$revision = (Get-Content "$repo/Tools/ForgeProof/forge-version.txt" -Raw).Trim()
$proofConfigPath = Join-Path $proof 'runtime/bridge-config.json'
$proofRevision = if (Test-Path -LiteralPath $proofConfigPath) {
    (Get-Content -LiteralPath $proofConfigPath -Raw | ConvertFrom-Json).revision
}
if ($BuildUpstream -or $proofRevision -ne $revision) {
    & "$repo/Tools/ForgeProof/Build-Bridge.ps1"
    if ($LASTEXITCODE) { throw 'Forge prerequisites failed.' }
}
if ((git -C $upstream rev-parse HEAD).Trim() -ne $revision) { throw 'Forge revision mismatch.' }
& python -X utf8 "$PSScriptRoot/Audit-InteractionContract.py"
if ($LASTEXITCODE) { throw 'Forge GUI contract changed or a reviewed handler is missing. Inspect the interaction contract report.' }
& python -X utf8 "$PSScriptRoot/Audit-EffectMarkerCoverage.py"
if ($LASTEXITCODE) { throw 'Forge effect marker storage changed; review and extend marker coverage before packaging.' }
$runtime = Join-Path $repo 'ForgeRuntime'
New-Item -ItemType Directory -Force "$runtime/classes", "$runtime/lib" | Out-Null
# Recompiling a renamed inner class does not remove the old .class automatically.
# Only the bridge package is rebuilt here; dependency classes live in the jars.
if (Test-Path -LiteralPath "$runtime/classes/mtgb/bridge") {
    Get-ChildItem -LiteralPath "$runtime/classes/mtgb/bridge" -Filter '*.class' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
}
Get-ChildItem -LiteralPath "$runtime/lib" -Filter '*.jar' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
Get-ChildItem "$proof/runtime/lib/*.jar" | Copy-Item -Destination "$runtime/lib" -Force
& python -X utf8 "$PSScriptRoot/Apply-NativePatches.py"
if ($LASTEXITCODE) { throw 'Reviewed native Forge fix failed to apply or compile.' }
$sources = @(Get-ChildItem "$PSScriptRoot/java/mtgb/bridge/*.java" | ForEach-Object FullName)
& javac -encoding UTF-8 -cp "$runtime/classes;$runtime/lib/*" -d "$runtime/classes" @sources
if ($LASTEXITCODE) { throw 'Forge bridge compilation failed.' }
& python -X utf8 "$PSScriptRoot/Package-Runtime.py"
if ($LASTEXITCODE) { throw 'Portable Forge runtime packaging failed.' }
& python -X utf8 "$PSScriptRoot/Build-DeckEditorRules.py"
if ($LASTEXITCODE) { throw 'Editor construction facet export failed.' }
