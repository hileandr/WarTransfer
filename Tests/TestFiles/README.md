The version 39 Warcraft III 3.0 fixtures are unchanged copies from
[inwc3/wc3libs](https://github.com/inwc3/wc3libs/tree/9a11e1f08943f87fdabaf5c4a6c347fa570536e8/src/test/resources/wc3data/W3I):

- `test39.w3i`: `war3map_v3.w3i`
- `test39_filled.w3i`: `war3map_v3_filled.w3i`

Upstream revision: `9a11e1f08943f87fdabaf5c4a6c347fa570536e8`.
Copyright 2016 Water and wc3libs contributors. See [wc3libs-license.txt](wc3libs-license.txt)
for the upstream Apache 2.0 license.

These fixtures exercise the new loading-screen crest, extended fog, water settings,
and player HUD skins. Tests require strict UTF-8 decoding, complete consumption,
byte-identical serialization, and preservation after changing string lengths.
Version 31/32/33 camera tests construct their wire layout independently of the serializer.

Layout references:

- [wc3libs version 39 reader/writer](https://github.com/inwc3/wc3libs/blob/9a11e1f08943f87fdabaf5c4a6c347fa570536e8/src/main/java/net/moonlightflower/wc3libs/bin/app/W3I.java)
- [War3Net camera field ordering](https://github.com/Drake53/War3Net/blob/master/src/War3Net.Build.Core/Serialization/Binary/Info/MapInfo.cs)
