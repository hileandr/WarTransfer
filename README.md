WarTransfer is a tool for Warcraft III modders that automates map data transfers from a source map file to any number of other map files.
I created it for my altered melee mod Spellbringers, since I wanted to transfer all my data to many different terrains.

Supports JASS-to-JASS transfers and initial Lua-to-Lua support for World Editor
generated scripts. JASS/Lua conversion is not supported.
Source script language is checked after extraction, before destination archives
are opened. Every destination is checked before any map is merged. Mixed
languages, unknown script types, and missing scripts fail with an explicit error.
Lua merging uses a Lua 5.3 parser; unsupported entrypoint layouts fail explicitly.
Synthetic Lua scripts pass interpreter and archive/CLI tests; acceptance with
real Lua maps and in-game validation remains outstanding.
See [script transfer development and tests](Tests/README.md) for the language
boundary and regression coverage.

For agent and unattended transfers, use [WarTransferCL](WarTransferCL/README.md).
It provides explicit command-line options, dry runs, JSON results, exit codes, and
isolated temporary directories. Run `WarTransferCL.exe --help` for usage.

Warcraft III 3.0 map information (`war3map.w3i`, format 39) is supported,
including loading-screen crests, extended fog and water settings, and player HUD skins.
Formats 18, 25, 28, 31, 32, and 33 are also supported. Unknown formats are rejected
with their version number instead of being interpreted as an older layout.

Compatibility is covered by byte-for-byte read/write tests against version 39
editor fixtures and regression tests for older camera layouts. Full transfers
still need testing in Warcraft III with the maps being used.
