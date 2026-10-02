param([switch]$SkipForgeBuild)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
$work = Join-Path $repo '.tools/forge-proof'
$upstream = Join-Path $work 'upstream'
$revision = (Get-Content "$PSScriptRoot/forge-version.txt" -Raw).Trim()
New-Item -ItemType Directory -Force $work | Out-Null
if (!(Test-Path "$upstream/.git")) {
    git clone --depth 1 https://github.com/Card-Forge/forge.git $upstream
    if ($LASTEXITCODE) { throw 'Forge clone failed.' }
    git -C $upstream fetch --depth 1 origin $revision
    if ($LASTEXITCODE) { throw 'Cannot fetch pinned Forge revision.' }
    git -C $upstream checkout --detach $revision
    if ($LASTEXITCODE) { throw 'Forge checkout failed.' }
}
if ((git -C $upstream rev-parse HEAD).Trim() -ne $revision) { throw 'Forge revision mismatch.' }
if (!$SkipForgeBuild) {
    Push-Location $upstream
    try {
        & mvn -B -pl forge-gui -am clean package dependency:copy-dependencies '-DskipTests' '-Dcheckstyle.skip=true' *> "$work/forge-build.log"
        if ($LASTEXITCODE) { throw "Forge build failed; see $work/forge-build.log" }
    } finally { Pop-Location }
}
$runtime = Join-Path $work 'runtime'
New-Item -ItemType Directory -Force "$runtime/classes", "$runtime/lib" | Out-Null
Get-ChildItem -LiteralPath "$runtime/lib" -Filter '*.jar' -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
Get-ChildItem "$upstream/forge-gui/target/dependency/*.jar" | Copy-Item -Destination "$runtime/lib" -Force
$forgeVersion = ([xml](Get-Content "$upstream/pom.xml" -Raw)).project.version
foreach ($module in @('forge-core','forge-game','forge-ai','forge-gui')) {
    Copy-Item "$upstream/$module/target/$module-$forgeVersion.jar" "$runtime/lib" -Force
}
& javac -encoding UTF-8 -cp "$runtime/lib/*" -d "$runtime/classes" "$PSScriptRoot/java/src/main/java/mtgb/proof/ForgeProof.java"
if ($LASTEXITCODE) { throw 'Bridge compilation failed.' }
# Use the checkout's official resources; user preferences/caches stay in this probe.
@("userDir=$($work.Replace('\','/'))/profile", "cacheDir=$($work.Replace('\','/'))/cache") | Set-Content "$upstream/forge-gui/forge.profile.properties" -Encoding utf8NoBOM
$javaSettings = (& java '-XshowSettings:properties' -version 2>&1 | Out-String)
$javaHomeMatch = [regex]::Match($javaSettings, '(?m)^\s*java.home\s*=\s*(.+)$')
if (!$javaHomeMatch.Success) { throw 'Cannot resolve the actual JVM executable.' }
# Oracle javapath/java.exe is a launcher that spawns another process. Use the JVM
# directly so PID, memory measurements and forced shutdown refer to the real engine.
$java = Join-Path $javaHomeMatch.Groups[1].Value.Trim() 'bin/java.exe'
if (!(Test-Path $java)) { throw "JVM executable missing: $java" }
@{ java=$java; classpath="$runtime/classes;$runtime/lib/*"; assets="$upstream/forge-gui"; revision=$revision } | ConvertTo-Json | Set-Content "$runtime/bridge-config.json" -Encoding utf8NoBOM
Write-Output "Bridge ready: $runtime/bridge-config.json"
