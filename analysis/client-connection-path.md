# Stock Windows client connection path

The current private-server tooling is not connected to the installed Arena client. The local HTTP accounts/decks backend and local TLS protocol harness are separate services. A successful local protocol self-test is not evidence that MTGA.exe selected these services.

This audit inspected the refreshed managed assemblies from client `0.1.14403.1326110` and listed installed StreamingAssets files. It did not modify the installation, launch a redirected client, install certificates, edit the registry or contact official services. Embedded service credentials are intentionally omitted from this document.

## Why the production client does not select localhost

`Wizards.Mtga.Platforms.PlatformContext.GetConfigurationLoader` in Core.dll returns `DefaultConfigurationLoader` on Windows. `DefaultConfigurationLoader.LoadServicesConfigurationImpl` reads exactly `Application.streamingAssetsPath/services.conf`. The installed `C:/Program Files/Wizards of the Coast/MTGA/MTGA_Data/StreamingAssets` directory contained no `services.conf` or other `.conf` file during this audit.

`Core.Shared.Code.Providers.EnvironmentManager.InitializeEnvironment` selects the configured environment matching `MDNPlayerPrefs.PreviousFDServer`, otherwise the first configured environment, otherwise `HardcodedProd`. Without services.conf, production fallback is expected. Launching MTGA.exe normally therefore does not select the local backend or TLS listener.

`ServicesConfigurationConverter.ReadJson` expects an `environments` object keyed by environment name. Optional `$environments` lists active names; when absent it uses all keys. It injects each object key as the environment's `name`. A single explicitly local environment would avoid selecting a previously remembered production entry. `ExpandTinkerbellEnvironments` performs remote discovery when `tinkerbellUri` is provided, so an isolated local configuration should use explicit hosts and omit that property.

## Minimal reversible configuration route

The observed file-based route is an **isolated client copy** containing a local `MTGA_Data/StreamingAssets/services.conf`. The production installation should remain intact. The environment object exposes these relevant fields, confirmed in `EnvironmentDescription`:

- `fdHost`, `fdPort`: FrontDoor TLS endpoint.
- `mdHost`, `mdPort`: debug/default MatchDoor settings; live matches also receive endpoint details through a FrontDoor notification.
- `doorbellUri`: account/content bootstrap selector endpoint.
- `accountSystemBaseUri`: WAS login/profile base URL; must end in `/` because routes are appended directly.
- `ecoUri`: a distinct configuration/service endpoint, not replaced by setting FrontDoor alone.
- `accountSystemId`, `accountSystemSecret`, `accountSystemEnvironment`: local account-service client configuration; use dedicated local placeholders/credentials, never copied production secrets.
- `HostPlatform`: enum `Unknown=0`, `AWS=1`; `accountSystemEnvironment` enum `Prod=0`, `PreProd=1`, `Load=2`.
- `bikeUri`: separate business-event endpoint.

The exact container shape is `{ "environments": { "Local": { ...environment fields... } }, "$environments": ["Local"] }`. This identifies where to redirect a copied client; it is not a working configuration until the contracts below are implemented.

Isolation limits: Unity preferences are not proven to be directory-isolated. `EnvironmentManager.SetEnvironmentByName` writes `MDNPlayerPrefs.PreviousFDServer`; account refresh tokens are also stored in preferences. An isolated executable copy alone does not guarantee isolated registry/account state. This audit did not change those preferences or establish a supported command-line switch that isolates them. Any redirected launch must account for that shared state and use local test credentials.

## Required bootstrap contracts before FrontDoor

### 1. Doorbell and installed content manifests

`Assets.Core.Code.Doorbell.Doorbell.RingDoorbell` performs an HTTP JSON POST to `doorbellUri + /api/ring` or `/api/v2/ring`, selected by the `RingDoorbellV2` feature toggle. Explicit `fdHost` does **not** prevent that request. Debug-only PlayerPrefs override code checks `Debug.isDebugBuild`, so it is not an established retail-client shortcut.

Legacy request fields are `playerId`, `clientVersion`, `environmentKey`, `platformKey`; it also appends a service-code query parameter. V2 request fields are `code`, `environment`, `clientVersion`, `platform`, `playerId`. Service-code literal values are not reproduced here and are unnecessary for a dedicated local stub's authentication policy.

