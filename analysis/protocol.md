# Recovered Arena transport

Inspected the supplied managed assemblies locally with ILSpy. No official services were contacted and no installed client was modified.

## Confirmed transport

`Wizards.Arena.TcpConnection.dll`: `TcpConnection.ProcessTcpConnected` creates `SslStream`, then authenticates TLS 1.2 to the configured host with certificate revocation checking. The default outgoing frame version is 4. `BufferMessageReader.ProcessRead` and `TcpConnection.SendNextMsg` establish the framing:

| Version | Header |
| --- | --- |
| 1 | version byte, little-endian int32 body length; implicit message type |
| 3 | version byte, type byte, little-endian int32 body length |
| 4 | version byte, packed format/type byte, little-endian int32 body length |

Version 4 packed byte: high nibble serialization format (JSON=1, protobuf=2), low nibble message type (message=1, ping=2, pong=3). Ping and pong bodies are the four-byte tick counter; pong echoes it. Thus protobuf application frames begin `04 21`, JSON ping frames begin `04 12`.

## FrontDoor

`SharedClientCore.dll`: `FrontDoorConnectionAWS.ProtobufSerializeToMsg` sends `Wizards.Arena.Protocol.Cmd`; inbound messages parse `Response`. Models are generated in `Wizards.Arena.Models.Protobuf.dll`.

Cmd fields: command type=1, transaction GUID string=2, protobuf `Any`=3 OR JSON bytes=4, compressed=5. Response fields: transaction GUID string=1, `Any`=2 OR JSON bytes=3, `ServerError`=4, compressed=5. JSON compression is implemented by `Wizards.Arena.MessageSerialization.DtoCompressor`; a real implementation must follow it rather than assume every JSON payload is plain UTF-8.

`AwsAuthServiceWrapper.Authenticate` sends `CmdType.Authenticate` with JSON `AuthenticateReq`: `ClientVersion`, `AcceptedPolicy`, `Token`, `PersonaId`, `PlatformId`. Success DTO `Wizards.Unification.Models.FrontDoor.FrontDoorSessionResp` has `SessionId`, `Attached`, `PlayerConditions`, `PlayerAdvancement`, `ExternalChatAppId`, `LastDetach`. The client checks policy/version conditions and may send `Attach` when another session is attached.

Confirmed FrontDoor command routes include `EventEnterPairing`, `EventLeavePairing`, `CardGetCardSet`, `CurrencyGetCurrencies`, `BoosterGetOwnedBoosters`, `QuestGetQuests`, `EventGetMatchResultReport`. Refreshed installed-client enum export contains 183 values (the original archive had 179). Matching enum IDs alone does not establish that the local backend returns compatible DTOs.

## MatchDoor / GRE

`SharedClientCore.dll`: `GreClient.Network.MatchTcpConnection` serializes `ClientToMatchServiceMessage` and parses `MatchServiceToClientMessage` from `Wizards.MDN.GreProtobuf.dll`. `GREConnection.HandleTcpOpen` sends `AuthenticateRequest` with `ClientId`, `ClientInfo.ClientVersion`, `ClientInfo.ClientType`, `PlayFabSessionTicket`, `InactivityTimeoutMs`. The match response authenticates with `AuthenticateResponse.ClientId`.

`GREConnection.CreateConnectReq` sends `ClientToMatchDoorConnectRequest` containing `ClientToGreMessageBytes`, `MatchId`, `McFabricUri`. Gameplay goes through serialized `ClientToGREMessage` payloads, returned inside `GreToClientEvent.GreToClientMessages`. Match service message IDs observed: connect=1, GRE=2, GRE UI=3, authenticate=4, create room=5, echo=8.

`FrontDoorConnectionAWS.ProcessIncomingMessage` maps `PushNotification.MatchInfoV4` into endpoint host/port, controller fabric URI, match ID, player information and seat. A queue service must send this exact notification to move the stock client into a match.

## Available executable implementation

`protocol-host` is a .NET 10 transport harness referencing locally supplied generated protobuf assemblies. It implements loopback TLS 1.2, all three frame versions, bounded incoming frame lengths, partial reads, ping/pong, request correlation and genuine observed error envelopes. Default authentication returns FrontDoor `FD_AuthFailed` (1004), other commands `MethodNotFound` (-32601), MatchDoor `NotAuthorized` (111).

Optional `--backend-url http://127.0.0.1:8787` validates `AuthenticateReq.Token` against the local backend `/api/me`, checks PersonaId when supplied, and returns the observed session DTO. It only accepts local account tokens; it provides no official authentication or stock-client bootstrap. Redirects are disabled and the CLI restricts the bridge to numeric loopback HTTP addresses. All other FrontDoor commands remain explicitly unsupported. MatchDoor gameplay remains explicitly unsupported.

Run `dotnet run --project protocol-host -- --self-test` for framing, size/truncation checks, typed errors, local authentication bridge, and a real TLS 1.2 fragmented ping/application exchange. Run `dotnet run --project protocol-host -- --export-schema` to regenerate the recovered descriptor set and command inventory.

The separate `--backend-self-test URL` mode registers and logs in through a real companion backend and SQLite database, then authenticates through TLS FrontDoor frames, checking session DTO/correlation and wrong-persona/invalid-token rejection. The Python integration test uses a temporary database and loopback ephemeral HTTP port. This local protocol integration is verified; it does not verify stock-client bootstrap or game compatibility.

## Practical blockers

1. The Windows client first performs account and doorbell bootstrap before FrontDoor authentication. `EnvironmentDescription` stores distinct `accountSystemBaseUri`, `ecoUri`, `doorbellUri`, FrontDoor and MatchDoor host/port settings. The harness does not implement or redirect this bootstrap.
2. Trusted hostname/certificate setup remains necessary for any redirected stock client. Generated harness certificates are for local tests and are not installed into trust stores.
3. Local backend accounts/decks/queues need exact command and DTO adapters before the stock frontend can use them.
4. The authoritative card rules engine and complete GRE state/action translation remain missing. Client UI handlers, bot choice helpers and protobuf contracts do not recover the remote rules engine or all card behavior.
5. Stock-client compatibility has not been demonstrated by a playable game. HTTP account/queue support and diagnostic protocol support must not be described as a complete Arena private server.

## Schema artifacts

`analysis/schemas/arena-descriptor-set.pb` and `.json` contain nine recovered protobuf file descriptors including FrontDoor, MatchService and GRE plus recursive dependencies. The binary is a standard `FileDescriptorSet` usable with protobuf tools; JSON is readable for inspection. `commands.json` preserves all command names and numeric IDs. These come directly from generated descriptors in the supplied assemblies, not guessed message definitions.
