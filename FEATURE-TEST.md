# Feature Test mode

Open http://127.0.0.1:8787/ and use the Feature Test panel.

1. Choose a scenario and click **Use this scenario**.
2. In Arena, start a Local match with any selectable deck.
3. Choose to play first if prompted. Keep your opening hand. The prepared position appears after mulligans.
4. Follow the scenario instructions in the panel.
5. End the game and start another Local match to repeat. Choose **Normal matches** to return to ordinary deck selection.

Selection affects future local matches globally and resets when the server restarts. Saved decks are unchanged. Prepared positions use normal rules after setup, including mana payment, priority, and the AI. Twenty basic lands are provided to the human player.

## Included scenarios

- Ugin ultimate: try choosing zero, one, and several permanents in separate matches.
- Opponent discard: Thoughtseize, Duress, Thought Erasure and a mixed opposing hand.
- Library: scry, surveil, selection, bottom ordering, and search.
- Combat: attacks against a player or planeswalker, blocking, phasing, and life gain.
- Counters: energy, proliferate, counter multipliers, loyalty and an emblem.
- Choices: modes, creature types, cycling and a tapped land.

- Random results: coin flips, a d20, regeneration, control changes and scry.
- Modal double-faced cards: Esika, pathways and spell/land face selection.
- Replacement effects: Doubling Season, landfall counters and temporary stat changes.
- Morph and cloak: public face-down cards, ward and face-up actions.
- Copies: Clone copying Serra Angel, copied art/rules/stats and temporary model changes.
- Multistep effects: scry, surveil, explore, proliferate and connive start/finish playback around real decisions.
- Card models: Wizard Class levels, Scuttlemutt color changes, and Witness Protection type/stat changes.
- Designations: Fleecemane Lion monstrosity, Palace Jailer monarch and Arch of Orazca city blessing.
- Ability changes: Minimus Containment removes Serra Angel's printed abilities and grants Treasure mana.

Real Forge checks and installed Arena message-handler checks passed for random outcomes, replacement counters, regeneration, morph/face-up and Clone. Visible Unity playback still needs live confirmation. Commander tax, sideboarding, timed games and specialized interactions need additional prepared positions; this is not exhaustive feature coverage.