Legacy response fields are `fdURI`, `contentHash`, `preloadContentHashes`, `configurationRoot`. V2 response is `DoorbellRingResponseV2`: `FdURI` and `BundleManifests`. Manifest metadata uses JSON fields `priority`, `category`, `hash`; it derives filenames `Manifest_[category_]hash.mtga`. Correct locally installed manifest hashes and accessible bundle sources must be recovered before providing a response; an empty invented manifest list is not established as sufficient for the client.

`ConnectionManager.RingDoorbell` sets FrontDoor host/port from response FdURI only when configured fdHost is empty. It strips `tcp://` and splits host/port, with fallback port 443. Therefore a local explicit host/port is stable, but successful doorbell/content bootstrap remains necessary.

### 2. Local WAS-compatible login and profile

`WizardsAccountsClient.SetCredentials` calls `WASHTTPClient.Init` using environment account base URL and client identity. Credential login calls `WASHTTPClient.Login`, then retrieves the profile before marking the account fully logged in.

Observed credential login contract:

- POST `accountSystemBaseUri + auth/oauth/token`.
- HTTP Basic client authentication header.
- Form body `grant_type=password`, `username`, `password`.
- Refresh uses the same endpoint with `grant_type=refresh_token`, `refresh_token`.
- JSON response `WAS.LoginResponse` fields: `access_token`, `refresh_token`, `expires_in`, `token_type`, `client_id`, `game_id`, `domain_id`, `persona_id`, `account_id`, `display_name`, `conflictingPersonaID`.
- GET `accountSystemBaseUri + profile` with Bearer access token.
- `Wizards.Platform.Sdk.Model.Profile` JSON fields: `accountID`, `personaID`, `displayName`, `gameID`, `externalID`, `email`, `dataOptIn`, `presenceSettings`, `countryCode`.

Important incompatibility: the current backend `/api/login` returns an opaque local token and does not implement this route/form/response contract. `WizardsAccountsClient.SetAccountCredentialsFromLoginResponse` calls `WAS.JwtHandler.GetRolesFromAccessToken`, which requires three JWT segments and decodes JSON claims; it looks for `wotc-rols`. Email verification parsing reads `wotc-flgs`. Merely returning the existing opaque token as `access_token` will fail client-side parsing. Implement genuine dedicated local JWT issuance/validation and matching local profile/account IDs rather than accepting or imitating production account tokens. This audit does not prescribe guessed claims or fabricate login success.

### 3. FrontDoor TLS authentication

`LoginScene.ConnectToFrontDoor` and `FrontDoorConnectionManager.onAccountLoginSuccess` create `FDCConnectionParams` with environment host/port and `AccountInformation.Credentials.Jwt`. `AwsAuthServiceWrapper` sends recovered FrontDoor `Authenticate` with token/persona/client version/platform/policy fields.

The current protocol host's optional bridge accepts only existing local backend tokens via `/api/me`. That was tested with a custom harness and is not wired to the stock WAS login path. A WAS-compatible local issuer must be integrated with this validator so the token/persona selected by Arena is actually recognized locally.

The client uses TLS 1.2 with host certificate checks and revocation enabled. The harness's generated localhost certificate is not installed in trust stores, so a stock client does not automatically trust it. An isolated trust/validation solution is still required. No trust-store, certificate-validation patch or registry modification was made during this audit.

## Authentication does not finish frontend startup

`WrapperController.Coroutine_StartupSequence` waits for FrontDoor connection, then calls StartHook and static content, processes the returned initialization data and fans out additional requests. Observed startup work includes card inventory, ranks/seasons, renewal, entitlements, deck list, preconstructed decks, player preferences, play-blade configuration, net-deck folder, voucher definitions, inbox, WPN notification preferences, prize wall, preferred printings and challenge reconnect.

The current diagnostic host returns explicit unsupported errors for these commands. Therefore even a valid TLS handshake and local authentication will not currently load the Arena frontend into a usable state. Exact returned DTOs must be mapped from observed wrappers before implementing each required startup response.

Live match entry then additionally requires FrontDoor queue/direct-challenge commands and a genuine `PushNotification.MatchInfoV4` containing MatchDoor endpoint, fabric URI, match ID, player/seat info. The MatchDoor host currently rejects authentication; the Forge snapshot encoder produces files and has no stock-client request/response session. Bidirectional Forge decision-to-GRE prompts remain necessary for gameplay.

## Observable milestones and present status

