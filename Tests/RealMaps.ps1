<#
.SYNOPSIS
Transfers private real maps into TestResults and audits assets, terrain, metadata,
gameplay functions, and pjass results without modifying the supplied inputs.
.DESCRIPTION
Requires a Debug CLI build, MPQEditor, pjass, and local common.j/blizzard.j.
Extracted destination folders are packed into private archives. Case aliases keep
multiple versions of the same terrain separate. Known-good maps are matched by
their filename with the version removed; historical differences are reported.
RunDirectory resumes generated outputs; use only with the same unchanged inputs
and CLI build. The audits always run again.
#>
param(
    [Parameter(Mandatory=$true)][string]$SourceMap,
    [Parameter(Mandatory=$true)][string]$Targets,
    [Parameter(Mandatory=$true)][string]$KnownGood,
    [string]$RunDirectory,
    [string]$VersionRegex = '\d+\.\d+(?:beta\d+)?',
    [string]$MpqEditor = "$PSScriptRoot\..\WarTransferCL\bin\Debug\MPQEditor.exe",
    [string]$Pjass = "${env:ProgramFiles(x86)}\Warcraft III\_retail_\x86_64\JassHelper\pjass.exe",
    [string]$JassLibraries = ([IO.Path]::Combine([Environment]::GetFolderPath('MyDocuments'), 'Warcraft III\JassHelper'))
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$SourceMap = (Get-Item -LiteralPath $SourceMap).FullName
$Targets = (Get-Item -LiteralPath $Targets).FullName
$KnownGood = (Get-Item -LiteralPath $KnownGood).FullName
$MpqEditor = (Get-Item -LiteralPath $MpqEditor).FullName
$Pjass = (Get-Item -LiteralPath $Pjass).FullName
$cli = "$repo\WarTransferCL\bin\Debug\WarTransferCL.exe"
[void][Reflection.Assembly]::LoadFrom($cli)
# Keep this path short: older MPQEditor versions can return zero after silently
# omitting entries whose expanded paths exceed their Windows path limit.
$run = if($RunDirectory){[IO.Path]::GetFullPath($RunDirectory)}else{Join-Path $repo ('TestResults\rm-' + [Guid]::NewGuid().ToString('N').Substring(0,8))}
if (!$run.StartsWith((Join-Path $repo 'TestResults\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'RunDirectory must be under this repository TestResults directory.' }
New-Item -ItemType Directory -Path $run -Force | Out-Null
$savedTemp = $env:TEMP; $savedTmp = $env:TMP
try {
$env:TEMP = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Temp'
$env:TMP = $env:TEMP
$calls = [Collections.Generic.List[object]]::new()

function Native([string]$Executable, [string[]]$Arguments, [string]$Label) {
    foreach ($argument in $Arguments) { if ($argument.Contains('"')) { throw 'Embedded quotes are not supported in native arguments.' } }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    # MPQEditor parses its command word itself and ignores a quoted command word.
    $start.Arguments = ($Arguments | ForEach-Object { if ($_ -match '\s' -or $_ -eq '') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        [void]$process.Start()
        $outFile = [IO.FileStream]::new((Join-Path $run ($Label + '.out')),[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::Read)
        $errFile = [IO.FileStream]::new((Join-Path $run ($Label + '.err')),[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::Read)
        try {
            $stdout = $process.StandardOutput.BaseStream.CopyToAsync($outFile)
            $stderr = $process.StandardError.BaseStream.CopyToAsync($errFile)
            $process.WaitForExit(); $stdout.Wait(); $stderr.Wait()
        } finally { $outFile.Dispose(); $errFile.Dispose() }
        $result = [pscustomobject]@{Label=$Label; ExitCode=$process.ExitCode; Milliseconds=$timer.ElapsedMilliseconds}
        $calls.Add($result)
        $calls | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'native-calls.json')
        return $result
    } finally { $process.Dispose() }
}

function Extract([string]$Archive, [string]$Directory, [string]$Label, [string]$Mask='*') {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $result = Native $MpqEditor @('extract',$Archive,$Mask,$Directory,'/fp') $Label
    if ($result.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $Directory 'war3map.j'))) { throw "Extraction failed: $Label" }
}

function Hashes([string]$Directory) {
    $result = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in Get-ChildItem -LiteralPath $Directory -File -Recurse) {
        $relative = $file.FullName.Substring($Directory.TrimEnd('\').Length + 1)
        if ($relative -in @('(listfile)','(attributes)','(signature)')) { continue }
        $result[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return ,$result
}

function ReadMap([string]$Directory) {
    $log = [IO.StringWriter]::new()
    $handler = [WarTransferCL.CommandLineErrorHandler]::new($log,$true)
    $info = [WarTransfer.W3iUtility]::ReadFile($handler,(Join-Path $Directory 'war3map.w3i'))
    if ($info -and $info.FormatVersion -ge 28 -and $info.ScriptTypeId -ne 0) { throw 'RealMaps.ps1 currently audits JASS maps only; Lua acceptance needs a separate real-map corpus.' }
    $strings = [WarTransfer.WtsUtility]::ReadFile($handler,(Join-Path $Directory 'war3map.wts'))
    $script = [WarTransfer.JUtility]::ReadFile($handler,(Join-Path $Directory 'war3map.j'))
    if ($handler.KillProcess) { throw $log.ToString() }
    return [pscustomobject]@{Info=$info; Strings=$strings; Script=$script}
}

function Canonical($Value, $Strings) {
    $json = ConvertTo-Json -InputObject $Value -Depth 30 -Compress
    return [regex]::Replace($json, '(?<!\\)"TRIGSTR_(\d+)"', [Text.RegularExpressions.MatchEvaluator]{ param($match)
        $key = [int]$match.Groups[1].Value
        if (!$Strings.FileMap.ContainsKey($key)) { return '"<missing-reference>"' }
        return ConvertTo-Json -InputObject $Strings.GetValue($key) -Compress
    })
}

function Functions($Map, [bool]$ResolveStrings=$true) {
    $result = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Map.Script.Functions.GetEnumerator()) {
        $writer = [IO.StringWriter]::new(); $entry.Value.Serialize($writer)
        $text = $writer.ToString()
        if ($ResolveStrings -and $text.Contains('TRIGSTR_')) {
            $text = [regex]::Replace($text, '(?<!\\)"TRIGSTR_(\d+)"', [Text.RegularExpressions.MatchEvaluator]{ param($match)
                $key = [int]$match.Groups[1].Value
                if (!$Map.Strings.FileMap.ContainsKey($key)) { return '"<missing-reference>"' }
                return ConvertTo-Json -InputObject $Map.Strings.GetValue($key) -Compress
            })
        }
        $result[$entry.Key] = $text
    }
    return ,$result
}

function CheckScript([string]$Directory, [string]$Label) {
    $result = Native $Pjass @((Join-Path $JassLibraries 'common.j'),(Join-Path $JassLibraries 'blizzard.j'),(Join-Path $Directory 'war3map.j')) $Label
    return $result.ExitCode
}

function MetadataErrors($Source, $Target, $Actual, [string]$Version, [string]$Pattern) {
    foreach ($property in $Target.Info.PSObject.Properties) {
        $name = $property.Name
        $expected = Canonical $property.Value $Target.Strings
        if ($name -eq 'Flags') {
            $expected = [string](([int]$Target.Info.Flags -band (-bnot 4)) -bor ([int]$Source.Info.Flags -band 4))
        } elseif (($name -eq 'GameDataSet' -and $Target.Info.FormatVersion -ge 25) -or ($name -in @('SupportedModes','GameDataVersion') -and $Target.Info.FormatVersion -ge 31)) {
            $expected = Canonical $Source.Info.$name $Source.Strings
        } elseif ($name -in @('Name','Description')) {
            # VersionUtility replaces the first matched version throughout each
            # line. Work on resolved strings so WTS allocation is immaterial.
            $value = ConvertFrom-Json -InputObject $expected
            $lines = foreach ($line in ($value -split "`n")) {
                $match = [regex]::Match($line,$Pattern)
                if ($match.Success) { $line.Replace($match.Groups[$match.Groups.Count-1].Value,$Version) } else { $line }
            }
            $expected = ConvertTo-Json -InputObject ($lines -join "`n") -Compress
        }
        if ((Canonical $Actual.Info.$name $Actual.Strings) -ne $expected) { $name }
    }
}

function MissingStringReferences($Map, [string]$Directory) {
    $text = (ConvertTo-Json -InputObject $Map.Info -Depth 30 -Compress) + [IO.File]::ReadAllText((Join-Path $Directory 'war3map.j'))
    $references = [regex]::Matches($text,'(?<!\\)"TRIGSTR_(\d+)"') | ForEach-Object { [int]$_.Groups[1].Value } | Select-Object -Unique
    foreach ($key in $references) { if (!$Map.Strings.FileMap.ContainsKey($key)) { 'TRIGSTR_' + $key } }
}

Write-Host "Read-only input snapshot; private artifacts: $run"
$originalFiles = @((Get-Item -LiteralPath $SourceMap)) + @(Get-ChildItem -LiteralPath $Targets -File -Recurse) + @(Get-ChildItem -LiteralPath $KnownGood -File -Recurse)
$snapshot = Join-Path $run 'inputs-before.json'
if (Test-Path -LiteralPath $snapshot) { $before = Get-Content -LiteralPath $snapshot -Raw | ConvertFrom-Json }
else {
    $before = @($originalFiles | ForEach-Object { [pscustomobject]@{Path=$_.FullName; Hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
    $before | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $snapshot
}
$inputDirectory = Join-Path $run 'in'; New-Item -ItemType Directory -Path $inputDirectory -Force | Out-Null
$sourceVersion = [regex]::Match([IO.Path]::GetFileNameWithoutExtension($SourceMap),$VersionRegex).Value
if (!$sourceVersion) { throw 'Version regex does not match the source filename.' }
$cases = @(); $number = 0
foreach ($item in Get-ChildItem -LiteralPath $Targets | Where-Object Extension -in '.w3x','.w3m' | Sort-Object Name) {
    $number++; $id = '{0:d2}' -f $number
    $stem = [regex]::Replace([IO.Path]::GetFileNameWithoutExtension($item.Name),$VersionRegex,'')
    $alias = 'c' + $id + '-' + [IO.Path]::GetFileNameWithoutExtension($item.Name) + '.w3x'
    $staged = Join-Path $inputDirectory $alias
    Write-Host "Preparing $id $($item.Name)"
    if ($item.PSIsContainer) {
        if (@(Get-ChildItem -LiteralPath $item.FullName -File -Recurse | Where-Object Name -Match '^File\d{8}\.').Count) { throw 'Cannot rebuild an extracted folder with unnamed entries.' }
        if (!(Test-Path -LiteralPath $staged)) {
            if ((Native $MpqEditor @('n',$staged,'4096') ('pack-'+$id+'-new')).ExitCode -ne 0 -or !(Test-Path -LiteralPath $staged)) { throw "Cannot create $alias" }
            if ((Native $MpqEditor @('a',$staged,$item.FullName,'/c','/r') ('pack-'+$id+'-add')).ExitCode -ne 0) { throw "Cannot pack $alias" }
        }
    } else { Copy-Item -LiteralPath $item.FullName -Destination $staged }
    $gold = @(Get-ChildItem -LiteralPath $KnownGood -File | Where-Object { [regex]::Replace($_.BaseName,$VersionRegex,'') -eq $stem })
    $cases += [pscustomobject]@{Id=$id; Name=$item.Name; Original=$item.FullName; Directory=$item.PSIsContainer; Staged=$staged;
        OutputName=([regex]::Replace($alias,$VersionRegex,$sourceVersion)); Golden=if($gold.Count -eq 1){$gold[0].FullName}else{$null}}
}
$cases | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'cases.json')
$output = Join-Path $run 'out'
$pending = @($cases | Where-Object { !(Test-Path -LiteralPath (Join-Path $output $_.OutputName)) })
$transferMilliseconds = $null
if ($pending.Count) {
    $pendingDirectory = Join-Path $run ('pending-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
    New-Item -ItemType Directory -Path $pendingDirectory -Force | Out-Null
    foreach ($case in $pending) { Copy-Item -LiteralPath $case.Staged -Destination (Join-Path $pendingDirectory ([IO.Path]::GetFileName($case.Staged))) }
    Write-Host "Transferring $($pending.Count) pending cases with destination cameras/regions retained."
    $label = 'transfer-' + [Guid]::NewGuid().ToString('N').Substring(0,6)
    $transfer = Native $cli @('--source',$SourceMap,'--targets',$pendingDirectory,'--output',$output,'--mpq-editor',$MpqEditor,'--version-regex',$VersionRegex,'--verbose','--json') $label
    $result = Get-Content -LiteralPath (Join-Path $run ($label + '.out')) -Raw | ConvertFrom-Json
    if ($transfer.ExitCode -ne 0 -or !$result.success -or $result.targets.Count -ne $pending.Count -or $result.outputs.Count -ne $pending.Count) { throw "Transfer failed/incomplete; see $run\$label.err" }
    $transferMilliseconds = $transfer.Milliseconds
    Write-Host "Pending transfers completed in $transferMilliseconds ms."
}
Write-Host "Auditing all $($cases.Count) cases."
$sourceDirectory = Join-Path $run 'src'; Extract $SourceMap $sourceDirectory 'extract-source'
$source = ReadMap $sourceDirectory; $sourceHashes = Hashes $sourceDirectory; $sourceFunctions = Functions $source $false
$sourceCheck = CheckScript $sourceDirectory 'pjass-source'
$worldPattern = '^(config|main|CreateAllUnits|CreateNeutralPassiveBuildings|CreatePlayerBuildings|CreateNeutralPassive|CreateNeutralHostile|CreatePlayerUnits|CreateUnitsForPlayer\d+|CreateBuildingsForPlayer\d+|InitCustomPlayerSlots|InitCustomTeams|InitAllyPriorities|InitRandomGroups|CreateRegions|CreateCameras|Unit\d+_DropItems|ItemTable\d+_DropItems)$'
$terrainFiles = @('war3map.doo','war3map.mmp','war3mapMap.blp','war3map.shd','war3map.w3e','war3map.wpm','war3map.blp','war3mapUnits.doo','war3map.w3c','war3map.w3r')
$goldCache = @{}; $reports = @()
foreach ($case in $cases) {
    Write-Host "Auditing $($case.Id) $($case.Name)"
    $terrain = Join-Path $run ('t'+$case.Id); Extract $case.Staged $terrain ('extract-terrain-'+$case.Id) 'war3map*'
    $produced = Join-Path $run ('o'+$case.Id); Extract (Join-Path $output $case.OutputName) $produced ('extract-output-'+$case.Id)
    $target = ReadMap $terrain; $actual = ReadMap $produced; $actualHashes = Hashes $produced; $targetHashes = Hashes $terrain
    $actualFunctions = Functions $actual
    $actualRawFunctions = Functions $actual $false
    $assetErrors = @(); $terrainErrors = @(); $gameplayErrors = @()
    foreach ($entry in $sourceHashes.GetEnumerator()) {
        if ($entry.Key -in ($terrainFiles + @('war3map.j','war3map.w3i','war3map.wts'))) { continue }
        if (!$actualHashes.ContainsKey($entry.Key) -or $actualHashes[$entry.Key] -ne $entry.Value) { $assetErrors += $entry.Key }
    }
    foreach ($name in $terrainFiles) {
        if ($targetHashes.ContainsKey($name) -and (!$actualHashes.ContainsKey($name) -or $actualHashes[$name] -ne $targetHashes[$name])) { $terrainErrors += $name }
    }
    foreach ($entry in $sourceFunctions.GetEnumerator()) {
        if ($entry.Key -match $worldPattern) { continue }
        if (!$actualRawFunctions.ContainsKey($entry.Key) -or $actualRawFunctions[$entry.Key] -ne $entry.Value) { $gameplayErrors += $entry.Key }
    }
    $goldChanges = @(); $goldFunctions = @(); $infoChanges = @(); $goldPjass = $null
    if ($case.Golden) {
        if (!$goldCache.ContainsKey($case.Golden)) {
            $dir = Join-Path $run ('g'+$case.Id); Extract $case.Golden $dir ('extract-golden-'+$case.Id)
            $map = ReadMap $dir
            $goldCache[$case.Golden] = [pscustomobject]@{Map=$map; Hashes=(Hashes $dir); Functions=(Functions $map); Pjass=(CheckScript $dir ('pjass-golden-'+$case.Id))}
        }
        $gold = $goldCache[$case.Golden]; $goldPjass = $gold.Pjass
        foreach ($entry in $actualHashes.GetEnumerator()) { if (!$gold.Hashes.ContainsKey($entry.Key) -or $gold.Hashes[$entry.Key] -ne $entry.Value) { $goldChanges += $entry.Key } }
        foreach ($entry in $actualFunctions.GetEnumerator()) { if (!$gold.Functions.ContainsKey($entry.Key) -or $gold.Functions[$entry.Key] -ne $entry.Value) { $goldFunctions += $entry.Key } }
        foreach ($property in $actual.Info.PSObject.Properties) {
            if ((Canonical $property.Value $actual.Strings) -ne (Canonical $gold.Map.Info.($property.Name) $gold.Map.Strings)) { $infoChanges += $property.Name }
        }
    }
    $reports += [pscustomobject]@{Case=$case; SourcePjass=$sourceCheck; TerrainPjass=(CheckScript $terrain ('pjass-terrain-'+$case.Id));
        OutputPjass=(CheckScript $produced ('pjass-output-'+$case.Id)); GoldenPjass=$goldPjass;
        Format=$actual.Info.FormatVersion; Players=$actual.Info.PlayerCount; SourceAssets=$sourceHashes.Count; OutputFiles=$actualHashes.Count;
        AssetErrors=$assetErrors; TerrainErrors=$terrainErrors; GameplayErrors=$gameplayErrors;
        MetadataErrors=@(MetadataErrors $source $target $actual $sourceVersion $VersionRegex);
        MissingStringReferences=@(MissingStringReferences $actual $produced);
        GoldenFileDifferences=$goldChanges; GoldenFunctionDifferences=$goldFunctions; GoldenMetadataDifferences=$infoChanges}
    $reports | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'report.json')
}
Write-Host 'Verifying original input files are unchanged.'
$changed = @($before | Where-Object { !(Test-Path -LiteralPath $_.Path) -or (Get-FileHash -LiteralPath $_.Path -Algorithm SHA256).Hash -ne $_.Hash } | ForEach-Object Path)
$summary = [pscustomobject]@{Run=$run; Source=$SourceMap; Cases=$reports.Count; PendingTransferCases=$pending.Count; PendingTransferMilliseconds=$transferMilliseconds;
    SourcePjass=$sourceCheck; OutputPjassFailures=@($reports | Where-Object OutputPjass -ne 0).Count;
    AssetFailures=@($reports | Where-Object { $_.AssetErrors.Count }).Count; TerrainFailures=@($reports | Where-Object { $_.TerrainErrors.Count }).Count;
    GameplayFailures=@($reports | Where-Object { $_.GameplayErrors.Count }).Count;
    MetadataFailures=@($reports | Where-Object { $_.MetadataErrors.Count }).Count;
    StringReferenceFailures=@($reports | Where-Object { $_.MissingStringReferences.Count }).Count; ChangedInputs=$changed; OriginalFiles=$before.Count}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'summary.json')
$calls | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'native-calls.json')
$summary | ConvertTo-Json -Depth 5
if ($sourceCheck -ne 0 -or $summary.OutputPjassFailures -or $summary.AssetFailures -or $summary.TerrainFailures -or $summary.GameplayFailures -or $summary.MetadataFailures -or $summary.StringReferenceFailures -or $changed.Count) {
    throw "Real-map acceptance checks failed; see $run\summary.json and report.json."
}
} finally {
    $env:TEMP = $savedTemp; $env:TMP = $savedTmp
}
