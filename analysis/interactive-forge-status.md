# Interactive Forge bridge

The requested target is the stock Arena match screen. Forge's own window is not the target.

`engine-bridge/ForgeInteractiveSession.java` embeds a real Forge human controller and AI opponent through `ProtocolGuiGame`/`IRemote`. It runs in the existing local JSON-lines subprocess. Operations are `interactiveStart`, `interactivePoll`, `interactiveAnswer`, `interactiveAction`, and `interactiveClose`. These are internal development operations, not authenticated HTTP routes or an Arena protocol implementation.

Explicit answers support boolean confirmations, option/ability indices, bounded distinct choice indices, and text input. Other prompt types produce an explicit adapter-unavailable error. Prompts remain pending rather than silently selecting for the human. Actions support selecting an own-hand/public-battlefield card, OK, cancel, and concede. Button event IDs reject stale or repeated actions. Only one interactive session is supported per process, and simulations are refused while it owns Forge state.

Validation: compile both Java files using the configured Forge classpath, then run `python engine-bridge/test_interactive.py`. This starts a real human-versus-AI game, accepts its prompts, selects a Plains from the human hand, verifies a land on the battlefield, passes priority, verifies turn three after the AI turn, then concedes. It does not launch or validate Arena.

Remaining before Arena play: account/deck-bound AI matchmaking; exact FrontDoor queue/course responses and MatchInfoV4 notification; MatchDoor authentication/connect lifecycle; ordered authoritative state snapshots with Arena printing identities; GRE prompt/action translation including mulligans, abilities, targets, mana, combat, ordering, and game results. The existing state encoder remains a prototype. No playable Arena capability is enabled by this change.

The event stream contains internal Forge objects rendered as diagnostic descriptions. Keep it inside the trusted local engine boundary; it is not sanitized for a remote player. Before any client-facing transport, replace diagnostic descriptions with explicit viewer-scoped data and identifiers.