| Milestone | Evidence needed | Present status |
| --- | --- | --- |
| Local backend listening | loopback HTTP health + local account tests | implemented |
| FrontDoor transport listening | loopback TLS port + host framing tests | implemented |
| Copied client selects local environment | client log confirms services.conf and local URLs | not verified |
| Local doorbell/content bootstrap | successful client manifest/content load | unimplemented |
| Local WAS login/profile | stock client obtains genuine local account/token | unimplemented |
| Stock client FrontDoor auth | correlated local Authenticate request and accepted session | not observed |
| Arena startup screens load | required startup DTOs accepted | unimplemented |
| Stock client enters live Forge match | MatchDoor handshake, state and decision responses | unimplemented |

Production client display text such as “Waiting for Server” does not establish any of these local connection milestones. Listener availability and custom integration tests likewise do not establish that the running MTGA client contacted them.

## Next implementation boundary

The next concrete work is to recover installed manifest hashes and required startup DTO shapes, implement a dedicated local WAS issuer/profile plus doorbell adapter with tests against these exact recovered contracts, and prepare a reviewed isolated services configuration. Only then should an isolated client launch be used to prove which local request arrives first and advance through FrontDoor startup. TLS trust and shared preference handling must be resolved explicitly for that launch. The existing generated schemas and Forge integration reduce later work; they do not replace this bootstrap path.

## Protocol-side implementation prepared after this audit

The protocol host now compares authentication PersonaId with `/api/me.persona_id` when provided, falling back to the earlier integer identity for standalone local tests. A `--was-backend-self-test URL` mode submits the actual form/Basic WAS request and verifies JWT/profile identity plus TLS authentication against a disposable local backend. This requires the new bootstrap HTTP routes to be integrated before it can pass; it does not pretend to be MTGA.exe.

`--create-local-cert PREFIX` produces a seven-day localhost-only TLS server leaf (SAN localhost and 127.0.0.1, CA=false, server-auth EKU), encrypted PFX, CER, random password file and thumbprint/expiry metadata. The command installs no trust. `--trusted-cert-self-test PFX` uses default OS certificate validation with online revocation checking, then exchanges TLS ping/pong without any validation bypass. These modes support root-managed reversible trust setup without intercepting official hosts.

Exact protobuf StartHook request/response and recursive inventory/deck/graph/reward dependencies have been exported to `analysis/startup-schemas/arena-descriptor-set.json` and `.pb` (61 protobuf file descriptors). The main protocol schema export remains nine transport/GRE descriptor files. No empty invented startup responses were added to the protocol host.

## Local deckbuilder adapters (2026-10-03)

The running stock client has now authenticated against the locally trusted TLS endpoint. `LocalStartup` binds each accepted token to its connection and rechecks `/api/me` before supported account commands. The per-connection deadline resets after every frame, so pings keep an editing session alive. Unsupported commands still return explicit errors.

An empty graph list caused a confirmed startup deadlock: `CampaignGraphManager.Initialized` requires a nonempty definitions map, and `NodeNpeStrategy` specifically requires `NPE_Tutorial`. The source-backed fixture now supplies a synthetic completed local tutorial graph. This indicates only the local account's skipped onboarding; it provides no official campaign content or rewards. Unknown graph identifiers receive NotFound.

StartHook uses the recovered protobuf response and account-bound persistent deck summaries/bodies. Deck and collection requests proxy the native JSON DTOs to `/api/arena/command`. Other recovered startup JSON contracts are explicitly listed in `analysis/startup-fixtures.json`; format data is loaded from `analysis/deckbuilder-formats.json` when present. TLS integration against an ephemeral backend verifies the nonempty graph, completed graph state, typed StartHook, collection response and deck summaries. Actual stock deckbuilder loading and create/edit/save behavior still require the parent task's live UI verification; transport tests alone do not prove those screens work.

`GetFormats` differs from the legacy JSON fixtures: `AwsFormatsServiceWrapper.GetFormatData` supplies a generated `GetFormatsReq`, selecting `FrontDoorConnectionAWS.SendMessage<T>(CmdType, IMessage)` and requiring a packed protobuf `GetFormatsResponse`. The host converts its local fixture through recovered descriptors (including capitalized internal field names), rejects unknown fields and packs the exact generated response. TLS verification checks nonempty format lists, legal-set codes and groups. These are explicitly local sandbox rules, not official Historic legality.
