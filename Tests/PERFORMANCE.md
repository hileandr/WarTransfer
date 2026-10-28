# Measurements and implemented changes

Measured locally on 2026-10-27 using `Tests/Benchmark.ps1`, the Debug CLI, two
destination archives, four deterministic 4 MiB random assets, and 512 small files
per archive. Each variant ran three times with archive verification enabled.
Numbers include process startup, extraction, merging, packing, verification,
publication, and cleanup. This synthetic workload is not a gameplay fixture.

| Variant | Mean seconds | Samples, seconds |
| --- | ---: | --- |
| Starting implementation | 10.41 | 10.09, 10.06, 11.07 |
| Compressed source reuse, full destination extraction | 10.78 | 11.16, 10.09, 11.07 |
| Source reuse plus selective destination extraction | 8.09 | 8.14, 8.07, 8.07 |

The combined change is about 22% faster here. Source reuse alone showed no clear
gain on this random-byte workload. Destination filtering produced the measurable
improvement. Real maps with compressible imports may behave differently.

In the final variant, seven MPQEditor calls consumed 3.75, 3.64, and 3.74 seconds.
These durations include native file I/O and decompression, not just process launch.
The remaining wall time includes managed copies, hashing, filesystem traversal,
publication, cleanup, and benchmark/process-wait overhead; it is not all script
processing. Logs include individual native command durations under `--verbose`.

Implemented improvements:

- Extract only root `war3map*` entries from destination archives. Those include
  every terrain/metadata/script file consumed by the existing workflow. Imports
  and object data still come from the source map. Source extraction and output
  verification remain complete.
- Reuse a bare MPQ source archive's compressed entries when its file set is unchanged,
  overlaying only modified files. Added/removed named entries use a fresh archive
  to avoid source hash-table capacity issues. Unnamed entries retain the existing
  conservative overlay path and cannot be modified. Named archives with Warcraft
  HM3W/user-data preambles retain the existing rebuild path, avoiding stale header
  names/player counts. Header rewriting needs real editor-map fixtures. All paths
  verify output bytes.
- Avoid extracting the source twice when it is also in the target directory.
- Remove repeated expression compilation when remapping W3I string properties.
- Avoid sorting WTS keys merely to find the next unused key.

Run `./Tests/Benchmark.ps1 -Label current` to reproduce the workload. JSON reports,
logs, and generated input/output data are retained under ignored `TestResults`.
The benchmark has no timing assertions in the test suite.

# Next performance opportunities

The [MPQ library investigation](MPQ-LIBRARIES.md) compares WarCode's managed
backend, War3Net, and direct StormLib access on two private real maps. It records
operation timings, runtime constraints, compatibility findings, and a proposed
integration path. These are not end-to-end transfer speedup measurements.

