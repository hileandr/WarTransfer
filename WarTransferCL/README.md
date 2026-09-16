# WarTransferCL

Noninteractive Windows command-line interface to the shared WarTransfer engine.
Requires .NET Framework 4.7.2 and Ladik's `MPQEditor.exe`. Supports JASS maps,
including Warcraft III 3.0 map information. Does not read GUI settings or prompt.

## Agent quick start

Use absolute paths in automation. Preview the plan first:

```powershell
& .\WarTransferCL.exe transfer `
  --source 'D:\Maps\My Mod 2.0.w3x' `
  --targets 'D:\Maps\Terrains' `
  --output 'D:\Maps\Generated' `
  --mpq-editor 'D:\Tools\MPQEditor.exe' `
  --dry-run --json
$LASTEXITCODE
```

Remove `--dry-run` to execute. No confirmation is required. A successful dry run
checks the paths, selected maps, naming, overwrite policy, regex, and tool existence;
it does **not** extract maps, validate their contents, run the tool, or test gameplay.

Use `--json` and check the process exit code. Stdout contains exactly one JSON
object; human-readable logs go to stderr. `--verbose` adds progress, exception
stacks, and archive-tool output to stderr. No console input is requested.
`--help` and `--version` work without maps or MPQEditor, including with `--json`.

## Selection and transfer options

| Option | Meaning/default |
| --- | --- |
| `--source PATH` | Required source `.w3x` or `.w3m` archive whose mod data is copied. |
| `--targets DIRECTORY` | Required directory of terrain map archives, immediate children only. The source itself is excluded. |
| `--output DIRECTORY` | Required output directory; created when publishing successful results. All output archives use `.w3x`. |
| `--mpq-editor PATH` | Explicit tool path; defaults to `MPQEditor.exe` beside the CLI, independent of the working directory. |
| `--dry-run` | Validate and return the planned output filenames without modifying files. |
| `--overwrite` | Allow replacing existing output maps; absent by default. |
| `--version-regex PATTERN` | Extract the version from the source filename and update matching target filenames/map strings through the engine. Disabled by default. |
| `--transfer-cameras` | Enable the shared engine's source-camera transfer option. Default false. |
| `--transfer-regions` | Enable the shared engine's source-region transfer option. Default false. |
| `--transfer-unit-tables` | Copy source random unit tables. Default false. |
| `--transfer-item-tables` | Copy source random item tables. Default false. |
| `--flags LIST` | Map flags to copy: comma-separated `W3iFlags` names, `none`, or a 32-bit hexadecimal mask (`0x...`). Default `MeleeMap`. |
| `--json` | Machine-readable result on stdout. |
| `--verbose` | Detailed logs on stderr. |

The `transfer` word is optional. Both `--option value` and `--option=value` work
for value options. Duplicate options, unknown options, missing values, and invalid
flag names fail with exit 2. Boolean switches do not take values.

Flags are defined in [W3iFile.cs](../WarTransfer/FileTypes/W3iFile.cs), including
`MeleeMap`, `UseCustomForces`, `CustomForcesFixedPlayerSettings`, `UseCustomTechtree`,
`UseCustomAbilities`, and `UseCustomUpgrades`. For example:

```powershell
& .\WarTransferCL.exe transfer --source 'D:\Maps\Mod 2.0.w3x' `
  --targets 'D:\Terrains' --output 'D:\Generated' `
  --version-regex '\d+\.\d+' --flags 'MeleeMap,UseCustomForces' `
  --transfer-unit-tables --transfer-item-tables --json
```

The version regex must match a nonempty version in the source filename without
its extension. The shared engine uses the last capture group of the first match
(or the whole match if there are no groups); it replaces the whole matching span
in target filenames. Prefer patterns without capture groups. Matching has a
2-second timeout. Filename collisions after version replacement are rejected.

## JSON contract and exit codes

A transfer/dry-run result has `schema_version: 1` and these fields:

```json
{
  "schema_version": 1,
  "success": true,
  "exit_code": 0,
  "dry_run": false,
  "source": "D:\\Maps\\Mod.w3x",
  "targets": ["D:\\Terrains\\Terrain.w3x"],
  "output_directory": "D:\\Generated",
  "planned_outputs": ["D:\\Generated\\Terrain.w3x"],
  "outputs": ["D:\\Generated\\Terrain.w3x"],
  "errors": []
}
```

`outputs` lists only maps published by this invocation; it is empty for a dry run.
On a publishing failure or cancellation, it can contain a partially published
batch. `planned_outputs` is the preflight plan and does not imply success. On
argument failures, paths may be absent or only partly normalized. Help/version
JSON has `schema_version`, `success`, `exit_code`, and `text` instead.

| Exit | Meaning |
| --- | --- |
| 0 | Transfer, dry run, help, or version succeeded. |
| 1 | Transfer, archive, filesystem, or cleanup failure. |
| 2 | Invalid arguments or preflight plan, including regex timeout. |
| 3 | MPQEditor is missing. |
| 130 | Cancelled with Ctrl+C. |

Any engine error stops a CLI transfer, including errors historically marked
nonfatal in the GUI. Ctrl+C requests cancellation; a running archive process is
terminated and temporary files are cleaned. Cancellation during managed file
copying waits for that operation to return. Forced process termination can leave
temporary files in `%TEMP%\WarTransfer` or `%TEMP%\WarTransferCL`.

## Filesystem behavior

Inputs are read and copied; they are never overwritten. Output trees cannot
overlap the target directory or contain the source archive. Symbolic links and
junctions in map/output paths are rejected to keep this rule unambiguous.
Extracted map directories are not accepted by the CLI. Duplicate source/target
basenames, `.w3m`/`.w3x` collisions, and versioned output collisions are rejected.

Each process uses unique temporary folders. Archive commands execute directly,
without a shell, and stop on the first failure. The entire batch is generated in
staging before publishing starts. A transfer failure leaves existing output maps
untouched. Publishing uses a temporary file in the output directory and an atomic
move/replace for each map; the batch itself is not one atomic transaction. Existing
unrelated output files are left alone. Separate output directories are recommended
for concurrent runs; with `--overwrite`, the last publisher of the same map wins.

## Build and test

From a Visual Studio Developer PowerShell with .NET Framework 4.7.2 targeting tools:

```powershell
msbuild WarTransferCL\WarTransferCL.csproj /p:Configuration=Release
```

Output: `WarTransferCL\bin\Release\WarTransferCL.exe`, `WarTransfer.dll`, and the
executable configuration file. Copy MPQEditor beside the executable, pass its path
at runtime, or include an existing installation in the build output with:

```powershell
msbuild WarTransferCL\WarTransferCL.csproj /p:Configuration=Release /p:MPQEditorPath=D:\Tools\MPQEditor.exe
```

No new NuGet dependencies are required for the CLI. Both Debug and Release solution
builds include it. The project no longer uses the abandoned ClickOnce setup.

`Tests/CommandLineTests.cs` covers parsing, preflight, JSON, overwrite policy,
workflow errors, process failure propagation, cancellation, and execution from
another working directory. For the real archive integration test, set
`WARTRANSFER_TEST_MPQ_EDITOR` to an existing MPQEditor executable before running
the MSTest suite. Without it, that one test is marked inconclusive. Tests use
synthetic archives and version 39 metadata; they do not claim in-game validation.
