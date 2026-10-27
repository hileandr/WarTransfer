param(
    [string]$Label = 'current',
    [int]$Iterations = 3,
    [string]$MpqEditor = "$PSScriptRoot\..\WarTransferCL\bin\Debug\MPQEditor.exe"
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$run = Join-Path $repo ("TestResults\benchmark-" + $Label + '-' + [Guid]::NewGuid().ToString('N'))
$sourceFiles = Join-Path $run 'source-files'
$terrains = Join-Path $run 'terrains'
$output = Join-Path $run 'output'
New-Item -ItemType Directory -Path $sourceFiles,$terrains -Force | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\TestFiles\test39.w3i" -Destination "$sourceFiles\war3map.w3i"
[IO.File]::WriteAllText("$sourceFiles\war3map.j", "globals`nendglobals`nfunction config takes nothing returns nothing`nendfunction`nfunction main takes nothing returns nothing`n    call InitBlizzard()`nendfunction`n")
[IO.File]::WriteAllText("$sourceFiles\war3map.wts", ((1..100 | ForEach-Object { "STRING $_`n{`nFixture $_`n}`n" }) -join ''))
[IO.File]::WriteAllText("$sourceFiles\war3map.w3e", 'terrain marker')
$random = [Random]::new(12345)
$bytes = [byte[]]::new(4 * 1024 * 1024)
for ($i=0; $i -lt 4; $i++) { $random.NextBytes($bytes); [IO.File]::WriteAllBytes("$sourceFiles\asset$i.blp", $bytes) }
for ($i=0; $i -lt 512; $i++) { [IO.File]::WriteAllText("$sourceFiles\small$i.txt", ('x' * 1024)) }
$source = Join-Path $run 'source.w3x'
& $MpqEditor n $source 4096 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot create benchmark source archive.' }
& $MpqEditor a $source $sourceFiles /c /r | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot pack benchmark source archive.' }
for ($i=0; $i -lt 2; $i++) { Copy-Item -LiteralPath $source -Destination "$terrains\terrain$i.w3x" }
$samples = @()
$nativeSamples = @()
for ($i=0; $i -lt $Iterations; $i++) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $arguments = @('--source', ('"' + $source + '"'), '--targets', ('"' + $terrains + '"'), '--output', ('"' + $output + '"'), '--mpq-editor', ('"' + $MpqEditor + '"'), '--overwrite', '--verbose', '--json')
    $process = Start-Process -FilePath "$repo\WarTransferCL\bin\Debug\WarTransferCL.exe" -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput "$run\result$i.json" -RedirectStandardError "$run\log$i.txt"
    $timer.Stop()
    if ($process.ExitCode -ne 0) { throw "Benchmark transfer failed: $run\log$i.txt" }
    $samples += $timer.Elapsed.TotalMilliseconds
    $native = [regex]::Matches([IO.File]::ReadAllText("$run\log$i.txt"), 'Process duration (\d+) ms')
    $nativeSamples += [pscustomobject]@{ calls=$native.Count; milliseconds=($native | ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Sum).Sum }
}
$report = [pscustomobject]@{ label=$Label; milliseconds=$samples; mean_ms=($samples | Measure-Object -Average).Average; native_process_samples=$nativeSamples; source_bytes=(Get-Item -LiteralPath $source).Length; destinations=2; directory=$run }
$report | ConvertTo-Json | Set-Content -LiteralPath "$run\benchmark.json"
$report | ConvertTo-Json -Compress
