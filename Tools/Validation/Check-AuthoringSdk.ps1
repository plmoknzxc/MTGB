param([string]$EditorRoot = 'D:/WorkSpace/MTGB-DataEditor')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach ($pair in @(
    @('CardDatabases/schema.sql', 'schema.sql'),
    @('Docs/LuaSDK.zh-CN.md', 'Docs/LuaSDK.zh-CN.md')
)) {
    $runtimeText = [IO.File]::ReadAllText((Join-Path $projectRoot $pair[0])).Replace("`r`n", "`n")
    $editorText = [IO.File]::ReadAllText((Join-Path $EditorRoot $pair[1])).Replace("`r`n", "`n")
    if ($runtimeText -cne $editorText) { throw "Authoring contract differs: $($pair[0])" }
}
$runtime = [IO.File]::ReadAllText((Join-Path $projectRoot 'Assets/Scripts/Scripting/LuaCSharpCardScriptRuntime.cs'))
$apiVersion = [regex]::Match($runtime, 'SupportedApiVersion\s*=\s*(\d+)').Groups[1].Value
$template = [IO.File]::ReadAllText((Join-Path $EditorRoot 'MainForm.cs'))
if (-not $apiVersion -or $template -notmatch "ApiVersion = $apiVersion\b") { throw 'Default Lua template version differs from runtime.' }
$runtimeDb = [IO.File]::ReadAllText((Join-Path $projectRoot 'Assets/Scripts/Data/CardDatabaseRepository.cs'))
$editorDb = [IO.File]::ReadAllText((Join-Path $EditorRoot 'CardDatabaseService.cs'))
$schema = [regex]::Match($runtimeDb, 'SupportedSchemaVersion\s*=\s*(\d+)').Groups[1].Value
if (-not $schema -or $editorDb -notmatch "SupportedSchemaVersion\s*=\s*$schema\b") { throw 'Database schema versions differ.' }
Write-Output "Authoring SDK synchronized: schema $schema, Lua API $apiVersion, schema.sql and Lua SDK documentation."
