param([string]$OutputDirectory = "$PSScriptRoot\..\TestResults\lua53")
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$archive = Join-Path $output 'lua-5.3.6.tar.gz'
$expected = 'fc5fd69bb8736323f026672b1b7235da613d7177e72558893a0bdcd320466d60'
# Official source and checksum: https://www.lua.org/ftp/
if (!(Test-Path -LiteralPath $archive)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://www.lua.org/ftp/lua-5.3.6.tar.gz' -OutFile $archive -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'Lua source checksum mismatch.' }
& tar -xzf $archive -C $output
if ($LASTEXITCODE -ne 0) { throw 'Cannot extract Lua source.' }
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if (!$vs) { throw 'Visual Studio C++ x64 build tools are required to build the test interpreter.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
$source = Join-Path $output 'lua-5.3.6\src'
$files = (Get-ChildItem -LiteralPath $source -File -Filter '*.c' | Where-Object Name -ne 'luac.c' | ForEach-Object Name) -join ' '
$batch = Join-Path $output 'build.cmd'
@("@echo off", ('call "' + $vcvars + '" >nul'), 'if errorlevel 1 exit /b 1',
    ('cl.exe /nologo /O2 /MD /D_CRT_SECURE_NO_WARNINGS /Fe:lua.exe ' + $files), 'exit /b %errorlevel%') | Set-Content -LiteralPath $batch -Encoding ASCII
Push-Location $source
try {
    & $env:ComSpec /d /c $batch
    if ($LASTEXITCODE -ne 0) { throw 'Lua interpreter build failed.' }
} finally { Pop-Location }
$runtime = Join-Path $output 'lua.exe'
Copy-Item -LiteralPath (Join-Path $source 'lua.exe') -Destination $runtime -Force
& $runtime -v
if ($LASTEXITCODE -ne 0) { throw 'Lua interpreter did not start.' }
Write-Host "Test interpreter: $runtime"
