# Transfer regression tests

Build `Tests/Tests.csproj` with Visual Studio MSBuild, then run `Tests.dll` with
`vstest.console.exe` from `Tests/bin/Debug` (or `Release`). Existing binary
fixtures are resolved relative to that working directory.

```powershell
& $msbuild Tests\Tests.csproj /restore /t:Build /p:Configuration=Debug
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
MPQEditor/pjass installations and a locally built Lua interpreter, and fails
immediately if a required tool is missing. Build the interpreter first with
`./Tests/BuildLua.ps1` (Visual Studio C++ x64 tools required), or set
`WARTRANSFER_TEST_LUA` to an existing Lua 5.3 executable.
`./Tests/Run.ps1 -UnitOnly -Configuration Release` runs the unit subset, including
Lua execution checks when an interpreter is available. Add `-RequireLua` to fail
if that interpreter is missing.
The Windows GitHub Actions workflow builds the checksum-pinned official Lua
5.3.6 source and runs that explicitly named unit subset with `-RequireLua`;
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

The suites cover:

- `JassTransferTests`: gameplay retention, destination configuration and units,
  initialization order and sounds, insertion/removal of generated functions and
  item-drop callbacks, camera/region globals and optional functions, WTS and
  `ExecuteFunc` remapping, and failure without overwriting the output script.
- `ScriptTypeTests`: JASS and Lua selection for supported W3I layouts,
  unknown types, missing/invalid metadata, missing
  scripts, and selection of the active script when both files exist.
- `TransferWorkflowTests`: real archive transfers to multiple destinations,
  camera/region and random-table switches, metadata and string merging, versioned
  filenames, unchanged input archives, and rejection before merging or replacing
  existing outputs. An unknown source language is rejected before opening
  destination archives; mixed-language batches fail before any merging. The
  standalone Lua CLI test executes the extracted output with a Lua interpreter.

The maps constructed by these tests contain synthetic scripts and data markers;
they test transfer behavior and archive preservation, not Warcraft III gameplay.

# Private real-map acceptance

After building the Debug CLI, run the separate JASS acceptance harness against
local maps and a directory of previous outputs:

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
constructor, and WTS tables; it does not use language-specific document types.
`JassScriptMerger` and `LuaScriptMerger` own their respective parsers. Different
source/destination languages are rejected by the engine before merging.

Camera and region transfer now consistently selects the requested functions and
globals, and inserts their initialization calls when the terrain lacks those
calls. Missing, duplicate, or conditional `InitBlizzard()` markers fail explicitly.
Random-unit-table selection also selects the matching generated globals and
initializer. Source-transferred table names keep their source WTS references.
The parser checks basic structure and literal boundaries; it remains a lightweight
JASS parser, not a complete compiler/type checker. pjass is used by tests.

# Lua transfers

`LuaScriptMerger` uses [Loretta 0.2.13](https://github.com/LorettaDevs/Loretta)
with Lua 5.3 syntax options. The parser dependency is pinned through NuGet and
restored by MSBuild; no Lua interpreter is needed to run transfers. Map scripts
are parsed, never executed by the transfer engine. Text edits preserve source
gameplay, closures, comments, literals, and formatting outside replaced spans.

The supported layout is World Editor style: top-level `function config()` and
`function main()` declarations, each without parameters, and one top-level,
unconditional `InitBlizzard()` call in each main. Main initialization before
that call must be straight-line code. Weather locals such as `local we` are
supported: discarded source locals must not be referenced by retained source
code, and retained destination locals must not shadow its bindings. Scope checks
distinguish inner locals, parameters, and member names. Local `_ENV` overrides
across the boundary are rejected. Destination
terrain initialization is followed by the source marker and source gameplay
suffix. Source sound initialization is retained on the source's side of the
marker. Missing selected initializers are inserted in region/camera/group order
before placement functions. Placement functions include units, destructables,
items, player/team configuration, and drop callbacks.

Camera/region functions and globals follow their transfer switches. Random group
code/globals and item-table callbacks follow the table switches. Placed-unit,
destructable, and item globals come from the destination; custom globals, sounds,
triggers, and other gameplay stay with the source. Exact WTS references in
imported literals are allocated in the output table without rewriting source
gameplay references or comments. Short and long quoted strings are supported.

Duplicate/assigned/local generated entrypoints, conditional initialization,
mixed global assignments that cannot be separated, and destination-local or
missing helper dependencies fail explicitly. This is not a general Lua program
merger or semantic/type checker. Frameworks that replace or wrap generated
entrypoints need dedicated fixtures and may require further support. A failed
merge leaves staged scripts untouched and publishes no batch outputs.
When source cameras, regions, or random groups are selected, imported terrain
code must refer to matching globals declared by the source; differing names fail
instead of generating nil placement dependencies.

`LuaTransferTests` covers merge ownership, WTS collisions, initialization,
malformed scripts, unsafe scopes, and output preservation. `LuaExecutionTests`
runs synthetic fixtures through a separate Lua 5.3 interpreter with native stubs
and asserts the exact initialization/callback sequence for different options.
`BuildLua.ps1` downloads [official Lua source](https://www.lua.org/ftp/), checks its
SHA-256, and builds into ignored `TestResults`; it installs nothing system-wide.
The source fixture includes closures, bitwise operators, integer division, and
long strings. Game/archive/compiler checks do not open World Editor or Warcraft
III or take desktop focus.

Additional real Lua fixtures and isolated in-game validation remain useful before
claiming production compatibility. A known-good result is useful when available,
but input/output invariants can validate a first transfer. `RealMaps.ps1` audits
JASS; the 22-map Spellbringers corpus is JASS.

The first real Lua source exposed the editor-generated weather local and now
passes scope checks. Real acceptance also transferred that format-33, two-player
source to a different, Lua-saved Turtle Rock destination (format 39, four players).
The independent private audit checked all 312 output content entries against
source assets and destination terrain, exact source gameplay function bodies and
globals, destination generated functions and globals, main initialization on
both sides of `InitBlizzard()`, resolved metadata, gameplay strings, and complete
string references. Source, destination, and output pass the standalone Lua 5.3
syntax check without executing map code. Both original archives are unchanged.
The output retains four players and destination camera/region files. Those files
are empty in this pair, so nonempty camera/region behavior still relies on the
synthetic fixtures. No in-game Lua behavior has been validated. Private artifacts
and the audit script remain under ignored `TestResults/rl-58f65e41/`.
