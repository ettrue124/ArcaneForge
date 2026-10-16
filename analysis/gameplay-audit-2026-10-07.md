# Gameplay audit — October 7, 2026

## Changes deployed to the local server

- Generated tokens now resolve to real Arena printings using the installed database's source-to-token links. The exporter contains 1,283 token printings and 3,282 source links. Forge names such as `Treasure Token` map to Arena's `Treasure`. Unambiguous equivalent printings can be used as a fallback; ambiguous token definitions are not substituted.
- Tokens are encoded as native token objects with their printed abilities, current colors and stats. Creation and deletion annotations supply the stock client's token animation events. Treasure mana actions and Food activations are exposed through Forge's actual legal actions.
- Cleanup discard inputs and other anonymous Forge input subclasses now use the named parent input type. The previously empty type prevented Arena from showing the decision and stalled play.
- Counterspell legality includes spell/ability candidates on the stack. Forge stack selections now use native target requests and preserve the original stack objects when answering.
- Blockers can receive multiple attackers, within Forge's capacity. The bridge retains existing assignments and removes changed ones before adding replacements. The native blocker request and response carry every assigned attacker.
- A controller action must finish before the next normal decision is published. Blocking modal/number prompts remain answerable while that action waits. Responses arriving during an action refresh the current request instead of disconnecting Arena.
- Player counters, including energy and poison, are sent as typed persistent counter state. Their additions/removals use native counter events. Modified card colors are sent from Forge; hidden cards have those colors removed from the viewer's state.

## Validation completed

- All 63 Python unit/integration tests passed.
- Java bridge and .NET protocol host compiled successfully.
- The complete protocol self-test passed against the installed Arena assemblies, including real token creation/deletion parsers, stack target and blocker request constructors, and player energy/poison state.
- `test_tokens.py`: real Forge Treasure/Food/Insect creation; real Arena printing IDs and abilities; native creation/deletion events; Treasure sacrifice adds mana; Food sacrifice gains three life. Cleanup discard decisions completed during the same test.
- `test_stack_targets.py`: Negate remains playable with a valid opponent spell on the stack; the native target request points to that exact object; the countered spell reaches its graveyard.
- `test_multiblock.py`: a Spike-Tailed Ceratops blocks two attackers, and both assignments plus capacity survive the native translation.
- `test_fight_x.py`: both target groups for Primal Might and Rabid Bite, X=2 selection/display, buff and lethal damage passed after these changes.
- `test_prompt_games.py`: scry placement, modal choices, divided damage and trample damage allocation passed after these changes.
- The local backend and FrontDoor/MatchDoor were restarted with this build. Arena was launched and observed at the native opening-hand screen of a two-player Commander match. User input was active; further UI testing was left to the user. Treasure's actual on-screen rendering/animation was not visually verified in that match.

## Limits still requiring work

This is not a claim that every Arena feature or animation is complete. Best-of-three sideboarding remains unsupported. Specialized search/replacement/pile/counter browsers use alternate Forge choice workflows. Arbitrary non-card ordering and several card variants need native UI testing. Four-player protocol state has parser tests; four-player battlefield rendering remains unverified. Refer to `analysis/action-inventory/adapter-coverage.md` for the implementation inventory.

Tests launch isolated Forge engines and do not replace the live match or saved user decks. Start a new match after changing bridge classes or restarting the server.
