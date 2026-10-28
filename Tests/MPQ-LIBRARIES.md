# MPQ library investigation

Investigated locally on 2026-10-28. **StormLib is the preferred first backend
based on the measured stream reads and overlay writes.** WarCode has a usable
managed MPQ library, but adopting its wrapper directly requires runtime changes
and an improvement to lookup of entries missing from the listfile. No shipping
archive code or dependencies changed during this investigation.

The user explicitly accepts upgrading .NET whenever the benefit justifies the
work. Keeping the current runtime is a convenience, not a project constraint.
Evaluate managed library reuse and runtime modernization alongside performance,
compatibility, and native binary distribution when making the backend choice.

## What WarCode uses

The local checkout at `D:\Dev\WC3 Modding\WarCode` contains:

- `src/WarCode.Mpq`: reusable `IMpqBackend` / `IMpqArchive` contracts and a test
  backend.
- `src/WarCode.Mpq.Backends.Native`: an adapter over **War3Net.IO.Mpq 6.0.1**.
  Despite its name, this adapter uses managed War3Net code, not StormLib.
- `src/WarCode.Mpq.Cli`: a thin diagnostics CLI over the same library.

Both library projects target .NET 8. War3Net 6.0.1 itself targets .NET 5.
WarTransfer targets .NET Framework 4.7, its CLI targets 4.7.2, and its GUI targets
.NET 6. Neither the WarCode wrapper nor this War3Net package can be referenced
directly by the current core. Reuse would require retargeting/multitargeting
projects or a separate helper process. A helper still permits coarse operations,
but would not give the existing process direct archive streams.

