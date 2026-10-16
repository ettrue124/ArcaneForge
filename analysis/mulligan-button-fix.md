# Mulligan button follow-through

Latest Arena log confirms MulliganResp with decision Mulligan is sent. The prior
bridge had no London bottom-card input handling and reused game-state IDs.

Changes: increment game-state IDs; avoid republishing an unchanged button input;
serialize state/prompt pushes; correlate mulligan and bottom-card responses with
the current GRE request ID. Track human mulligan count in the Forge session.
Map InputLondonMulligan to SelectNReq with the current hand instance IDs and exact
required count. Route selected IDs through an explicit bottomCards operation,
which validates count, uniqueness and own-hand membership before selecting them
on Forge EDT and confirming. Restore mandatory current button-event validation.
Subsequent mulligan prompts report original starting size separately from count.

Forge performs bottom-card selection immediately after EACH redraw, before asking
Keep/Mulligan again. The adapter follows that engine sequence. It does not choose
bottom cards automatically for the user.

Validation: Java compilation passes. engine-bridge/test_interactive.py now passes
two redraws, bottom selection counts [1, 2], keeping, explicit land play and AI turn
progression. protocol-host/bin/MulliganButtonFix builds with zero warnings/errors;
its --self-test passes including installed Arena MulliganRequest construction.
Stock-client button rendering/selection still requires a new-match UI test.
Start-PrivateServer.ps1 rebuilds bin/live when starting a stopped host. Existing
running services do not hot reload these changes.

Follow-up: user still saw seven cards after clicking Mulligan. Player.log showed
SelectNReq was sent, but SelectNWorkflowTranslation.OpenInBattlefieldHandBrowser
threw NullReferenceException. Decompiled client code unconditionally accesses
request.Prompt.PromptId. The earlier SelectNReq omitted that display prompt.
CreateBottomCardRequest now supplies both envelope and selection prompts with
verified localization IDs (2428 Choose a card; 193 Choose two cards), plus
OptionContext.Resolution for instance-card selection. Self-test instantiates the
installed Arena SelectNRequest and verifies non-null display prompt, exact count,
and instance-selection classification. Build: bin/MulliganSelectionFix.
This fixes the identified request shape; it is not an on-screen verification.
