using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using Wizards.Arena.Protocol;
using Wizards.Arena.Enums.System;
using Wizards.Arena.Models.Network;
using Wotc.Mtgo.Gre.External.Messaging;

var mode = args.Contains("--match") ? "match" : "frontdoor";
if (args.Contains("--self-test")) { await SelfTest.Run(); await BattlePollTests.Run(); return; }
if (args.Contains("--playback-self-test")) { int i=Array.IndexOf(args,"--playback-self-test"); BattlePlaybackTests.RunFile(args[i+1]); return; }
if (args.Contains("--flow-probe")) { BattlePlaybackTests.Probe(); return; }
if (args.Contains("--backend-self-test")) { int i = Array.IndexOf(args, "--backend-self-test"); if (i + 1 >= args.Length) throw new ArgumentException("Missing test backend URL"); await SelfTest.Backend(args[i + 1]); return; }
if (args.Contains("--was-backend-self-test")) { int i = Array.IndexOf(args, "--was-backend-self-test"); if (i + 1 >= args.Length) throw new ArgumentException("Missing WAS test backend URL"); await SelfTest.Backend(args[i + 1], true); return; }
if (args.Contains("--encode-state")) { int i = Array.IndexOf(args, "--encode-state"); if (i + 1 >= args.Length) throw new ArgumentException("Missing bridge JSON file"); ForgeEncoder.Export(args[i + 1], i + 2 < args.Length ? args[i + 2] : args[i + 1] + ".gre"); return; }
if (args.Contains("--create-local-cert")) { int i = Array.IndexOf(args, "--create-local-cert"); if (i + 1 >= args.Length) throw new ArgumentException("Missing certificate output prefix"); Certificates.Export(args[i + 1]); return; }
if (args.Contains("--trusted-cert-self-test")) { int i = Array.IndexOf(args, "--trusted-cert-self-test"); if (i + 1 >= args.Length) throw new ArgumentException("Missing trusted certificate PFX"); await SelfTest.TrustedCertificate(args[i + 1]); return; }
if (args.Contains("--export-schema")) { int i = Array.IndexOf(args, "--export-schema"); Schema.Export(i + 1 < args.Length ? args[i + 1] : Path.Combine(Environment.CurrentDirectory, "analysis", "schemas")); return; }
if (args.Contains("--export-startup-schema")) { int i = Array.IndexOf(args, "--export-startup-schema"); Schema.Export(i + 1 < args.Length ? args[i + 1] : Path.Combine(Environment.CurrentDirectory, "analysis", "startup-schemas"), true); return; }
if (args.Contains("--inspect-models")) { ModelInspector.Run(); return; }
string Option(string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
var frontdoorPort = int.Parse(Option("--port", "9445"));
var matchPort = int.Parse(Option("--match-port", "9444"));
var backend = Option("--backend-url", "");
if (backend.Length > 0 && (!Uri.TryCreate(backend, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address))) throw new ArgumentException("--backend-url must be a numeric loopback HTTP address");
string pfxPath = Option("--pfx", "");
if (pfxPath.Length > 0) pfxPath = Path.GetFullPath(pfxPath);
string? pfxPassword = Environment.GetEnvironmentVariable("ARENA_PFX_PASSWORD");
if (string.IsNullOrEmpty(pfxPassword) && pfxPath.Length > 0) {
    string passwordFile = Path.ChangeExtension(pfxPath, ".password");
    if (File.Exists(passwordFile)) pfxPassword = File.ReadAllText(passwordFile).Trim();
}
using var cert = Certificates.Load(pfxPath, pfxPassword);
var chain = new X509Certificate2Collection();
string rootPath = pfxPath.Length > 0 ? Path.ChangeExtension(pfxPath, ".root.cer") : "";
if (rootPath.Length > 0 && File.Exists(rootPath)) {
    chain.Add(X509CertificateLoader.LoadCertificateFromFile(rootPath));
    Console.WriteLine($"Loaded TLS chain root: {rootPath}");
} else if (pfxPath.Length > 0) {
    Console.WriteLine($"WARNING: TLS chain root not found at '{rootPath}'");
}

if (mode is "match" or "frontdoor") {
    // Load static catalogues before accepting a game, not during its first
    // granted ability or planeswalker action.
    BattlePresentation.WarmAbilityMetadata();
    try {
        var matchListener = new TcpListener(IPAddress.Loopback, matchPort);
        matchListener.Start();
        Console.WriteLine($"Local match protocol host listening on TLS 127.0.0.1:{matchPort}. Interactive GRE Forge adapter enabled.");
        _ = Task.Run(async () => {
            while (true) {
                var matchClient = await matchListener.AcceptTcpClientAsync();
                _ = MatchDoorHost.Serve(matchClient, cert, backend, chain);
            }
        });
    } catch (SocketException ex) {
        Console.WriteLine($"MatchDoor port {matchPort} could not be bound: {ex.Message}");
    }
}

if (mode == "match") {
    Console.WriteLine("Running in standalone match host mode.");
    await Task.Delay(Timeout.Infinite);
} else {
    try {
        var listener = new TcpListener(IPAddress.Loopback, frontdoorPort);
        listener.Start();
        Console.WriteLine($"Local frontdoor protocol host listening on TLS 127.0.0.1:{frontdoorPort}. Local token authentication: {(backend.Length > 0 ? "enabled" : "unsupported")}. Matchmaker and bot match push notifications enabled.");
        while (true) { var client = await listener.AcceptTcpClientAsync(); _ = Host.Serve(client, cert, "frontdoor", backend, chain); }
    } catch (SocketException) {
        Console.WriteLine($"FrontDoor port {frontdoorPort} is already in use. MatchDoor host remains active on 127.0.0.1:{matchPort}.");
        await Task.Delay(Timeout.Infinite);
    }
}

record Frame(byte Version, byte Type, byte Format, byte[] Body);
static class Schema
{
    public static void Export(string directory, bool includeStartup = false)
    {
        Directory.CreateDirectory(directory);
        var files = new Dictionary<string, Google.Protobuf.Reflection.FileDescriptor>();
        void Visit(Google.Protobuf.Reflection.FileDescriptor file) { if (!files.TryAdd(file.Name, file)) return; foreach (var dep in file.Dependencies) Visit(dep); }
        Visit(Cmd.Descriptor.File); Visit(ClientToMatchServiceMessage.Descriptor.File); Visit(ClientToGREMessage.Descriptor.File); Visit(GREToClientMessage.Descriptor.File);
        if (includeStartup) { Visit(StartHookReq.Descriptor.File); Visit(StartHookResponseV2.Descriptor.File); }
        var set = new Google.Protobuf.Reflection.FileDescriptorSet();
        foreach (var file in files.Values) set.File.Add(Google.Protobuf.Reflection.FileDescriptorProto.Parser.ParseFrom(file.SerializedData));
        File.WriteAllBytes(Path.Combine(directory, "arena-descriptor-set.pb"), set.ToByteArray());
        File.WriteAllText(Path.Combine(directory, "arena-descriptor-set.json"), JsonFormatter.Default.Format(set));
        File.WriteAllText(Path.Combine(directory, "commands.json"), System.Text.Json.JsonSerializer.Serialize(Enum.GetValues<CmdType>().Select(c => new { name = c.ToString(), value = (int)c }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Exported {files.Count} protobuf files and {Enum.GetValues<CmdType>().Length} command mappings to {directory}");
    }
}
static class Frames
{
    public const int MaxBody = 4 * 1024 * 1024;
    public static async Task<Frame?> Read(Stream stream, CancellationToken ct = default)
    {
        byte[] first = new byte[1];
        if (await stream.ReadAsync(first, ct) == 0) return null;
        byte version = first[0];
        if (version is not (1 or 3 or 4)) throw new InvalidDataException("Unsupported frame version");
        byte type = 1, format = 2;
        if (version >= 3) { await stream.ReadExactlyAsync(first, ct); type = (byte)(first[0] & 15); if (version == 4) format = (byte)(first[0] >> 4); }
        if (type is < 1 or > 3 || (version == 4 && format is not (1 or 2))) throw new InvalidDataException("Invalid type/format");
        byte[] length = new byte[4]; await stream.ReadExactlyAsync(length, ct);
        int n = BinaryPrimitives.ReadInt32LittleEndian(length);
        if (n < 0 || n > MaxBody || (type != 1 && n != 4)) throw new InvalidDataException("Invalid frame size");
        byte[] body = new byte[n]; await stream.ReadExactlyAsync(body, ct);
        return new Frame(version, type, format, body);
    }
    public static byte[] Encode(Frame frame)
    {
        int head = frame.Version == 1 ? 5 : 6;
        var result = new byte[head + frame.Body.Length]; result[0] = frame.Version;
        if (head == 6) result[1] = (byte)(frame.Type | (frame.Version == 4 ? frame.Format << 4 : 0));
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(head - 4), frame.Body.Length);
        frame.Body.CopyTo(result, head); return result;
    }
}
static class Diagnostics
{
    public static Frame? Reply(Frame frame, string mode)
    {
        if (frame.Type == 2) return frame with { Type = 3 };
        if (frame.Type == 3) return null;
        if (frame.Format != 2) throw new InvalidDataException("Diagnostic host expects protobuf message bodies");
        byte[] body;
        if (mode == "match")
        {
            var req = ClientToMatchServiceMessage.Parser.ParseFrom(frame.Body);
            Console.WriteLine($"Match request {req.ClientToMatchServiceMessageType}; requestId={req.RequestId}");
            body = new MatchServiceToClientMessage { RequestId = req.RequestId, TransactionId = req.TransactionId,
                Error = new MatchServiceError { ErrorCode = MatchServiceErrorCode.NotAuthorized, ErrorMessage = "Local diagnostic host: match authentication and GRE gameplay are not implemented." } }.ToByteArray();
        }
        else
        {
            var cmd = Cmd.Parser.ParseFrom(frame.Body);
            Console.WriteLine($"FrontDoor command {cmd.Type}; transaction={cmd.RawTransId}");
            body = new Response { RawTransId = cmd.RawTransId, Error = new ServerError {
                Code = cmd.Type == CmdType.Authenticate ? (int)ServerErrors.FD_AuthFailed : (int)ServerErrors.MethodNotFound,
                Message = $"Local diagnostic host: command {cmd.Type} is unsupported; no official credentials are accepted." } }.ToByteArray();
        }
        return new Frame(frame.Version, 1, 2, body);
    }
}
static class Certificates
{
    public static X509Certificate2 Load(string path, string? password)
    {
        if (path.Length > 0) {
            path = Path.GetFullPath(path);
            if (string.IsNullOrEmpty(password)) {
                string passwordFile = Path.ChangeExtension(path, ".password");
                if (File.Exists(passwordFile)) password = File.ReadAllText(passwordFile).Trim();
            }
            return X509CertificateLoader.LoadPkcs12FromFile(path, password);
        }
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        // Unity's bundled TLS verifier needs a real trust anchor, not a
        // self-signed end-entity certificate installed as if it were a root.
        // This certificate is local-only and serves as both root and localhost
        // endpoint for the private development service.
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
        return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }
    public static void Export(string prefix)
    {
        prefix = Path.GetFullPath(prefix);
        foreach (var extension in new[] { ".pfx", ".cer", ".root.cer", ".password", ".metadata.json" }) if (File.Exists(prefix + extension)) throw new IOException("Certificate target exists; choose a fresh output prefix");
        Directory.CreateDirectory(Path.GetDirectoryName(prefix)!);
        string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Arena Local Development Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(365));
        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        serverRequest.CertificateExtensions.Add(san.Build());
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        using var signedLeaf = serverRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(365), RandomNumberGenerator.GetBytes(12));
        using var cert = signedLeaf.CopyWithPrivateKey(serverKey);
        File.WriteAllBytes(prefix + ".pfx", cert.Export(X509ContentType.Pfx, password));
        File.WriteAllBytes(prefix + ".cer", cert.Export(X509ContentType.Cert));
        File.WriteAllBytes(prefix + ".root.cer", root.Export(X509ContentType.Cert));
        File.WriteAllText(prefix + ".password", password);
        File.WriteAllText(prefix + ".metadata.json", System.Text.Json.JsonSerializer.Serialize(new {
            subject = cert.Subject, thumbprint = cert.Thumbprint, notBeforeUtc = cert.NotBefore.ToUniversalTime(), notAfterUtc = cert.NotAfter.ToUniversalTime(),
            subjectAlternativeNames = new[] { "localhost", "127.0.0.1" }, certificateAuthority = false, issuer = root.Subject,
            rootThumbprint = root.Thumbprint, rootPath = prefix + ".root.cer", enhancedKeyUsage = "TLS server authentication",
            pfxPath = prefix + ".pfx", certificatePath = prefix + ".cer", passwordPath = prefix + ".password", trustInstalled = false
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Created seven-day localhost TLS leaf and one-year local root: {prefix}.metadata.json (no trust changes performed).");
    }
}
static class Host
{
    public static async Task Serve(TcpClient client, X509Certificate2 cert, string mode, string backend = "", X509Certificate2Collection? extraCertificates = null)
    {
        if (mode == "match") {
            await MatchDoorHost.Serve(client, cert, backend, extraCertificates);
            return;
        }
        using (client)
        using (var stream = new SslStream(client.GetStream(), false))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
        {
            try {
                var options = new SslServerAuthenticationOptions { EnabledSslProtocols = SslProtocols.Tls12 };
                options.ServerCertificateContext = SslStreamCertificateContext.Create(cert, extraCertificates ?? new X509Certificate2Collection(), offline: true);
                await stream.AuthenticateAsServerAsync(options, timeout.Token);
                using var startup = new LocalStartup(backend);
                while (await Frames.Read(stream, timeout.Token) is { } frame) {
                    timeout.CancelAfter(TimeSpan.FromMinutes(2));
                    if (mode == "frontdoor" && frame.Type == 1 && frame.Format == 2 && Cmd.Parser.ParseFrom(frame.Body).Type == CmdType.Authenticate) startup.ForgetAuthentication();
                    var localReply = backend.Length > 0 && mode == "frontdoor" && frame.Type == 1 ? await LocalAuth.TryReply(frame, backend, timeout.Token) : null;
                    if (localReply is not null) startup.AcceptAuthentication(frame);
                    var startupReply = localReply is null && mode == "frontdoor" && frame.Type == 1 ? await startup.TryReply(frame, timeout.Token) : null;
                    if ((localReply ?? startupReply ?? Diagnostics.Reply(frame, mode)) is { } reply) {
                        await stream.WriteAsync(Frames.Encode(reply), timeout.Token);
                        while (startup.PendingPushes.TryDequeue(out var pushFrame)) {
                            await stream.WriteAsync(Frames.Encode(pushFrame), timeout.Token);
                        }
                    }
                }
            } catch (Exception e) when (e is IOException or AuthenticationException or OperationCanceledException or InvalidProtocolBufferException) {
                Console.WriteLine($"Connection ended: {e.GetType().Name}");
            }
        }
    }
}
static class LocalAuth
{
    public static async Task<Frame?> TryReply(Frame frame, string backend, CancellationToken ct)
    {
        if (frame.Format != 2) return null;
        var cmd = Cmd.Parser.ParseFrom(frame.Body);
        if (cmd.Type != CmdType.Authenticate || cmd.Compressed || cmd.PayloadCase != Cmd.PayloadOneofCase.JsonPayload) return null;
        Wizards.Unification.Models.FrontDoor.AuthenticateReq? auth;
        try { auth = Newtonsoft.Json.JsonConvert.DeserializeObject<Wizards.Unification.Models.FrontDoor.AuthenticateReq>(cmd.JsonPayload.ToStringUtf8()); }
        catch (Newtonsoft.Json.JsonException) { return null; }
        if (auth is null || string.IsNullOrEmpty(auth.Token) || auth.Token.Length > 512) return null;
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(5) };
        using var request = new HttpRequestMessage(HttpMethod.Get, backend.TrimEnd('/') + "/api/me");
        try {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
            using var result = await http.SendAsync(request, ct);
            if (!result.IsSuccessStatusCode) return null;
            using var data = System.Text.Json.JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
            var user = data.RootElement;
            string id = user.TryGetProperty("persona_id", out var persona) ? persona.ToString() : user.GetProperty("id").ToString();
            if (!string.IsNullOrEmpty(auth.PersonaId) && auth.PersonaId != id) return null;
            var session = new Wizards.Unification.Models.FrontDoor.FrontDoorSessionResp { SessionId = Guid.NewGuid().ToString(), Attached = true, ExternalChatAppId = "" };
            var response = new Response { RawTransId = cmd.RawTransId, JsonPayload = ByteString.CopyFromUtf8(Newtonsoft.Json.JsonConvert.SerializeObject(session)) };
            Console.WriteLine("Local FrontDoor authentication accepted through the loopback account service.");
            return new Frame(frame.Version, 1, 2, response.ToByteArray());
        } catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or KeyNotFoundException or FormatException or TaskCanceledException) { return null; }
    }
}
static class SelfTest
{
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    public static async Task TrustedCertificate(string pfx)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var certificate = Certificates.Load(pfx, Environment.GetEnvironmentVariable("ARENA_PFX_PASSWORD"));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try {
            var accept = listener.AcceptTcpClientAsync();
            using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
            var serving = Host.Serve(await accept, certificate, "frontdoor");
            using var ssl = new SslStream(client.GetStream(), false);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12, CertificateRevocationCheckMode = X509RevocationMode.Online }, timeout.Token);
            var ping = new Frame(4, 2, 1, new byte[] {1, 2, 3, 4}); await ssl.WriteAsync(Frames.Encode(ping), timeout.Token);
            var pong = await Frames.Read(ssl, timeout.Token); Check(pong?.Type == 3 && pong.Body.SequenceEqual(ping.Body), "trusted TLS ping/pong");
            ssl.Close(); client.Close(); await serving;
        } finally { listener.Stop(); }
        Console.WriteLine("PASS: localhost TLS1.2 certificate accepted by default OS validation with online revocation checking; ping/pong succeeds.");
    }
    public static async Task Backend(string backend, bool was = false)
    {
        if (!Uri.TryCreate(backend, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address)) throw new ArgumentException("Backend self-test requires numeric loopback HTTP address");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var account = new { username = "protocol_" + Guid.NewGuid().ToString("N")[..12], password = "Test-Only-" + Guid.NewGuid().ToString("N") };
        string accountJson = System.Text.Json.JsonSerializer.Serialize(account);
        using var registered = await http.PostAsync(backend.TrimEnd('/') + "/api/register", new StringContent(accountJson, System.Text.Encoding.UTF8, "application/json"), timeout.Token); registered.EnsureSuccessStatusCode();
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, backend.TrimEnd('/') + (was ? "/auth/oauth/token" : "/api/login"));
        if (was) {
            loginRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "password", ["username"] = account.username, ["password"] = account.password });
            loginRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("arena-local:local-only")));
        } else loginRequest.Content = new StringContent(accountJson, System.Text.Encoding.UTF8, "application/json");
        using var logged = await http.SendAsync(loginRequest, timeout.Token); logged.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(await logged.Content.ReadAsStringAsync(timeout.Token));
        string token = json.RootElement.GetProperty(was ? "access_token" : "token").GetString()!;
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, backend.TrimEnd('/') + "/api/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var meResult = await http.SendAsync(meRequest, timeout.Token); meResult.EnsureSuccessStatusCode();
        using var me = System.Text.Json.JsonDocument.Parse(await meResult.Content.ReadAsStringAsync(timeout.Token));
        string id = me.RootElement.TryGetProperty("persona_id", out var realPersona) ? realPersona.ToString() : me.RootElement.GetProperty("id").ToString();
        if (was) {
            Check(token.Split('.').Length == 3, "WAS JWT structure");
            Check(json.RootElement.GetProperty("persona_id").GetString() == id, "WAS persona identity");
            using var profileRequest = new HttpRequestMessage(HttpMethod.Get, backend.TrimEnd('/') + "/profile"); profileRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var profileResult = await http.SendAsync(profileRequest, timeout.Token); profileResult.EnsureSuccessStatusCode();
            using var profile = System.Text.Json.JsonDocument.Parse(await profileResult.Content.ReadAsStringAsync(timeout.Token));
            Check(profile.RootElement.GetProperty("personaID").GetString() == id, "WAS profile identity");
        }
        using var cert = Certificates.Load("", null);
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try {
            var accept = listener.AcceptTcpClientAsync();
            using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, timeout.Token);
            var serving = Host.Serve(await accept, cert, "frontdoor", backend);
            using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12 }, timeout.Token);
            async Task<Response> Authenticate(string bearer, string persona) {
                var transaction = Guid.NewGuid().ToString();
                var cmd = new Cmd { Type = CmdType.Authenticate, RawTransId = transaction, JsonPayload = ByteString.CopyFromUtf8(System.Text.Json.JsonSerializer.Serialize(new { Token = bearer, PersonaId = persona })) };
                await ssl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, cmd.ToByteArray())), timeout.Token);
                var response = Response.Parser.ParseFrom((await Frames.Read(ssl, timeout.Token))!.Body);
                Check(response.RawTransId == transaction, "real backend response correlation"); return response;
            }
            var accepted = await Authenticate(token, id);
            Check(accepted.Error == null, "real backend local authentication");
            var session = Newtonsoft.Json.JsonConvert.DeserializeObject<Wizards.Unification.Models.FrontDoor.FrontDoorSessionResp>(accepted.JsonPayload.ToStringUtf8());
            Check(session?.Attached == true && !string.IsNullOrWhiteSpace(session.SessionId), "real backend session DTO");
            async Task<Response> SendCommand(Cmd command) {
                command.RawTransId = Guid.NewGuid().ToString();
                await ssl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, command.ToByteArray())), timeout.Token);
                var response = Response.Parser.ParseFrom((await Frames.Read(ssl, timeout.Token))!.Body);
                Check(response.RawTransId == command.RawTransId && response.Error is null, "startup/deck command correlation and success");
                return response;
            }
            var graph = await SendCommand(new Cmd { Type = CmdType.GraphGetGraphDefinitions, JsonPayload = ByteString.CopyFromUtf8("{}") });
            var definitions = Newtonsoft.Json.Linq.JObject.Parse(graph.JsonPayload.ToStringUtf8())["GraphDefinitions"] as Newtonsoft.Json.Linq.JArray;
            Check(definitions?.Count > 0 && definitions[0]?["Id"]?.ToString() == "NPE_Tutorial", "initialized local tutorial graph definitions");
            var graphState = await SendCommand(new Cmd { Type = CmdType.GraphGetGraphState, JsonPayload = ByteString.CopyFromUtf8("{\"GraphId\":\"NPE_Tutorial\"}") });
            Check(Newtonsoft.Json.Linq.JObject.Parse(graphState.JsonPayload.ToStringUtf8())["NodeStates"]?["LocalCompleted"]?["Status"]?.ToString() == "Completed", "completed local tutorial graph state");
            var formats = await SendCommand(new Cmd { Type = CmdType.GetFormats, ProtobufPayload = Google.Protobuf.WellKnownTypes.Any.Pack(new GetFormatsReq()) });
            var formatState = formats.ProtobufPayload.Unpack<GetFormatsResponse>();
            Check(formatState.Formats.Count > 0 && formatState.Formats[0].LegalSets.Count > 0 && formatState.FormatGroups.Count > 0, "typed nonempty local format configuration");
            var sets = await SendCommand(new Cmd { Type = CmdType.GetSets, ProtobufPayload = new Google.Protobuf.WellKnownTypes.Any() });
            var setState = sets.ProtobufPayload.Unpack<SetMetadataResponse>();
            Check(setState.SetGroups.Any(g => g.GroupName == "AllFilters" && g.Sets.Count > 0), "set metadata supplies required AllFilters group");
            var start = await SendCommand(new Cmd { Type = CmdType.StartHook, ProtobufPayload = Google.Protobuf.WellKnownTypes.Any.Pack(new StartHookReq()) });
            var startupState = start.ProtobufPayload.Unpack<StartHookResponseV2>();
            Check(startupState.InventoryInfo is not null && startupState.PreferredCosmetics is not null && startupState.ServerTime.Year >= 2026, "typed StartHook initialization");
            var cards = await SendCommand(new Cmd { Type = CmdType.CardGetAllCards, JsonPayload = ByteString.CopyFromUtf8("{\"CacheVersion\":0}") });
            Check(Newtonsoft.Json.Linq.JObject.Parse(cards.JsonPayload.ToStringUtf8())["cards"] is Newtonsoft.Json.Linq.JObject, "native collection response");
            var summaries = await SendCommand(new Cmd { Type = CmdType.DeckGetDeckSummariesV3, JsonPayload = ByteString.CopyFromUtf8("{}") });
            Check(Newtonsoft.Json.Linq.JObject.Parse(summaries.JsonPayload.ToStringUtf8())["Summaries"] is Newtonsoft.Json.Linq.JArray, "native deck summaries response");
            string deckId = Guid.NewGuid().ToString("D");
            var draft = Newtonsoft.Json.Linq.JObject.FromObject(new {
                Summary = new { DeckId = deckId, Name = "TLS draft", Attributes = new[] { new { name = "Format", value = "Historic" } }, PreferredCosmetics = new { } },
                Deck = new { MainDeck = new object[0], Sideboard = new object[0], CommandZone = new object[0], Companions = new object[0], CardSkins = new object[0] }, ActionType = 0
            });
            async Task<Response> Native(CmdType type, Newtonsoft.Json.Linq.JToken payload) => await SendCommand(new Cmd { Type = type, JsonPayload = ByteString.CopyFromUtf8(payload.ToString(Newtonsoft.Json.Formatting.None)) });
            var created = await Native(CmdType.DeckUpsertDeckV3, draft);
            Check(Newtonsoft.Json.Linq.JObject.Parse(created.JsonPayload.ToStringUtf8())["DeckId"]?.ToString() == deckId, "TLS draft create");
            draft["Summary"]!["Name"] = "TLS renamed";
            var renamed = await Native(CmdType.DeckUpsertDeckV3, draft);
            Check(Newtonsoft.Json.Linq.JObject.Parse(renamed.JsonPayload.ToStringUtf8())["Name"]?.ToString() == "TLS renamed", "TLS draft edit");
            var deckRequest = Newtonsoft.Json.Linq.JObject.FromObject(new { DeckId = deckId });
            var body = await Native(CmdType.DeckGetDeck, deckRequest);
            Check(Newtonsoft.Json.Linq.JObject.Parse(body.JsonPayload.ToStringUtf8())["MainDeck"] is Newtonsoft.Json.Linq.JArray, "TLS draft body read");
            var populated = await SendCommand(new Cmd { Type = CmdType.StartHook, ProtobufPayload = Google.Protobuf.WellKnownTypes.Any.Pack(new StartHookReq()) });
            var populatedState = populated.ProtobufPayload.Unpack<StartHookResponseV2>();
            Check(populatedState.DeckSummaries.Any(d => d.Name == "TLS renamed") && populatedState.DecksInternal.ContainsKey(deckId), "typed startup persisted draft mapping");
            var deleted = await Native(CmdType.DeckDeleteDeck, deckRequest);
            // Bot Match end-to-end integration test
            string matchDeckId = Guid.NewGuid().ToString("D");
            var playDeck = Newtonsoft.Json.Linq.JObject.FromObject(new {
                Summary = new { DeckId = matchDeckId, Name = "PlayDeck", Attributes = new[] { new { name = "Format", value = "Historic" } }, PreferredCosmetics = new { } },
                Deck = new {
                    MainDeck = new[] {
                        new { cardId = 6993, quantity = 52 }, // Forest
                        new { cardId = 67440, quantity = 4 },  // Llanowar Elves
                        new { cardId = 79334, quantity = 4 }   // Grizzly Bears
                    },
                    Sideboard = new object[0],
                    CommandZone = new object[0],
                    Companions = new object[0],
                    CardSkins = new object[0]
                },
                ActionType = 0
            });
            await Native(CmdType.DeckUpsertDeckV3, playDeck);

            var botMatchCmd = new Cmd {
                Type = CmdType.EventAiBotMatch,
                RawTransId = Guid.NewGuid().ToString(),
                JsonPayload = ByteString.CopyFromUtf8(Newtonsoft.Json.JsonConvert.SerializeObject(new { deckId = matchDeckId, botDeckId = (string?)null, botMatchType = 0 }))
            };
            await ssl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, botMatchCmd.ToByteArray())), timeout.Token);
            var matchRespFrame = await Frames.Read(ssl, timeout.Token);
            var matchResp = Response.Parser.ParseFrom(matchRespFrame!.Body);
            Check(matchResp.RawTransId == botMatchCmd.RawTransId && matchResp.Error is null, "EventAiBotMatch resolved promise");

            var pushFrame = await Frames.Read(ssl, timeout.Token);
            var pushResp = Response.Parser.ParseFrom(pushFrame!.Body);
            var pushNotif = Newtonsoft.Json.JsonConvert.DeserializeObject<Wizards.Unification.Models.Player.PushNotification>(pushResp.JsonPayload.ToStringUtf8());
            Check(pushNotif?.Type == Wizards.Arena.Enums.Player.ENotificationType.MatchCreated, "MatchCreated push notification");
            Check(pushNotif?.MatchInfoV4?.MatchEndpointHost == "127.0.0.1", "MatchDoor host 127.0.0.1");
            Check(pushNotif?.MatchInfoV4?.MatchEndpointPort == 9444, "MatchDoor port 9444");
            string activeMatchId = pushNotif!.MatchInfoV4.MatchId;

            // Test MatchDoor TLS interaction
            var matchListener = new TcpListener(IPAddress.Loopback, 0); matchListener.Start();
            int testMatchPort = ((IPEndPoint)matchListener.LocalEndpoint).Port;
            var matchAccept = matchListener.AcceptTcpClientAsync();
            using var matchTcp = new TcpClient();
            await matchTcp.ConnectAsync(IPAddress.Loopback, testMatchPort, timeout.Token);
            var matchServing = MatchDoorHost.Serve(await matchAccept, cert, backend);
            using var matchSsl = new SslStream(matchTcp.GetStream(), false, (_, _, _, _) => true);
            await matchSsl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12 }, timeout.Token);

            var authMsg = new ClientToMatchServiceMessage {
                ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.AuthenticateRequest,
                RequestId = 1,
                Payload = new AuthenticateRequest { ClientId = id, PlayFabSessionTicket = token }.ToByteString()
            };
            await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, authMsg.ToByteArray())), timeout.Token);
            var authReplyFrame = await Frames.Read(matchSsl, timeout.Token);
            var authReply = MatchServiceToClientMessage.Parser.ParseFrom(authReplyFrame!.Body);
            Check(authReply.AuthenticateResponse?.ClientId == id, "MatchDoor AuthenticateResponse ClientId");

            var connMsg = new ClientToMatchServiceMessage {
                ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.ClientToMatchDoorConnectRequest,
                RequestId = 2,
                Payload = new ClientToMatchDoorConnectRequest { MatchId = activeMatchId, McFabricUri = "fabric:/match/" + activeMatchId }.ToByteString()
            };
            await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, connMsg.ToByteArray())), timeout.Token);

            var roomStateFrame = await Frames.Read(matchSsl, timeout.Token);
            var roomState = MatchServiceToClientMessage.Parser.ParseFrom(roomStateFrame!.Body);
            Check(roomState.MatchGameRoomStateChangedEvent?.GameRoomInfo?.StateType == MatchGameRoomStateType.Playing, "Match room state is Playing");

            var connectFrame = await Frames.Read(matchSsl, timeout.Token);
            var connectMsg = MatchServiceToClientMessage.Parser.ParseFrom(connectFrame!.Body);
            Check(connectMsg.GreToClientEvent?.GreToClientMessages.Any(m => m.ConnectResp?.Status == ConnectionStatus.Success) == true, "ConnectResp Success");

            var stateFrame = await Frames.Read(matchSsl, timeout.Token);
            var stateMsg = MatchServiceToClientMessage.Parser.ParseFrom(stateFrame!.Body);
            Check(stateMsg.GreToClientEvent?.GreToClientMessages.Any(m => m.GameStateMessage != null) == true, "GameStateMessage received from Forge");

            GREToClientMessage? firstGre = stateMsg.GreToClientEvent?.GreToClientMessages.FirstOrDefault(m => m.ActionsAvailableReq != null || m.MulliganReq != null);
            var starter = stateMsg.GreToClientEvent?.GreToClientMessages.FirstOrDefault(m => m.ChooseStartingPlayerReq != null);
            if (starter != null) {
                var starterResp = new ClientToGREMessage {
                    Type = ClientMessageType.ChooseStartingPlayerResp,
                    RespId = starter.MsgId,
                    ChooseStartingPlayerResp = new ChooseStartingPlayerResp { SystemSeatId = 1 }
                };
                var sMsg = new ClientToMatchServiceMessage {
                    ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.ClientToGremessage,
                    Payload = starterResp.ToByteString()
                };
                await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, sMsg.ToByteArray())), timeout.Token);
            }
            while (firstGre == null) {
                var f = await Frames.Read(matchSsl, timeout.Token);
                if (f == null) break;
                var msg = MatchServiceToClientMessage.Parser.ParseFrom(f.Body);
                var starterMsg = msg.GreToClientEvent?.GreToClientMessages.FirstOrDefault(m => m.ChooseStartingPlayerReq != null);
                if (starterMsg != null) {
                    var starterResp = new ClientToGREMessage {
                        Type = ClientMessageType.ChooseStartingPlayerResp,
                        RespId = starterMsg.MsgId,
                        ChooseStartingPlayerResp = new ChooseStartingPlayerResp { SystemSeatId = 1 }
                    };
                    var sMsg = new ClientToMatchServiceMessage {
                        ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.ClientToGremessage,
                        Payload = starterResp.ToByteString()
                    };
                    await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, sMsg.ToByteArray())), timeout.Token);
                    continue;
                }
                firstGre = msg.GreToClientEvent?.GreToClientMessages.FirstOrDefault(m => m.ActionsAvailableReq != null || m.MulliganReq != null);
            }
            Check(firstGre != null && (firstGre.ActionsAvailableReq != null || firstGre.MulliganReq != null), "ActionsAvailableReq or MulliganReq received");

            if (firstGre?.MulliganReq != null) {
                var keepGre = new ClientToGREMessage {
                    Type = ClientMessageType.MulliganResp,
                    RespId = firstGre.MsgId,
                    MulliganResp = new MulliganResp { Decision = MulliganOption.AcceptHand }
                };
                var keepMsg = new ClientToMatchServiceMessage {
                    ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.ClientToGremessage,
                    Payload = keepGre.ToByteString()
                };
                await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, keepMsg.ToByteArray())), timeout.Token);
            } else {
                var passGre = new ClientToGREMessage {
                    Type = ClientMessageType.PerformActionResp,
                    RespId = firstGre?.MsgId ?? 0,
                    PerformActionResp = new PerformActionResp {
                        Actions = { new Wotc.Mtgo.Gre.External.Messaging.Action { ActionType = ActionType.Pass } }
                    }
                };
                var passMsg = new ClientToMatchServiceMessage {
                    ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.ClientToGremessage,
                    Payload = passGre.ToByteString()
                };
                await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, passMsg.ToByteArray())), timeout.Token);
            }

            var concedeGre = new ClientToGREMessage {
                Type = ClientMessageType.ConcedeReq,
                ConcedeReq = new ConcedeReq()
            };
            var concedeMsg = new ClientToMatchServiceMessage {
                ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.ClientToGremessage,
                Payload = concedeGre.ToByteString()
            };
            await matchSsl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, concedeMsg.ToByteArray())), timeout.Token);

            MatchServiceToClientMessage? completedMsg = null;
            while (completedMsg?.MatchGameRoomStateChangedEvent?.GameRoomInfo?.StateType != MatchGameRoomStateType.MatchCompleted) {
                var f = await Frames.Read(matchSsl, timeout.Token);
                if (f == null) break;
                completedMsg = MatchServiceToClientMessage.Parser.ParseFrom(f.Body);
            }
            Check(completedMsg?.MatchGameRoomStateChangedEvent?.GameRoomInfo?.StateType == MatchGameRoomStateType.MatchCompleted, "Match completed cleanly");

            matchSsl.Close(); matchTcp.Close(); matchListener.Stop();

            Check((await Authenticate(token, "wrong-persona")).Error?.Code == 1004, "wrong persona rejection");
            Check((await Authenticate("invalid-local-token", id)).Error?.Code == 1004, "invalid token rejection");
            ssl.Close(); client.Close(); await serving;
        } finally { listener.Stop(); }
        Console.WriteLine(was ? "PASS: WAS form login/JWT/profile, TLS FrontDoor auth/session/correlation, bot matchmaking, MatchDoor TLS/GRE Forge loop, wrong persona and invalid token rejection." : "PASS: real backend register/login, TLS FrontDoor auth/session/correlation, bot matchmaking, MatchDoor TLS/GRE Forge loop, wrong persona and invalid token rejection.");
    }
    public static async Task Run()
    {
        ForgeEncoder.Test();
        BattlePresentation.Test();
        BattleFlow.Test();
        BattlePlaybackTests.Run();
        await BattleCompletionTests.Run();
        using (var starting = System.Text.Json.JsonDocument.Parse("""
            {"input":{"type":"InputConfirm","startingPlayer":true}}
            """)) {
            var request = BattleRequests.Create(starting.RootElement, 30, 3)!;
            Check(request.Type == GREMessageType.ChooseStartingPlayerReq && request.ChooseStartingPlayerReq.SystemSeatIds.SequenceEqual(new uint[] { 1, 2 }), "Native Play/Draw choice before opening hand");
        }
        using (var targets = System.Text.Json.JsonDocument.Parse("""
            {"input":{"type":"InputSelectTargets","divided":false,"sourceId":1001,"min":1,"max":1,"targets":[2,1002],"selected":[]}}
            """)) {
            var request = BattleRequests.Create(targets.RootElement, 31, 4)!;
            Check(request.SelectTargetsReq.Targets.Single().Targets.Count == 2 && request.SelectTargetsReq.SourceId == 1001, "Native player/card targeting request");
            Check(GREToClientMessage.Parser.ParseFrom(request.ToByteArray()).Type == GREMessageType.SelectTargetsReq, "Target request protobuf roundtrip");
        }
        using (var attackers = System.Text.Json.JsonDocument.Parse("""
            {"input":{"type":"InputAttack","attackers":[{"instanceId":1001,"defenders":[2],"selected":2}]}}
            """)) {
            var request = BattleRequests.Create(attackers.RootElement, 32, 5)!;
            Check(BattleRequests.RecipientId(request.DeclareAttackersReq.Attackers.Single().SelectedDamageRecipient) == 2, "Native attacker/defender mapping");
        }
        using (var blockers = System.Text.Json.JsonDocument.Parse("""
            {"input":{"type":"InputBlock","blockers":[{"instanceId":1002,"attackers":[1001],"selected":[1001]}]}}
            """)) {
            var request = BattleRequests.Create(blockers.RootElement, 33, 6)!;
            Check(request.DeclareBlockersReq.Blockers.Single().SelectedAttackerInstanceIds.Single() == 1001, "Native blocker assignment mapping");
        }
        string chosenDeck = Guid.NewGuid().ToString("D");
        Check(LocalStartup.SelectedDeckId(Newtonsoft.Json.Linq.JObject.FromObject(new { Summary = new { DeckId = chosenDeck }, Deck = new { MainDeck = new object[0] } })) == chosenDeck, "Native Arena Summary.DeckId selection");
        try { LocalStartup.SelectedDeckId(new Newtonsoft.Json.Linq.JObject()); throw new Exception("Missing deck selection accepted"); } catch (InvalidDataException) { }
        var mulliganRequest = MatchDoorHost.CreateMulliganRequest(10, 1, 7);
        Check(mulliganRequest.Prompt.Parameters[0].NumberValue == 7, "Arena starting hand prompt parameter");
        string managedPath = @"C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed";
        if (File.Exists(Path.Combine(managedPath, "SharedClientCore.dll"))) {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) => {
                string dependency = Path.Combine(managedPath, name.Name + ".dll");
                return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
            };
            var clientAssembly = System.Reflection.Assembly.LoadFrom(Path.Combine(managedPath, "SharedClientCore.dll"));
            using var highlightSnapshot = System.Text.Json.JsonDocument.Parse("""
            {"legalActions":[{"instanceId":1001,"grpId":7,"actionType":1,"isLand":false,"manaCost":"{G}","autoTapSources":[1002]}]}
            """);
            var highlightAction = BattlePresentation.Actions(highlightSnapshot.RootElement).Single();
            var canAfford = clientAssembly.GetType("ActionExtensions", true)!.GetMethod("CanAffordToCast")!;
            Check((bool)canAfford.Invoke(null, new object[] { highlightAction })!, "Installed Arena recognizes payable spell highlight");
            highlightAction.AutoTapSolution = null;
            Check(!(bool)canAfford.Invoke(null, new object[] { highlightAction })!, "Installed Arena requires payment preview for payable highlights");
            var requestType = clientAssembly.GetType("GreClient.Rules.MulliganRequest", throwOnError: true)!;
            using var sacrificeSnapshot = System.Text.Json.JsonDocument.Parse("""
            {"input":{"type":"InputConfirm","startingPlayer":false,"sourceId":1001,"message":"Sacrifice Terramorphic Expanse?"}}
            """);
            var sacrifice = BattleRequests.Create(sacrificeSnapshot.RootElement,12,3)!;
            Check(sacrifice.Prompt.PromptId==1739 && (sacrifice.Prompt.Parameters[0].StringValue=="Terramorphic Expanse" || sacrifice.Prompt.Parameters[0].Reference?.Id==1001),"Native sacrifice prompt card reference");
            var optionalType=clientAssembly.GetType("GreClient.Rules.OptionalActionMessageRequest",true)!;
            using(var triggerSnapshot=System.Text.Json.JsonDocument.Parse("""
                {"input":{"type":"InputConfirm","startingPlayer":false,"sourceId":1001,"optionalTrigger":true,"optionalTriggerPrompt":7132},
                "stack":[{"instanceId":1000100,"parentId":1001}]}
                """)) {
                var trigger=BattleRequests.Create(triggerSnapshot.RootElement,13,3)!;
                Check(trigger.Type==GREMessageType.OptionalActionMessage && trigger.SelectNReq==null && trigger.Prompt.PromptId==7132
                    && trigger.OptionalActionMessage.SourceId==1000100,"Optional trigger used generic buttons or wrong ability source");
                Check(Activator.CreateInstance(optionalType,trigger.OptionalActionMessage,trigger)!=null,"Installed Arena rejected optional trigger request");
            }
            Check(Activator.CreateInstance(optionalType,sacrifice.OptionalActionMessage,sacrifice)!=null,"Native sacrifice request constructor");
            using(var exploreSnapshot=System.Text.Json.JsonDocument.Parse("""
                {"input":{"type":"InputConfirm","startingPlayer":false,"sourceId":1001,"exploreCardId":1002,"message":"[Explore] Put card in your graveyard?","options":["Yes","No"]}}
                """)) {
                var explore=BattleRequests.Create(exploreSnapshot.RootElement,13,3)!;
                Check(explore.Type==GREMessageType.OptionalActionMessage && explore.OptionalActionMessage.OptionalActionTypes.Contains(CardMechanicType.Explore)
                    && explore.OptionalActionMessage.RecipientIds.SequenceEqual(new uint[]{1002}) && explore.Prompt.PromptId==115,"Explore did not use its native revealed-card browser");
                Check(Activator.CreateInstance(optionalType,explore.OptionalActionMessage,explore)!=null,"Installed Arena rejected Explore request");
                Check(explore.Prompt.Parameters.Count==2 && explore.Prompt.Parameters[1].Reference.Id==1002,
                    "Explore's card must occupy positional argument 2");
            }
            var promptEngineType=clientAssembly.GetType("Wotc.Mtga.Cards.Database.PromptEngine",true)!;
            var promptEngine=Activator.CreateInstance(promptEngineType,new object?[]{null,null})!;
            var bindPrompt=promptEngineType.GetMethod("BuildParameterizedString",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            var promptFormatter=promptEngineType.GetField("PromptFormatter",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(promptEngine)!;
            var formatPrompt=promptFormatter.GetType().GetMethod("Format")!;
            foreach(int life in new[]{1,2,3,4,5,7,10,50}) {
                using var lifeSnapshot=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new {
                    input=new {type="InputConfirm",startingPlayer=false,sourceId=1001,message=$"Do you want to pay {life} life?",options=new[]{"Yes","No"}}}));
                var lifeRequest=BattleRequests.Create(lifeSnapshot.RootElement,13,3)!;
                Check(lifeRequest.Type==GREMessageType.OptionalActionMessage && lifeRequest.SelectNReq==null
                    && lifeRequest.Prompt.PromptId==8167 && lifeRequest.Prompt.Parameters.Count==2 && lifeRequest.Prompt.Parameters[1].NumberValue==life
                    && lifeRequest.OptionalActionMessage.OptionalActionTypes.Contains(CardMechanicType.PayLife),"Life confirmation used wrong menu or amount");
                Check(Activator.CreateInstance(optionalType,lifeRequest.OptionalActionMessage,lifeRequest)!=null,"Installed Arena rejected native life-payment request");
                bindPrompt.Invoke(promptEngine,new object[]{lifeRequest.Prompt.Parameters});
                Check((string)formatPrompt.Invoke(promptFormatter,new object?[]{"Pay {2, Number} life?",null,null})! == $"Pay {life} life?",
                    "Installed Arena rendered an invalid life-payment amount");
            }
            var clientRequest = Activator.CreateInstance(requestType, mulliganRequest.MulliganReq, mulliganRequest)!;
            Check((uint)requestType.GetField("StartingHandSize")!.GetValue(clientRequest)! == 7, "Installed Arena mulligan constructor accepts request");
            var bottomMessage = MatchDoorHost.CreateBottomCardRequest(11, 2, 1, new uint[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007 });
            var selectionType = clientAssembly.GetType("GreClient.Rules.SelectNRequest", throwOnError: true)!;
            var selection = Activator.CreateInstance(selectionType, bottomMessage.SelectNReq, bottomMessage)!;
            var baseType = selectionType.BaseType!;
            var selectionPrompt = (Prompt?)baseType.GetProperty("Prompt")!.GetValue(selection);
            Check(selectionPrompt is not null && selectionPrompt.PromptId > 1, "Arena bottom-selection workflow requires a display prompt");
            Check((int)selectionType.GetField("MinSel")!.GetValue(selection)! == 1 && (uint)selectionType.GetField("MaxSel")!.GetValue(selection)! == 1, "Choose exactly one bottom card");
            Check((bool)selectionType.GetProperty("IsInstanceIdSelection")!.GetValue(selection)!, "Arena hand-card selection classification");
            Console.WriteLine("PASS: installed Arena MulliganRequest constructor and starting-hand-size parameter.");
        }
        var manaAction = new Wotc.Mtgo.Gre.External.Messaging.Action();
        Check(MatchDoorHost.TryManaCost("{4}{R}{R}", manaAction) && manaAction.ManaCost.Sum(m => m.Count) == 6 && manaAction.ManaCost[0].Color.Single() == ManaColor.Generic, "Forge mana cost mapping");
        var phyrexianAction = new Wotc.Mtgo.Gre.External.Messaging.Action();
        Check(MatchDoorHost.TryManaCost("{W/P}", phyrexianAction) && phyrexianAction.ManaCost.Count == 1 && phyrexianAction.ManaCost[0].Color.Contains(ManaColor.Phyrexian), "Phyrexian mana parsed");
        Check(!MatchDoorHost.TryManaCost("{INVALID}", new Wotc.Mtgo.Gre.External.Messaging.Action()), "Unsupported costs are not treated as free");
        foreach (byte format in new byte[] { 1, 2 }) {
            var ping = new Frame(4, 2, format, new byte[] { 1, 2, 3, 4 });
            var pong = MatchDoorHost.HeartbeatReply(ping);
            Check(pong?.Type == 3 && pong.Format == format && pong.Body.SequenceEqual(ping.Body), "MatchDoor heartbeat echo");
            Check(MatchDoorHost.HeartbeatReply(pong!) is null, "Do not reply to a pong");
        }
        foreach (byte version in new byte[] { 1, 3, 4 }) {
            var original = new Frame(version, 1, 2, new byte[] { 1, 2, 3 });
            using var stream = new MemoryStream(Frames.Encode(original));
            var frame = await Frames.Read(stream); Check(frame?.Body.SequenceEqual(original.Body) == true, "framing roundtrip");
        }
        byte[] oversized = new byte[] {4, 33, 255, 255, 255, 127};
        try { await Frames.Read(new MemoryStream(oversized)); throw new Exception("Oversize accepted"); } catch (InvalidDataException) { }
        try { await Frames.Read(new MemoryStream(new byte[] {4, 33, 1, 0, 0, 0})); throw new Exception("Truncated accepted"); } catch (EndOfStreamException) { }
        var transaction = Guid.NewGuid().ToString();
        var command = new Cmd { Type = CmdType.Authenticate, RawTransId = transaction, JsonPayload = ByteString.CopyFromUtf8("{}") };
        var reply = Diagnostics.Reply(new Frame(4, 1, 2, command.ToByteArray()), "frontdoor")!;
        var response = Response.Parser.ParseFrom(reply.Body);
        Check(response.RawTransId == transaction && response.Error.Code == 1004, "frontdoor auth rejection");
        var match = new ClientToMatchServiceMessage { ClientToMatchServiceMessageType = ClientToMatchServiceMessageType.AuthenticateRequest, RequestId = 8, TransactionId = transaction };
        var matchReply = MatchServiceToClientMessage.Parser.ParseFrom(Diagnostics.Reply(new Frame(4, 1, 2, match.ToByteArray()), "match")!.Body);
        Check(matchReply.RequestId == 8 && matchReply.Error.ErrorCode == MatchServiceErrorCode.NotAuthorized, "match auth rejection");
        var httpPortProbe = new TcpListener(IPAddress.Loopback, 0); httpPortProbe.Start(); int httpPort = ((IPEndPoint)httpPortProbe.LocalEndpoint).Port; httpPortProbe.Stop();
        using var httpListener = new HttpListener(); string localUrl = $"http://127.0.0.1:{httpPort}"; httpListener.Prefixes.Add(localUrl + "/"); httpListener.Start();
        var httpServing = Task.Run(async () => {
            var context = await httpListener.GetContextAsync();
            Check(context.Request.Headers["Authorization"] == "Bearer local-test-token", "HTTP bridge token");
            var bytes = System.Text.Encoding.UTF8.GetBytes("{\"id\":1,\"username\":\"test\"}"); context.Response.ContentLength64 = bytes.Length; await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
        });
        var localCommand = new Cmd { Type = CmdType.Authenticate, RawTransId = transaction, JsonPayload = ByteString.CopyFromUtf8("{\"Token\":\"local-test-token\",\"PersonaId\":\"1\"}") };
        var localReply = await LocalAuth.TryReply(new Frame(4, 1, 2, localCommand.ToByteArray()), localUrl, CancellationToken.None);
        await httpServing; httpListener.Stop();
        Check(localReply != null, "local auth bridge");
        var session = Newtonsoft.Json.JsonConvert.DeserializeObject<Wizards.Unification.Models.FrontDoor.FrontDoorSessionResp>(Response.Parser.ParseFrom(localReply!.Body).JsonPayload.ToStringUtf8());
        Check(session?.Attached == true && !string.IsNullOrEmpty(session.SessionId), "observed session DTO");
        using var cert = Certificates.Load("", null);
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accept = listener.AcceptTcpClientAsync();
            using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port);
            var serving = Host.Serve(await accept, cert, "frontdoor");
            using var ssl = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12 });
            var ping = new Frame(4, 2, 1, new byte[] { 42, 0, 0, 0 });
            foreach (byte b in Frames.Encode(ping)) await ssl.WriteAsync(new byte[] { b });
            var pong = await Frames.Read(ssl); Check(pong?.Type == 3 && pong.Body.SequenceEqual(ping.Body), "TLS fragmented ping/pong");
            await ssl.WriteAsync(Frames.Encode(new Frame(4, 1, 2, command.ToByteArray())));
            var wireReply = await Frames.Read(ssl); Check(Response.Parser.ParseFrom(wireReply!.Body).Error.Code == 1004, "TLS protobuf exchange");
            ssl.Close(); client.Close(); await serving;
        } finally { listener.Stop(); }
        Console.WriteLine("PASS: v1/v3/v4 frames, bounds, truncation, FrontDoor/GRE correlation, local auth bridge, TLS1.2 fragmented ping/pong and protobuf exchange.");
    }
}

