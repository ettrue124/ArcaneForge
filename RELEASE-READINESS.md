# Animation feature release readiness

Status: **not ready to publish as full Arena animation parity**.

Automated checks exercise implemented rules and message contracts. They cannot certify rendered Unity effects, audio, cosmetic variants or timing. The latest audit is linked by `analysis/server-checks/latest-report.txt`.

## Confirmed implementation gaps

- Partial non-keyword ability suppression and unmatched granted keyword definitions. Printed suppression followed by mapped ability grants and common keyword removal/grants are now implemented and checked.
- Source-attributed counter transfers and their intermediate playback state.
- Layered and perpetual changes beyond current type/stat/color/copy state.
- Choice-result domains beyond implemented creature types, colors, nonnegative numbers and random outcomes.
- Remaining designations beyond Commander, monstrous, renowned, monarch and city blessing.
- Unmatched invalidating attachment ability identities. Known CantAttach and shared protection keywords now send native removal playback; ordinary type/zone detachment still follows state changes.
- Multistep categories beyond scry, surveil, explore, proliferate and connive.

These require rule-specific source data and checks. Emitting every annotation indiscriminately would produce incorrect gameplay visuals.

## Visual acceptance

Use the Feature Test panel at http://127.0.0.1:8787/. Start a new Local match for each position. Local decoded messages and Forge snapshots are captured automatically under `analysis/arena-traces/local-*`; screenshots or video are needed to compare what was actually displayed.

| Position | Required visible checks | Status |
| --- | --- | --- |
| Ugin ultimate | Zero/one/many choices complete; draws, life gain and battlefield entries play once | Not confirmed |
| Opponent-hand discard | Correct revealed hand; legal selection; selected discard animation | Not confirmed |
| Library choices | Correct scry/surveil destinations; ordering and search; hidden cards stay hidden | Not confirmed |
| Combat and phasing | Player/planeswalker hit targets; blockers; phasing; life gain | Not confirmed |
| Energy, loyalty and proliferate | Counter visuals; loyalty; emblem; unchanged states do not replay | Not confirmed |
| Modes, types and cycling | Readable mode/type choices; cycle versus land play; tapped land entry | Not confirmed |
| Modal double-faced cards | Both Esika faces and spell/land choices; correct art and costs | Not confirmed |
| Random results, regeneration and control | Actual coin/die outcomes; regeneration; temporary control and return | Not confirmed |
| Replacement effects | Replacement source; counter multiplier; landfall and temporary stats | Not confirmed |
| Morph and cloak | Anonymous opponent card; authorized identity; ward; face-up action and animation | Not confirmed |
| Copies | Copied art/rules/stats; no false reverse side; no repeated model transition | Not confirmed |
| Multistep effects | Start/finish visuals and audio for all five categories; decisions and destinations agree | Not confirmed |
| Class/color/type models | Levels; color markers; type/stat changes; abilities disappear and restore | Not confirmed |
| Designations | Monstrous/renowned markers; monarch transfers; city blessing persists | Not confirmed |
| Ability changes | Minimus Containment removes printed abilities and retains the Treasure mana ability; token abilities remain visible | Not confirmed |
| Chosen colors | Caged Sun retains the chosen color and the choice plays once; multiple colors and zero numbers use the correct domain | Not confirmed |
| Temporary stats | Piledriver's attack boost colors power only and clears after combat; Giant Growth colors both stats | Not confirmed |
| Protection removes an Aura | Pacifism detaches and moves to the graveyard after protection from white; the removal effect plays once | Not confirmed |
| Conditional blocking icons | Questing Beast shows the correct restriction tooltip and loses the icon when abilities are removed | Not confirmed |
| Decayed Zombies | Battlefield blocking marker references decayed; card details retain decayed icon; status clears when ability is removed | Not confirmed |
| Life payments | Numeric optional payments show Pay Life; accepting pays the amount, declining preserves life and applies the alternative | Not confirmed |
| Explore browser | Nonlands appear in Explore’s native library/graveyard browser; lands go to hand without that menu | Not confirmed |
| Battlefield arrival order | New land/permanent stacks appear to the right; returning cards, tapped entries and grouped copies retain correct motion | Not confirmed |
| Free casting during resolution | Ur-Sphinx offers the actual card in Arena’s cast/decline browser; decline leaves it in its original zone | Not confirmed |

Also check a complete two-player Commander game, opponent-turn commander tax, four-player targeting/elimination, reconnection, match end, and any advertised sideboarding/timed-game modes.

## Publication decision

Release the full-parity claim only after confirmed gaps are implemented, the fresh-build automated audit passes without skipped checks, and visual acceptance has evidence for the supported client version. Record limitations explicitly for any narrower experimental release. Do not package authentication traces, account data, certificates/private keys, or imported game binaries with a source release.
