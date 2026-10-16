# Scry playback adapter

ILSpy recovered ScryEventAnnotationParser and ScryResultUXEvent from the installed client. The parser requires AnnotationType.Scry, affected card IDs, and topIds/bottomIds details. The playback class loads ScryResultData and its SryResultUXEventDataRef, shows the top/bottom counts, and animates cards through the library holder.

The Forge GameAction override records the exact completed scry choice for humans and AI, including remembered choices. ForgePlayback journals this result. BattlePresentation emits the native Scry annotation with the real chosen IDs, their destinations and the source printing when known. No card faces or public reveal permissions are added. Empty choices and unresolved card IDs are omitted.

The generic Forge GameEventScry contains counts only and fires before library movement; those counts cannot safely identify chosen cards. The adapter therefore records the actual decision at the same point rather than inferring IDs from the later library order.

Java compiles with existing warnings. C# builds with zero warnings and errors. An isolated real Forge scenario confirmed native coin, d20 and scry annotations. In-game Unity visuals remain unverified.

Evidence sources:

- sources/ff02a80282f3/GreClient.Rules.Annotations.ScryEventAnnotationParser.cs
- sources/947441eecc9a/Wotc.Mtga.DuelScene.UXEvents.ScryResultUXEvent.cs
- vendor/forge/forge-game/src/main/java/forge/game/GameAction.java: decisions are announced before library moves.

See animation-map.md for 111 annotation/parser relationships and playback-contracts.md for 120 playback classes and their asset/audio references. These inventories do not enumerate every Unity animation clip or cosmetic variation.
