# Arena-only cards

Updated 2026-10-20 for Arena 0.1.14589.1332174.

## Installed coverage

All **799 Arena digital/rebalanced Oracle entries** in the current inventory have installed display records and available rules. This includes linked alternate forms; it does not mean 799 separately deck-buildable cards. There are **zero entries missing rules**. The 109 entries without a separate exact rules name are linked faces/forms, not missing implementations.

Nine missing Hag conjure targets and their artwork are installed. Existing printing IDs and linked faces are preserved. The local display pack contains 16,624 printings. Format legality still controls which cards can go into a deck.

The remaining 26 rules entries were implemented after the initial nine additions. There are now 39 reviewed local card scripts, including the four already present before this request. They use Forge's existing digital actions and scoped extensions for starting-deck history, lifetime life gain, random conjuring, loyalty adjustments, drafted abilities, and perpetual trigger removal.

## Mechanics and data

- Original card identities are tracked per player across zone changes. Conjured cards, prepared spell copies, and cards from another player's starting deck can satisfy actualize.
- Random effects draw from a dated list of released, native Arena card identities and require an available rules implementation. Ordinary custom-imported paper cards are excluded from that pool.
- Mystical Archive drafts directly into face-down exile. Its restricted mana can be split among colors.
- Enter the Eternities uses all 30 abilities from the installed client's template 108029, drafting three options at each of its three loyalty costs. Tokens keep the exiled creature's name, colors, and power-based loyalty.
- Remnants of Beleren can redirect empower counters to a Jace you control.
- Valgavoth's finality effect follows the cast permission used, rather than applying to every creature cast from a graveyard.
- Arena rebalances retain their own names and rules. Paper substitutions and text-only rules placeholders are not used.

Spellbook references: [Wizards' Alchemy: Strixhaven spellbooks](https://magic.wizards.com/en/news/mtg-arena/alchemy-strixhaven-spellbooks) and [Wizards' Alchemy: Reality Fracture spellbook](https://magic.wizards.com/en/news/mtg-arena/alchemy-reality-fracture-spellbook).

## Verification

`python tools/check_arena_digital_rules.py` uses a separate Forge game. It checks the entire current inventory can instantiate, all 30 Eternities draft abilities parse, and focused interactions covering perpetual evoke, starting-deck identity, actualize history, lifetime life gain, Flourishing Forces, Runeblade Raiser, Opulent Clomper, random artifact selection, face-down drafting, Eternities token creation, Prophet's loyalty adjustment, and Valgavoth's cast-permission/finality behavior.

**Full gameplay and native visual equivalence for every interaction remain unverified.** The support report deliberately keeps `verifiedAllCards: false`; lookup coverage and focused checks are not a claim that every combination has been played.

## Import and refresh

Run **Import-Arena-Only-Cards.bat** with Arena closed to install future compatible additions. It reuses cached artwork and does not duplicate linked forms or flatten multiface cards. Restart the local server after installing cards or changing rules.

Rebuild the inventory with `python tools/build_format_policies.py`, then `python tools/audit_arena_digital_cards.py`. Read `data/arena-digital-support.json` or the authenticated `/api/card-support` endpoint for per-card details. Re-run the focused checker after changing the engine or rules.
