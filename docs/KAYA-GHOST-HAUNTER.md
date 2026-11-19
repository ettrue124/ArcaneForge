# Kaya, Ghost Haunter

2026-10-29. The native deck importer rejected the exact title because this
playtest card was absent from the installed pack. The earlier planeswalker
audit excluded it for missing Forge rules; it was not a spelling alias for
Kaya, Geist Hunter or Kaya, Ghost Assassin.

## Local implementation

- `engine-bridge/cards/kaya_ghost_haunter.txt`: the three loyalty abilities,
  permanent upkeep emblems, and immediate return/cleanup triggers.
- `engine-bridge/overrides/forge/game/ability/effects/HauntEffect.java`: a
  `HauntFromBattlefield` branch for the zero ability. The ordinary Haunt keyword
  path remains unchanged. Confirm the original source and target identities,
  and only mark haunting when exile actually occurs.
- `engine-bridge/overrides/forge/game/card/CardProperty.java`: `Haunted` matches
  the existing Forge haunted-by relationship from any source. `HauntedBy`
  retains its original source-specific meaning.

The damage emblem targets the haunted creature and damages its owner, not its
controller or the creature. It uses DealDamage's RelativeTarget option so the
chosen creature is not accidentally treated as the damage recipient. The
control emblem uses Forge's existing StaticCommandCheck duration pattern
(also used by Rootwater Matriarch), evaluated separately for each target, to
end control when that permanent is no longer haunted.

Source for the card-specific rulings:
[Wizards' Mystery Booster release notes](https://magic.wizards.com/en/news/feature/mystery-booster-release-notes-2019-11-11).
Metadata and artwork use the locally cached Scryfall CMB2 #94 printing.

## Evidence and deployment

Full bridge/override compilation succeeded. The isolated Forge card-name
lookup recognizes the exact card and its rules face. These checks are not
gameplay verification; no tests were added or run.

Stable printing ID: `2016816`. The dedicated candidate manifest in ignored
`data/kaya-ghost-haunter/manifest.json` retains all 16,836 active printings and
adds this card. The existing general import candidate was left intact.
The same directory holds the baseline manifest backup, preparation record,
Forge lookup, installer preflight and installation log.

Installation completed with 16,837 ready printings and zero preflight blockers.
The user closed Arena before installation. `Restart-Arena.ps1` completed in
59.6 seconds and reopened the client. Read-only checks confirmed the exact
title/ID in the installed native database and synchronized catalogue, its
native metadata and activated-ability entry, playtest search registration,
and absence from the unsupported-name list. The client UI deck import and
gameplay have not been exercised automatically; the user can retry import.
