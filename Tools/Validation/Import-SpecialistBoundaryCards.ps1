param([string]$EditorAssembly = 'D:/WorkSpace/MTGB-DataEditor/bin/Debug/net8.0-windows/MTGB.CardDatabaseEditor.dll')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$databasePath = Join-Path $projectRoot 'CardDatabases/core.mtgbdb'
$assembly = [Reflection.Assembly]::LoadFrom($EditorAssembly)
function New-EditorRecord([string]$name) {
    return [Activator]::CreateInstance($assembly.GetType("MTGB.CardDatabaseEditor.$name"))
}
function New-Feature([string]$name, [int]$types, [string]$cost, [string]$text, [int]$power = 0, [int]$toughness = 0) {
    $f = New-EditorRecord 'CardFeatureRecord'
    $f.Name=$name; $f.Types=$types; $f.ManaCost=$cost
    $f.Power=$power; $f.Toughness=$toughness
    $f.PowerExpression=[string]$power; $f.ToughnessExpression=[string]$toughness
    if ($text) {
        $r=New-EditorRecord 'RuleEntryRecord'; $r.Text=$text; $f.RuleEntries.Add($r)
    }
    return $f
}
$serviceType = $assembly.GetType('MTGB.CardDatabaseEditor.CardDatabaseService')
if ($serviceType.GetField('SupportedSchemaVersion').GetRawConstantValue() -ne 8) { throw 'Expected Data Editor schema v8.' }
$sqliteType = $assembly.GetType('MTGB.CardDatabaseEditor.NativeSqlite')
# Reject legacy input before constructing the editor service (which can migrate it).
foreach ($file in Get-ChildItem (Join-Path $projectRoot 'CardDatabases') -Filter '*.mtgbdb') {
    $raw=$sqliteType.GetMethod('Open').Invoke($null,[object[]]@([string]$file.FullName,$false))
    try {
        if ($raw.ScalarString("SELECT value FROM metadata WHERE key='schema_version';", [object[]]@()) -ne '7') {
            throw "Unsupported input schema: $($file.FullName). No automatic legacy migration is allowed."
        }
    } finally { $raw.Dispose() }
}
$stamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')
$backup = Join-Path $projectRoot "Logs/core-before-specialist-$stamp.mtgbdb"
$staging = Join-Path $projectRoot "Logs/core-specialist-$stamp.mtgbdb"
Copy-Item -LiteralPath $databasePath -Destination $backup
Copy-Item -LiteralPath $databasePath -Destination $staging
$maxId = 0
foreach ($file in Get-ChildItem (Join-Path $projectRoot 'CardDatabases') -Filter '*.mtgbdb') {
    $reader=[Activator]::CreateInstance($serviceType,[object[]]@($file.FullName))
    try { foreach ($summary in $reader.LoadSummaries()) { $maxId=[Math]::Max($maxId,$summary.CardId) } }
    finally { $reader.Dispose() }
}
$service=[Activator]::CreateInstance($serviceType,[object[]]@([string]$staging))
try {
    $features=@(
        (New-Feature '祭品冲击' 16 '{R}' '作为施放此咒语的额外费用，牺牲一个永久物。祭品冲击对目标生物造成3点伤害。'),
        (New-Feature '血价火花' 16 '{R}' '你可以支付2点生命，作为施放此咒语的额外费用。血价火花对目标生物造成1点伤害。若支付了该额外费用，则改为造成3点伤害。'),
        (New-Feature '离墓结界' 8 '{1}{W}' '若一个生物将从战场置入坟墓场，则改为将它放逐。'),
        (New-Feature '倍伤熔炉' 8 '{2}{R}' '若一个来源将造成伤害，则改为造成两倍的伤害。'),
        (New-Feature '协同旗帜' 8 '{1}{W}' '由你操控的生物得+1/+1。'),
        (New-Feature '基准重塑' 16 '{U}' '直到回合结束，目标生物的基础力量与防御力成为2/3。'),
        (New-Feature '归途旅人' 4 '{2}{U}' '' 3 3),
        (New-Feature '试炼纪事' 8 '{1}{W}' '')
    )
    for ($i=0; $i -lt $features.Count; $i++) {
        $number=14+$i; $key="TST/$number"
        $existing=$service.LoadSummaries() | Where-Object CardKey -eq $key
        if ($existing) { throw "Refusing to overwrite existing $key; inspect it before re-importing." }
        $card=New-EditorRecord 'CardRecord'; $maxId++; $card.CardId=$maxId
        $card.SetCode='TST'; $card.CollectorNumber=[string]$number
        $card.ScriptPath="CardScripts/TST/m$number.lua"; $card.Features.Add($features[$i])
        if ($number -eq 20) {
            $adventure=New-Feature '踏返旧路' 16 '{U}' '将目标生物移回其拥有者手上。'
            $adventure.FeatureIndex=1; $adventure.Role=2; $adventure.Subtypes='Adventure'
            $card.Features.Add($adventure)
        }
        if ($number -eq 21) {
            $features[$i].Subtypes='Saga'
            $texts=@('你获得2点生命。','抓一张牌。','你获得3点生命。')
            for ($chapter=1; $chapter -le 3; $chapter++) {
                $entry=New-EditorRecord 'RuleEntryRecord'; $entry.Order=$chapter-1; $entry.Kind=1
                $entry.Chapters.Add($chapter); $entry.Text=$texts[$chapter-1]
                $features[$i].RuleEntries.Add($entry)
            }
        }
        $service.Save($card)
        # Round-trip each special structure through the actual editor normalizer.
        $restored=$service.LoadCard($key); $before=$restored | ConvertTo-Json -Depth 20 -Compress
        $service.Save($restored)
        $after=$service.LoadCard($key) | ConvertTo-Json -Depth 20 -Compress
        if ($before -ne $after) { throw "$key changed on the second editor save." }
        Write-Output "$key id=$($card.CardId) $($card.Name): editor save/reload stable"
    }
    $validation=$service.ValidateDatabase()
    if (-not $validation.IsValid) { throw ($validation.Errors -join "`n") }
} finally { $service.Dispose() }
Copy-Item -LiteralPath $staging -Destination $databasePath -Force
Write-Output "Imported 8 cards. Recoverable backup: $backup"
