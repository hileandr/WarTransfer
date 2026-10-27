param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$UnitOnly,
    [switch]$RequireIntegration
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath("$PSScriptRoot\..")
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = (Get-Command MSBuild.exe -ErrorAction SilentlyContinue).Source
if (!$msbuild) { $msbuild = & $vswhere -latest -products '*' -find 'MSBuild\Current\Bin\MSBuild.exe' | Select-Object -First 1 }
$vstest = & $vswhere -latest -products '*' -find 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe' | Select-Object -First 1
if (!$msbuild -or !$vstest) { throw 'Visual Studio MSBuild and VSTest are required.' }
if (!$env:WARTRANSFER_TEST_MPQ_EDITOR) {
    foreach ($path in @("$repo\WarTransferCL\bin\Debug\MPQEditor.exe", "$repo\WarTransferUI\bin\Debug\net6.0-windows\MPQEditor.exe")) {
        if (Test-Path -LiteralPath $path) { $env:WARTRANSFER_TEST_MPQ_EDITOR = $path; break }
    }
}
if (!$env:WARTRANSFER_TEST_PJASS) {
    $path = "${env:ProgramFiles(x86)}\Warcraft III\_retail_\x86_64\JassHelper\pjass.exe"
    if (Test-Path -LiteralPath $path) { $env:WARTRANSFER_TEST_PJASS = $path }
}
if ($RequireIntegration -and (![IO.File]::Exists($env:WARTRANSFER_TEST_MPQ_EDITOR) -or ![IO.File]::Exists($env:WARTRANSFER_TEST_PJASS))) {
    throw 'Integration checks require WARTRANSFER_TEST_MPQ_EDITOR and WARTRANSFER_TEST_PJASS.'
}
$env:TEMP = Join-Path $repo 'TestResults\temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
Push-Location $repo
try {
    & $msbuild Tests\Tests.csproj /t:Build "/p:Configuration=$Configuration" /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    Set-Location "Tests\bin\$Configuration"
    $arguments = @('Tests.dll', '/Logger:trx', '/Logger:console;verbosity=minimal', "/ResultsDirectory:$repo\TestResults")
    if ($UnitOnly) { $arguments += '/TestCaseFilter:TestCategory!=Integration&TestCategory!=Pjass' }
    $arguments += @('--', 'RunConfiguration.TestSessionTimeout=120000')
    & $vstest @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
