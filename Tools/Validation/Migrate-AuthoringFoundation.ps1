param(
    [string]$EditorAssembly = 'D:/project/MTGB/Logs/stage1-editor-build/MTGB.CardDatabaseEditor.dll'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Join-Path $projectRoot 'CardDatabases/core.mtgbdb'
$stamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')
$backup = Join-Path $projectRoot "Logs/core-before-foundation-$stamp.mtgbdb"
$staged = Join-Path $projectRoot "Logs/core-foundation-$stamp.mtgbdb"
$originalHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
Copy-Item -LiteralPath $source -Destination $backup
Copy-Item -LiteralPath $source -Destination $staged
$assembly = [Reflection.Assembly]::LoadFrom($EditorAssembly)
$serviceType = $assembly.GetType('MTGB.CardDatabaseEditor.CardDatabaseService')
$kindType = $assembly.GetType('MTGB.CardDatabaseEditor.CardContentKind')
if ($serviceType.GetField('SupportedSchemaVersion').GetRawConstantValue() -ne 8) { throw 'Expected Data Editor schema 8.' }
$service = [Activator]::CreateInstance($serviceType, [object[]]@([string]$staged))
try {
    $summaries = @($service.LoadSummaries())
    foreach ($summary in $summaries) {
        $card = $service.LoadCard($summary.CardKey)
        # Explicit classification of the shipped basic-land samples, never based on database filename.
        if ($card.CardKey -in @('MTGB/1','MTGB/2','MTGB/3','MTGB/4','MTGB/5','MTGB/6')) {
            $card.ContentKind = [Enum]::ToObject($kindType, 0)
        }
        $service.Save($card)
        $first = $service.LoadCard($card.CardKey)
        $options = [System.Text.Json.JsonSerializerOptions]::new()
        $before = [System.Text.Json.JsonSerializer]::Serialize($first, $first.GetType(), $options)
        $service.Save($first)
        $second = $service.LoadCard($card.CardKey)
        $after = [System.Text.Json.JsonSerializer]::Serialize($second, $second.GetType(), $options)
        if ($before -cne $after) { throw "Save/reload is not stable: $($card.CardKey)" }
    }
    $validation = $service.ValidateDatabase()
    if (-not $validation.IsValid -or $validation.CardCount -ne $summaries.Count) {
        throw "Data Editor validation failed: $($validation.Errors -join '; ')"
    }
    Write-Output "Data Editor validation passed: schema=$($validation.SchemaVersion), cards=$($validation.CardCount), all records saved/reloaded twice."
} finally { $service.Dispose() }
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $originalHash) {
    throw "Source changed during migration; staged file retained at $staged."
}
Copy-Item -LiteralPath $staged -Destination $source
Write-Output "Backup: $backup"
Write-Output 'Next required check: unity command mtgb_validate_project --timeout 180'
