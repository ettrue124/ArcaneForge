# VPN multiplayer server

## Current state

Implemented in source and compiled, but not loaded into the running server or verified with two Arena clients:

- One rules process per match, account-owned sessions, separate human interfaces and hidden-card filtering.
- Human queues match only the same event; bot events use independently generated, legal starter decks.
- Standard, Alchemy, Explorer, Pioneer, Historic, Timeless, Modern, Legacy, Vintage, Pauper, Brawl, Standard Brawl, Commander, and unbanned variants. No Bans 60 remains an exact 60-card mode. Other constructed formats permit 60+ cards with up to 15 sideboard cards. Command formats use their own deck sizes.
- Native Arena queue entries and deck-builder format rules, including restricted-card quantities.
- VPN host and client forwarding scripts. Services continue to bind to localhost. Forwarding binds only to the host's Tailscale address; clients forward their localhost ports to that address.

This is an experimental multiplayer implementation. Two-client matchmaking, simultaneous prompts, disconnect recovery, hidden-card behavior, combat and native views still need acceptance checks. No multiplayer gameplay tests have been run.

## Hosting

1. Install [Tailscale for Windows](https://tailscale.com/docs/install/windows), sign in, and invite/share access with the intended players. Tailscale setup requires your external account login; the server does not do this automatically.
2. Finish any active game. Run **Host-VPN-Server.bat**. It checks VPN readiness before restarting Arena and the services, then displays your VPN address. Keep the window open.
3. All players need their own accounts on this server. Email-shaped usernames work with the Arena sign-in screen; these are local server accounts.
4. Run **Export-VPN-Client-Pack.bat** to create `data/exports/Arena-VPN-Client-<version>.zip`. Share this display pack with the players. It includes public trust certificates and matching printing allocations, and excludes the server database, tokens, `.pfx`, `.password`, preferences and account backups.

## Joining

1. Install Tailscale, sign in and accept the host's device/network invitation.
2. Use a matching ArcaneForge source checkout, extract the host's client pack into it, close Arena, and run **Install-VPN-Client.ps1**. It checks the client version and pack hashes, preserves original client files and installs the shared display allocations and host's public certificate. If already configured for another local server, use **Restore-OfficialClient.ps1** first. The host keeps all private certificate keys.
3. Stop your own local services, then run **Join-VPN-Server.bat** and enter the host's `100.x.x.x` VPN address. Keep this window open.
4. Open `http://127.0.0.1:8787` through that forwarding connection and register your own server account. Sign into Arena with it.
5. Choose the same **Players** event on both clients. **Bots** events start an independent AI opponent. Leave a queue through Arena's cancel button.

No router forwarding or public Internet listener is configured. Tailscale must permit the players to reach the host on TCP 8787, 9444 and 9445. See [connecting Tailscale devices](https://tailscale.com/docs/how-to/connect-to-devices).

## Card and format coverage

Format legality is generated from the cached Scryfall Oracle snapshot identified inside `data/format-policies.json` (currently October 18, 2026); it is not a live ban-list subscription. Generate a new snapshot after refreshing the Oracle cache:

```
python tools/build_format_policies.py
python tools/update_multiplayer_formats.py
```

Use Wizards' [banned and restricted list](https://magic.wizards.com/en/banned-restricted-list) as the official reference. Arena's [format announcement](https://magic.wizards.com/en/news/mtg-arena/state-of-the-formats-2026) also describes digital-card format support and the Alchemy transition. Alchemy is retained here because it was explicitly requested.

**Arena-only coverage:** all 799 digital/rebalanced entries in the current inventory now have installed display records and available rules, including linked forms. There are no remaining missing-rules entries. Full gameplay/native visual equivalence is still unverified. See [ARENA-ONLY-CARDS.md](ARENA-ONLY-CARDS.md) for the additions, data sources, and focused checks. Paper substitutions are not applied automatically.

Read `data/arena-digital-support.json`, or authenticated `/api/card-support`, for details. Rebuild its lookup inventory with `python tools/audit_arena_digital_cards.py`. It checks exact names and linked rules forms, not gameplay equivalence.

## API

All play endpoints require the local server bearer session. Clients cannot choose another account's participant index.

- `GET /api/events`
- `POST /api/play/join` with `event` and owned `deckId`
- `GET /api/play/status`
- `POST /api/play/leave`
- `POST /api/play/snapshot`, `/poll`, `/action`, `/answer`

The original single-player interactive API now resolves the signed-in account's room. Anonymous battle access and cross-account deck fallback have been removed. Feature Lab is restricted to the initial local administrator account. The legacy control-panel queue remains a separate simulation lobby; use Arena's Players events for the new live queues.

## Random public Moxfield bot decks

Bot matches now try a random public Moxfield list for the chosen format. The server resolves every card against the installed catalogue and supported rules, validates deck size, copy limits, commander identity and format legality, and rejects incomplete or unavailable lists. It never removes unavailable cards to make a list fit. Commander sideboard suggestions and maybeboards are not played.

Validated source lists are cached in `data/moxfield-bot-decks` and revalidated before reuse. Discovery has a 22-second budget, at least 1.1 seconds between requests, and a backoff after rate limiting or denied access. If no valid list is found, the existing local bot decks remain available. Search samples random pages of public results; it does not sample every deck on Moxfield uniformly. No Moxfield account or private decks are used. The chosen public deck URL appears in the backend log. Per-card gameplay and AI strategy are not certified by deck validation.

## Competitive Commander bots

Commander bots now use only Moxfield lists referenced in the cEDH Decklist Database's `COMPETITIVE` section. The 113 source links are recorded in `data/cedh-bot-pool.json`; every downloaded or cached list still passes the selected format and installed-rules checks. Casual cached lists and starter-deck fallbacks are excluded from Commander selection. If no vetted list can be loaded, matchmaking reports an error. Other formats retain their format-specific selection.

Forge bots use the `ArcaneCompetitive` profile and hybrid simulation safety checks. The profile raises low-cost interaction willingness, recognizes named combo engines, avoids random unfavorable combat trades, and enables alternative Intuition/reanimation logic. Mana-cheating preferences are disabled. Hybrid checks preserve Forge's card-specific heuristics; they do not provide expert cEDH combo planning or certify a win-rate improvement. Complex combos can still be misplayed.
