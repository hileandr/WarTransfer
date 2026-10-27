# Transfer regression tests

Build `Tests/Tests.csproj` with Visual Studio MSBuild, then run `Tests.dll` with
`vstest.console.exe` from `Tests/bin/Debug` (or `Release`). Existing binary
fixtures are resolved relative to that working directory.

```powershell
& $msbuild Tests\Tests.csproj /t:Build /p:Configuration=Debug
Push-Location Tests\bin\Debug
try {
    & $vstest Tests.dll /Logger:trx
} finally {
    Pop-Location
}
```

Set `$msbuild` and `$vstest` to the corresponding executables in your Visual
Studio installation. Set `WARTRANSFER_TEST_MPQ_EDITOR` to the absolute path of
Ladik's `MPQEditor.exe` to enable the integration tests. Without it, those tests
are reported as inconclusive; the script tests require no archive tool.

`./Tests/Run.ps1 -RequireIntegration` builds and runs the suites, discovers local
MPQEditor/pjass installations, and fails immediately if either tool is missing.
`./Tests/Run.ps1 -UnitOnly -Configuration Release` runs the portable unit checks.
The Windows GitHub Actions workflow runs that explicitly named unit subset;
game/archive integration is a separate local check, not silently counted as CI
coverage. The workflow configuration has been added; its hosted run still needs
to execute after these changes are pushed.

`PjassValidationTests` validates a merged self-contained fixture and verifies
that pjass rejects an undefined function. It also reads a locally cached compiled
map script, round-trips it, and checks it against the local game libraries when
available. Configure custom paths with `WARTRANSFER_TEST_PJASS`,
`WARTRANSFER_TEST_REAL_JASS`, and `WARTRANSFER_TEST_JASS_LIBRARIES` (the directory
containing `common.j` and `blizzard.j`). These external files are read-only and
are not redistributed in this repository. The corpus check is inconclusive when
no local compiled script/libraries are available.

Additional suites cover lexical/structural JASS errors, escaped and empty strings,
multiline/Unicode WTS data and malformed blocks, missing/colliding references,
partial publication and cancellation, genuinely unnamed MPQ entries, and W3I
truncation at every byte boundary. See [performance and remaining validation](PERFORMANCE.md).

The new suites cover:

- `JassTransferTests`: gameplay retention, destination configuration and units,
  initialization order and sounds, insertion/removal of generated functions and
  item-drop callbacks, camera/region globals and optional functions, WTS and
  `ExecuteFunc` remapping, and failure without overwriting the output script.
- `ScriptTypeTests`: JASS selection for legacy and current W3I layouts, Lua
  rejection for either map role, unknown types, missing/invalid metadata, missing
  scripts, and selection of the active script when both files exist.
- `TransferWorkflowTests`: real archive transfers to multiple destinations,
  camera/region and random-table switches, metadata and string merging, versioned
  filenames, unchanged input archives, and rejection before merging or replacing
  existing outputs. A Lua source is rejected before opening destination archives.

The maps constructed by these tests contain synthetic scripts and data markers;
they test transfer behavior and archive preservation, not Warcraft III gameplay.

# Private real-map acceptance

After building the Debug CLI, run the separate acceptance harness against local
maps and a directory of previous outputs:

```powershell
./Tests/RealMaps.ps1 -SourceMap 'D:\Maps\Data1.09beta6.w3x' `
    -Targets 'D:\Maps\Terrains' -KnownGood 'D:\Maps\PreviousOutputs'
```

This keeps destination cameras, regions, and random tables. It accepts archived
and extracted-folder destinations, packs folders into private copies, and gives
each case an alias so older versions of the same terrain are tested separately.
The CLI itself still requires archive destinations. Known-good filenames are
matched after removing the version; `-VersionRegex` defaults to
`\d+\.\d+(?:beta\d+)?`. Override `-MpqEditor`, `-Pjass`, or `-JassLibraries` for
other local installations. The libraries directory must contain `common.j` and
`blizzard.j`.

All outputs, extracted contents, logs, and JSON reports stay under ignored
`TestResults/rm-*`. Input SHA-256 snapshots are checked after the run. Every
output is checked by pjass, source assets and gameplay functions are compared
with the source, and terrain files and resolved metadata are compared with the
destination. Script and metadata WTS references must resolve. The script fails
if these invariants fail. Compiler results for
originals and historical outputs are also recorded; historical differences are
reported separately because a newer source or terrain need not reproduce an old
output byte for byte. These checks do not replace reopening maps in World Editor
or playing them.

Use `-RunDirectory` with an existing run to reuse generated outputs and repeat
the audits, only while its inputs and CLI build remain unchanged. Keep scratch
paths short: older MPQEditor versions can return success after omitting files
when a native path is too long. Private maps and their extracted contents should
not be committed.

# Script boundary

The shared engine calls `MapScripts.Open` on the extracted source and all
destinations before starting any merges. `war3map.w3i` selects the active
language: formats before 28 imply JASS; newer layouts use `ScriptTypeId` (0 for
JASS, 1 for Lua). The corresponding script file must exist. An inactive Lua file
alongside an active JASS script does not change the map's declared language.
The CLI's `--dry-run` remains a path/output-plan check and does not extract maps
or validate their script languages.

`IMapScriptMerger` owns reading, merging, and writing map scripts. The map workflow
passes map directories, transfer options through the selected implementation's
constructor, and WTS tables; it does not use JASS document types. The existing
implementation lives in `WarTransfer/Scripts/JassScriptMerger.cs`. A future Lua
implementation can use a different parser behind the same interface and be
selected in `MapScripts.Open`. Different source/destination languages are rejected
by the engine; adding another implementation does not enable script conversion.

Camera and region transfer now consistently selects the requested functions and
globals, and inserts their initialization calls when the terrain lacks those
calls. Missing, duplicate, or conditional `InitBlizzard()` markers fail explicitly.
Random-unit-table selection also selects the matching generated globals and
initializer. Source-transferred table names keep their source WTS references.
The parser checks basic structure and literal boundaries; it remains a lightweight
parser, not a complete JASS compiler/type checker. pjass is currently used by tests.
