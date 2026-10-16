# Local Arena protocol harness

Requires .NET SDK 10 and DLLs under `../client-inspection` extracted from the supplied client. There are no NuGet dependencies.

```powershell
dotnet run --project protocol-host -- --self-test
dotnet run --project protocol-host -- --backend-self-test http://127.0.0.1:8787
dotnet run --project protocol-host -- --was-backend-self-test http://127.0.0.1:8787
dotnet run --project protocol-host -- --export-schema
dotnet run --project protocol-host -- --encode-state snapshot.json output-prefix
dotnet run --project protocol-host -- --create-local-cert data/local-client/tls/localhost
dotnet run --project protocol-host -- --trusted-cert-self-test data/local-client/tls/localhost.pfx
dotnet run --project protocol-host -- --export-startup-schema
dotnet run --project protocol-host -- --port 9445
dotnet run --project protocol-host -- --match --port 9444
dotnet run --project protocol-host -- --backend-url http://127.0.0.1:8787
```

Listeners bind to 127.0.0.1 only. Default certificates are generated for localhost and kept in memory. Supply `--pfx path.pfx` and environment variable `ARENA_PFX_PASSWORD` for a configured certificate. No certificates are installed and no game configuration is modified.

Default mode answers pings, decodes requests and returns real protobuf error envelopes. `--backend-url` enables local FrontDoor authentication using an existing account token from the companion HTTP backend, checking PersonaId when provided. Authenticated connections can read typed StartHook initialization, source-backed local startup fixtures, and persistent deck/collection commands through `/api/arena/command`. Match mode supports framing and typed errors only. Credentials and request payloads are never logged.

See `../analysis/protocol.md` for evidence, recovered schema files and missing compatibility work.

`--backend-self-test` creates a unique test account and logs in through the specified local backend, then verifies actual TLS authentication, session DTO, completed local tutorial graph, typed StartHook, collection/deck summaries and rejection of wrong persona/invalid token. Use it against a disposable database; the integrated Python test starts an ephemeral backend and removes its database after completion.

`--encode-state` translates the documented Forge snapshot JSON contract into a recovered GRE full-state envelope, producing protobuf bytes, v4 transport frame and readable protobuf JSON. It sanitizes hidden hand/library identities for one viewer. See `../analysis/forge-gre-mapping.md`; interactive prompts and stock-client rendering remain unverified.

`--create-local-cert` exports a seven-day leaf certificate limited to localhost/127.0.0.1, CA=false, server-auth EKU. Outputs are `.pfx`, `.cer`, `.password` (random private-key password), `.metadata.json` (thumbprint and expiry); choose an ignored data directory and a fresh prefix. It refuses to overwrite targets and performs no trust-store changes. Set `ARENA_PFX_PASSWORD` from the password file and pass `--pfx` to serve that certificate. Do not print the password. The local auth bridge uses `/api/me.persona_id` when available, otherwise the numeric user ID.

`--trusted-cert-self-test` validates a supplied PFX using the operating system's normal localhost certificate checks and online revocation policy, with no validation override, then exchanges a TLS ping/pong. It changes no trust settings; a configured trusted certificate is required.

`--was-backend-self-test` uses a disposable local account to perform the recovered WAS form/Basic login, checks the locally issued JWT/profile identity and validates actual TLS FrontDoor authentication. The Basic test identity is `arena-local:local-only`. It leaves the account in the supplied database, so use an ephemeral backend for tests.

`--export-startup-schema` additionally exports recovered StartHook request/response descriptors and their recursive dependencies, including inventory/deck/graph/reward models, to `analysis/startup-schemas`. These exact contracts are available for implementing startup adapters; the host still rejects unimplemented startup commands.

Startup fixtures are read from `analysis/startup-fixtures.json` and optional `analysis/deckbuilder-formats.json`. GetFormats is a generated protobuf Any response; the other listed fixtures use recovered legacy JSON DTOs. The synthetic completed NPE graph bypasses onboarding for the local account. Sandbox format data is labeled separately from official game legality. Unrecognized commands continue to fail explicitly; gameplay is not implemented by this host.