WarCode's native adapter reads entries into `byte[]`, including intermediate
memory copies. It constructs a builder even for read-only access. `SaveAsync`
rebuilds to a sibling temporary archive, replaces the old file, and reloads it.
This is a useful transactional design, but it is not an in-place overlay.
War3Net can reuse compressed entry bytes when flags, block size, and encryption
offset constraints permit; rebuilding does not imply recompressing every asset.
See the upstream [builder](https://github.com/Drake53/War3Net/blob/master/src/War3Net.IO.Mpq/MpqArchiveBuilder.cs)
and [entry-writing implementation](https://github.com/Drake53/War3Net/blob/master/src/War3Net.IO.Mpq/MpqFile.cs).

As checked on the investigation date, the current MPQ package is
[War3Net.IO.Mpq 6.0.3](https://www.nuget.org/packages/War3Net.IO.Mpq/), with .NET 6
and .NET 10 assets. The local 6.0.1 results below are not measurements of 6.0.3.
WarCode's existing dependency notices cover 6.0.1 and its DotNetZip dependency;
review the actual dependency graph and notices when choosing a shipping version.

## Measured archive operations

A disposable .NET 8 Release harness compared the local WarCode binaries,
War3Net directly, the existing MPQEditor executable, and StormLib through P/Invoke.
The StormLib DLL was linked from the local **9.23** x64 Release static library
under `C:\Users\Andrew Hile\source\repos\StormLib-master`.

Two private real-map copies were used: Searing Gorge (20.6 MB, 312 content
entries) and Spellbringers (28.1 MB, 1,194 content entries). Both are bare MPQ
archives. Archive metadata entries `(listfile)`, `(attributes)`, and `(signature)`
were excluded from content comparisons.

Mean seconds to open, enumerate, read and SHA-256 every content entry:

| Map | MPQEditor extraction + hashing files | WarCode wrapper | War3Net streams | StormLib reads |
| --- | ---: | ---: | ---: | ---: |
| Searing Gorge | 1.182 | 0.276 | 0.300 | 0.179 |
| Spellbringers | 3.525 | 0.531 | 0.513 | 0.346 |

StormLib was about 6.6x and 10.2x faster for this operation. WarCode was about
4.3x and 6.6x faster. These gains include avoiding extraction to temporary files;
they do not isolate process-launch overhead.

Mean seconds to produce a modified archive, replacing only `war3map.wts` with
its original bytes plus a newline:

| Map | MPQEditor copy + overlay | WarCode copy + save | War3Net builder | StormLib copy + overlay |
| --- | ---: | ---: | ---: | ---: |
| Searing Gorge | 0.052 | 0.266 | 0.249 | 0.009 |
| Spellbringers | 0.080 | 0.554 | 0.488 | 0.026 |

The direct builder writes a new output from the open source. The other save
paths include copying the source first. WarCode also includes transactional
replacement and reload. Save timings exclude verification; every one of the
32 generated outputs was subsequently extracted by MPQEditor and checked for
the exact expected content file set and SHA-256 values. Source copies and both
original real maps retained their hashes.

Each operation had one discarded warmup followed by three measured samples.
Runs used warm filesystem caches, fixed backend order, and existing local
library builds. Extraction/hash timings exclude deletion of extracted files.
These are exploratory operation measurements, not end-to-end transfer speedups
or a controlled cold-cache comparison. Different storage and asset mixes may
change the results. Private harness source, assembly hashes, individual timings,
and reports are retained under ignored `TestResults/mpq-spike-20261028/`.

## Compatibility findings

- WarCode successfully read and saved both real maps with all content bytes
  preserved except the intentional WTS edit.
- A generated archive without a listfile exposed a wrapper limitation:
  `ReadEntryAsync("war3map.lua")` throws `MpqEntryNotFoundException`, although
  direct War3Net and StormLib hash lookups find the entry. The wrapper indexes
  discovered filenames and synthetic unknown aliases instead of falling back
  to lookup by the requested name. It needs known-name probing or fallback lookup.
- Synthetic save checks preserved an unnamed asset both unencrypted and with
  compression, encryption, and an offset-adjusted key. Adding 100 named entries
  after an initial WarCode save also succeeded. These checks cover those fixtures,
  not every encrypted/protected archive layout.
- A separately compiled CLR 4 x64 executable successfully called StormLib and
  read scripts from the real Lua map and both listfile-free fixtures. This
  confirms a native adapter can run in the existing Framework runtime.
- No mixed-locale, Warcraft HM3W/user-data preamble, unusual MPQ version, damaged
  archive, cancellation, or failed-publication acceptance was established here.
  Existing transfer tests must be run against any new backend.

## Integration recommendation

Use a small archive boundary around entry existence, opening a read stream,
enumeration with stable entry identity, and applying a change set to a disposable
candidate. Keep transfer ownership rules in WarTransfer and archive mechanics in
the backend. Keep MPQEditor available as an explicitly selected fallback while
validating the new backend.

1. Implement a StormLib backend with safe native handles and checked errors.
   Read needed script/terrain/metadata entries directly. Verify output through
   streams, retaining complete content checks without writing extracted copies.
2. Replace per-destination full source-directory copies with an immutable source
   and a manifest of changed/added/deleted entries. Hash unchanged source content
   once per batch and compare output contents against that baseline plus changes.
3. Copy the original archive to a temporary candidate, overlay changed entries,
   verify, then publish using the existing batch publication rules. Never edit an
   original input. Preserve unnamed entry identity; avoid compaction or resizing
   until relevant encrypted/unnamed cases are covered. Define preamble and
   signature handling explicitly.
4. Run the existing archive/workflow suite and real-map acceptance corpus against
   both backends, then repeat the complete transfer benchmark. The prior corpus
   spent only about 35% of wall time in MPQEditor, so removing process launches
   alone cannot explain or eliminate the remaining cost.

StormLib requires packaged native binaries matching process architecture, native
interop code, and reproducible builds. The tested local 9.23 library is old;
select a pinned current release and repeat validation before shipping. Upstream
has [Windows build instructions](https://github.com/ladislav-zezula/StormLib#readme)
and an [MIT license](https://github.com/ladislav-zezula/StormLib/blob/master/LICENSE).

WarCode/War3Net is a credible managed alternative, and a worthwhile .NET upgrade
is acceptable to the user. Prefer improving the shared library with stream reads,
known-name lookup, and configurable save behavior over copying its wrapper into
this repository. StormLib remains the leading performance candidate in this
investigation; the final choice should include the work needed to distribute
native binaries versus upgrading .NET and improving the shared managed library.
