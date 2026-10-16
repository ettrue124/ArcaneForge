# Gameplay input correction

User observed zero displayed costs, skipped mulligan, unplayable cards, and missing
functions after reaching the Arena board. Root causes in the existing adapter:
all cards in hand were fabricated as cast/play actions; no ManaRequirement fields
were supplied; arbitrary confirm prompts were misclassified as mulligans; the
actual Forge InputConfirmMulligan was not exposed; background polling drained
events then polled a second time when sending state.

Changes: Forge interactivePoll now includes inputType and buttonEventId. The
MatchDoor adapter sends MulliganReq only for InputConfirmMulligan and routes keep/
mulligan to Forge OK/cancel with the current button event ID. Normal actions are
sent only during InputPassPriority. Background state pushes reuse the poll result.
interactiveSnapshot now supplies legalActions using Forge's filtered possible
abilities for the human hand. Cards with multiple abilities are omitted pending
an explicit ability adapter. Land identification comes from Forge's land ability,
not a hardcoded grpId range. Basic printed mana costs map to GRE requirements;
unsupported advanced symbols are omitted instead of represented as free spells.

Important limits: these are base printed costs, not a complete adjusted payment
solution. Nonmana costs, alternative choices, modified costs, targets, payment,
abilities, London bottom-card selection, combat, results, authentication/ownership,
cross-thread snapshot capture and full response correlation remain incomplete.
Do not claim all functions work or a complete Arena game was tested.

Build: protocol-host/bin/GameplayInputFix. Updating bin/live was blocked by the
running .NET host PID 36976 holding its DLL open; that live build is still old.
Compile both Java bridge sources using launch.json classpath at command[6].
ProtocolHost --self-test passes including ordinary cost and unsupported-symbol
checks. test_interactive.py checks real mulligan input exposure, no ordinary legal
actions during mulligan, explicit land play and AI turn progression. No stock
Arena UI test was performed for this patch.
