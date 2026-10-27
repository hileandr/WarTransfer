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

# Validation that needs your maps or environment

- Paths to one representative source map and two or three terrain maps you
  actually use together, preferably with a known-good generated output. No upload
  is needed for files already on this machine. Identify the intended pairing.
- At least one asset-heavy source and a realistic target batch for performance
  measurements, plus the usual storage location (local SSD, HDD, network, synced).
- Older supported editor formats (especially 18/25/28), maps with unnamed or
  encrypted imports, and camera/region/random-table combinations you depend on.
  Synthetic fixtures cover mechanics, but cannot establish your intended gameplay.
- World Editor reopen/save and in-game smoke checks on generated maps: initialization,
  player slots, placed units, cameras, regions, random tables, sounds, and triggers.
- If full integration should run in hosted CI: a Windows runner with MPQEditor and
  the game/compiler libraries, or an approved way to provision those dependencies.
  The repository currently automates the unit subset without redistributing them.

The local cached 1.5 MB compiled map script passed a lossless text round trip
(apart from newline normalization) and pjass with its local libraries. That is
useful parser coverage, not an end-to-end transfer test of a real map archive.