static class ModelInspector
{
    public static void Run()
    {
        var matchId = Guid.NewGuid().ToString("D");
        var matchInfo = new Wizards.Arena.Models.Network.ClientMatchInfoV4 {
            MatchEndpointHost = "127.0.0.1",
            MatchEndpointPort = 9444,
            MatchId = matchId,
            McFabricId = "fabric:/match/" + matchId,
            EventId = "BotMatch",
            Battlefield = "Default",
            MatchType = Wizards.Arena.Enums.Event.MatchType.Queue,
            YourSeat = 1
        };
        matchInfo.PlayerInfos.Add(new Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2 {
            ScreenName = "Player", SeatId = 1, TeamId = 1
        });
        matchInfo.PlayerInfos.Add(new Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2 {
            ScreenName = "Forge AI", SeatId = 2, TeamId = 2
        });
        var push = new Wizards.Unification.Models.Player.PushNotification(Wizards.Arena.Enums.Player.ENotificationType.MatchCreated) {
            MatchInfoV4 = matchInfo
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(push);
        Console.WriteLine($"PUSH JSON: {json}");
        var roundtrip = Newtonsoft.Json.JsonConvert.DeserializeObject<Wizards.Unification.Models.Player.PushNotification>(json);
        Console.WriteLine($"ROUNDTRIP TYPE: {roundtrip?.Type}, HOST: {roundtrip?.MatchInfoV4?.MatchEndpointHost}:{roundtrip?.MatchInfoV4?.MatchEndpointPort}, SEAT: {roundtrip?.MatchInfoV4?.YourSeat}");

        string fixturesPath = Path.Combine(Environment.CurrentDirectory, "analysis", "startup-fixtures.json");
        var fixtures = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fixturesPath));
        var activeEventsPayload = fixtures["commands"]?["EventGetActiveEventsV2"]?["payload"];
        try {
            var activeEvents = activeEventsPayload?.ToObject<Wizards.Unification.Models.Event.ActiveEventsResponseV2>();
            Console.WriteLine($"DESERIALIZED ActiveEvents: events={activeEvents?.Events?.Count}, botMatches={activeEvents?.AiBotMatches?.Count}");
            if (activeEvents?.AiBotMatches?.Count > 0) {
                var bm = activeEvents.AiBotMatches[0];
                Console.WriteLine($"BotMatch[0]: Name={bm.InternalEventName}, Format={bm.Format}, WinCondition={bm.WinCondition}, StartingPlayer={bm.StartingPlayer}, TimerPackage={bm.TimerPackage}");
            }
        } catch (Exception ex) {
            Console.WriteLine($"FAILED DESERIALIZATION ActiveEvents: {ex.Message}");
        }
        var bladeConfigPayload = fixtures["commands"]?["GetPlayBladeQueueConfig"]?["payload"];
        try {
            var bladeConfig = bladeConfigPayload?.ToObject<List<Wizards.Unification.Models.PlayBlade.PlayBladeQueueEntry>>();
            Console.WriteLine($"DESERIALIZED PlayBladeQueueConfig: count={bladeConfig?.Count}, firstType={bladeConfig?[0]?.QueueType}");
        } catch (Exception ex) {
            Console.WriteLine($"FAILED DESERIALIZATION PlayBladeQueueConfig: {ex.Message}");
        }
    }
}
