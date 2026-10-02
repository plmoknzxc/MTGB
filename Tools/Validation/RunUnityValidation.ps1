param(
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    throw "Set UNITY_EDITOR_PATH or pass -UnityPath with the full Unity.exe path."
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity editor was not found at '$UnityPath'."
}

$logPath = Join-Path $ProjectPath "Logs/validation.log"
& $UnityPath `
    -batchmode `
    -nographics `
    -projectPath $ProjectPath `
    -executeMethod MTGBValidationRunner.ValidateFromCommandLine `
    -logFile $logPath

if ($LASTEXITCODE -ne 0) {
    throw "MTGB validation failed with exit code $LASTEXITCODE. See '$logPath'."
}
if ((Get-Content -LiteralPath (Join-Path $ProjectPath 'Temp/project-validation-result.txt') -Raw).Trim() -ne 'PASS') {
    throw "Forge validation did not finish successfully. See '$logPath'."
}

Write-Host "MTGB validation passed. Log: $logPath"