1. **Read/write archive entries through an in-process backend.** A backend such as
   [StormLib](https://github.com/ladislav-zezula/StormLib) could keep archives open,
   read terrain entries directly, overlay changed entries, and verify by streaming
   decompressed data into hashes. Avoiding extraction/re-extraction and thousands
   of temporary files is likely more valuable than removing process launches alone.
   Encrypted/unnamed entries, capacity, and original Warcraft map headers need tests
   before switching the shipping backend.
2. **Avoid full source-directory copies per destination.** Stage only changed files
   over an immutable source archive. This also avoids hashing every unchanged
   asset to rediscover that it is unchanged. It needs a change manifest and exact
   file-set verification, rather than relying on timestamps or disabling checks.
3. **Cache parsed source metadata/scripts with independent per-map copies.** The
   current merger mutates documents and WTS tables, so sharing them directly would
   leak state between outputs. Measure this with large scripts and many targets.
4. **Consider bounded concurrency after state isolation.** `VersionUtility` still
   has mutable static state. Parallel transfers in the same process require that
   state to become invocation-local, plus independent handlers and documents.
   Disk contention can erase gains; benchmark on the actual storage first.

# Real-map acceptance measurements

A local Spellbringers corpus was transferred with destination cameras, regions,
and random tables retained: 16 archived destinations and six extracted-folder
destinations packed into private archives. Duplicate terrain versions remained
separate cases. All 22 outputs passed pjass, source-asset and gameplay-function
preservation, destination-terrain preservation, and resolved metadata checks.
The destinations cover W3I formats 31 (20 maps) and 33 (two maps), with two to
eight players. SHA-256 checks found no changes to the 4,925 original files.

| Batch | Transfer wall time | MPQEditor time | Of which output verification |
| --- | ---: | ---: | ---: |
| 16 archived destinations | 267.75 s | 93.85 s | 64.84 s |
| Six packed-folder destinations | 84.72 s | 29.89 s | 18.27 s |

These are two batches, each including source extraction. Folder preparation and
the separate acceptance audit are excluded. Wall times were recorded by the
native process wrapper. This is an acceptance workload without a
before/after baseline or repeated timing samples, not a speedup measurement.

MPQEditor consumed about 35% of the combined transfer time; verification was
about two thirds of that native time. These durations include archive I/O and
decompression. The remaining time needs profiling across directory copies,
file enumeration/hashing, parsing/merging, publication, and cleanup. Avoiding
repeated copies and hashes of unchanged source assets is a useful next target;
removing native process launches alone does not address all the measured work.
Keep full content verification when changing either path.

All 18 historical outputs also passed pjass. Resolved metadata matched them in
18 of the 22 cases. Four terrain versions had expected metadata differences
while still matching their supplied destinations. The source postdates those
historical outputs and has script/object/string changes, so historical archive
byte equality is not an acceptance requirement. `RealMaps.ps1` records those
differences separately. Private maps, extracted data, and detailed reports remain
under ignored `TestResults`; see [the acceptance harness instructions](README.md).

Disposable copies of King of the Hill, Fighting Spirit, and Ivory Gardens also
opened, saved, and reopened in World Editor. All three editor-saved scripts
passed pjass. An editor-launched King of the Hill test reached creed selection;
choosing Order spawned a selectable Citadel and five workers. This was the
editor's temporary test map with one human and one computer. Direct gameplay
checks of the untouched generated archives remain pending. Foreground tests
were stopped because they interrupted the user's desktop; further interactive
checks need an isolated desktop/session or VM. Audible sound, other creeds and
spells, combat, long-running play, and multiplayer have not been checked.

# Validation that needs your maps or environment

- Additional Lua maps, especially nonempty cameras/regions and scripts using
  frameworks or wrapped entrypoints. The first real Lua transfer now passes
  input/output invariants: a format-33, two-player source merged into Lua-saved
  Turtle Rock (format 39, four players). The Release CLI took 6.722 seconds for
  this single run, including output verification; this is not a repeated benchmark.
  All 312 output content entries, script ownership, metadata, WTS references,
  and independent Lua syntax checks passed. There is no known-good output or
  in-game validation for this pair. Details are in [the test notes](README.md#lua-transfers).
- Storage characteristics (local SSD, HDD, network, synced) for a controlled
  performance comparison; the real-map measurements above have no old-version
  baseline.
- Older supported editor formats (especially 18/25/28), maps with unnamed or
  encrypted imports, and camera/region/random-table combinations you depend on.
  Synthetic fixtures cover mechanics, but cannot establish your intended gameplay.
- Complete in-game smoke checks on generated maps: initialization,
  player slots, placed units, cameras, regions, random tables, sounds, and triggers.
- If full integration should run in hosted CI: a Windows runner with MPQEditor and
  the game/compiler libraries, or an approved way to provision those dependencies.
  The repository currently automates the unit subset without redistributing them.

The local cached 1.5 MB compiled map script passed a lossless text round trip
(apart from newline normalization) and pjass with its local libraries. That is
additional parser coverage; the real-map acceptance checks above exercise full
archive transfers but still do not establish in-game behavior.
