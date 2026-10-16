# Rebuild the animation mapping inventory

Double-click `Map-Arena-Animations.bat` in the project root. It uses the existing local ILSpy tool and reads the installed SharedClientCore.dll and Core.dll. It does not change the game installation or contact official servers.

Outputs:

- `animation-map.md`: a readable list of protocol annotations, detail-key candidates, discovered visual paths and whether our bridge explicitly emits them.
- `animation-map.json`: machine-readable mappings, source paths, audio/animation references, DLL hashes, local resource-file inventory and any failures.
- `sources/`: decompiled annotation parsers, event translators and referenced playback classes, cached by DLL hash.

The installed build produced 111 annotation/parser relationships, 77 referenced playback classes and 27,905 resource-file entries. Tap, damage, zone-transfer and resolution playback paths were checked against the generated report. These counts are an inventory, not a count of unique animations.

The script automates discovery of static references. It does not generate or install gameplay adapters. Dynamic asset lookups, bundle contents, branches and event sequencing still require inspection and gameplay tests. Forge must supply the correct event source, targets, damage, ability IDs and timing; inventing those from visual names would produce incorrect gameplay. Some visuals are generated from state differences, so absence of an explicit annotation does not prove a missing feature.

Run again after an Arena update; the new DLL hashes invalidate the source cache. Use `python tools/map_arena_animations.py --managed "PATH/TO/MTGA_Data/Managed"` for another installation.

The discovered contracts now drive ordered Forge adapters for damage, spell resolution, shuffles, taps and zone moves. See `ADAPTERS.md` for implementation, validation and remaining gaps.
