using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Wizards.Arena.Models.Network;
using Wizards.Arena.Protocol;

// Local account-bound command adapters. Unsupported commands retain explicit
// errors; every success below has an observed client DTO or generated contract.
sealed class LocalStartup : IDisposable
{
    internal static string SelectedDeckId(JToken payload)
    {
        string? value = payload["Summary"]?["DeckId"]?.ToString() ?? payload["DeckId"]?.ToString();
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty) throw new InvalidDataException("No valid selected Arena deck ID");
        return id.ToString("D");
    }
    private readonly string _backend;
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(120) };
    private string? _token;
    private DateTime _tokenValidUntil = DateTime.MinValue;
    private string? _lastSelectedDeckId;
    private string _lastSelectedEventName = "AIBotMatch";
    internal static string EventName(JToken payload, string fallback) =>
        payload["EventName"]?.ToString() ?? payload["eventName"]?.ToString() ?? payload["InternalEventName"]?.ToString() ?? fallback;
    internal static string? GameMode(string eventName) => eventName switch {
        "AIBotMatch" => "play", "NoBans60AI" => "no-bans-60", "CommanderAI" => "commander", "CommanderAI4" => "commander4", "CommanderUnbannedAI" => "commander-unbanned", _ => null
    };
    static object Course(string eventName, object? summary = null, object? deck = null) => new {
        InternalEventName=eventName, CurrentModule=Wizards.Arena.Enums.Event.EModule.WinNoGate,
        ModulePayload="", CourseDeckSummary=summary ?? new Wizards.Unification.Models.Deck.DTO_DeckSummaryV3(),
        CourseDeck=deck ?? new Wizards.Unification.Models.Deck.Deck(),
        CurrentWins=0, CurrentLosses=0, CardPool=Array.Empty<uint>()
    };
    private static JToken? _cachedCards;
    private static int CacheVersion(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Math.Max(2, BinaryPrimitives.ReadInt32BigEndian(hash) & int.MaxValue);
    }
    private static readonly HashSet<string> BackendCommands = new(StringComparer.Ordinal) {
        "DeckGetDeckSummariesV3", "DeckGetDeck", "DeckUpsertDeckV3", "DeckDeleteDeck", "CardGetAllCards", "CardGetCardSet"
    };
    public readonly Queue<Frame> PendingPushes = new();
    public LocalStartup(string backend) => _backend = backend;
    public void Dispose() => _http.Dispose();
    public void ForgetAuthentication() { _token = null; _tokenValidUntil = DateTime.MinValue; }
    public void AcceptAuthentication(Frame frame)
    {
        var cmd = Cmd.Parser.ParseFrom(frame.Body);
        var auth = JsonConvert.DeserializeObject<Wizards.Unification.Models.FrontDoor.AuthenticateReq>(cmd.JsonPayload.ToStringUtf8());
        _token = auth?.Token;
        _tokenValidUntil = DateTime.UtcNow.AddMinutes(5);
    }
    public async Task<Frame?> TryReply(Frame frame, CancellationToken ct)
    {
        if (frame.Format != 2 || _token is null || _backend.Length == 0) return null;
        var cmd = Cmd.Parser.ParseFrom(frame.Body);
        string name = cmd.Type.ToString();
        JObject? fixture = Fixture(name);
        if (cmd.Type != CmdType.ChallengeCreate && cmd.Type != CmdType.ChallengeExit && cmd.Type != CmdType.EventAiBotMatch && cmd.Type != CmdType.EventEnterPairing && cmd.Type != CmdType.GraphGetGraphDefinitions && cmd.Type != CmdType.StartHook && !BackendCommands.Contains(name) && fixture is null) return null;
        try {
        if (DateTime.UtcNow > _tokenValidUntil) {
            using var request = new HttpRequestMessage(HttpMethod.Get, _backend.TrimEnd('/') + "/api/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            using var result = await _http.SendAsync(request, ct);
            if (!result.IsSuccessStatusCode) { _token = null; return null; }
            _tokenValidUntil = DateTime.UtcNow.AddMinutes(5);
        }
        Frame reply;
        if (cmd.Type == CmdType.EventAiBotMatch || cmd.Type == CmdType.EventEnterPairing) {
            reply = await HandleMatchmaking(cmd, frame.Version, ct);
        } else if (cmd.Type == CmdType.ChallengeCreate) {
            reply = JsonReply(cmd, new {
                challenge = new {
                    challengeId = Guid.NewGuid().ToString("D"),
                    challengeName = "Direct Challenge",
                    challengeTitle = "Direct Challenge",
                    challengeLimit = 2
                }
            }, frame.Version);
        } else if (cmd.Type == CmdType.ChallengeExit) {
            reply = JsonReply(cmd, new {
                challenge = new {
                    challengeId = Guid.NewGuid().ToString("D")
                }
            }, frame.Version);
        } else if (cmd.Type == CmdType.EventJoin) {
            _lastSelectedEventName=EventName(RequestJson(cmd),_lastSelectedEventName);
            reply=JsonReply(cmd,new {Course=Course(_lastSelectedEventName),InventoryInfo=(object?)null},frame.Version);
        } else if (cmd.Type == CmdType.EventSetDeckV3) {
            var reqJson = RequestJson(cmd);
            try {
                var deckIdStr = SelectedDeckId(reqJson);
                await BackendCommand("DeckGetDeck", JObject.FromObject(new { DeckId = deckIdStr }), ct);
                _lastSelectedDeckId = deckIdStr;
                _lastSelectedEventName=EventName(reqJson,_lastSelectedEventName);
            } catch { _lastSelectedDeckId = null; throw; }
            reply=JsonReply(cmd,Course(_lastSelectedEventName,reqJson["Summary"],reqJson["Deck"]),frame.Version);
        } else if (cmd.Type == CmdType.GraphGetGraphDefinitions) {
            var payload = fixture?["payload"]?.ToObject<Wizards.Unification.Models.Graph.ClientGraphDefinitionsResponse>();
            if (payload?.GraphDefinitions is null || payload.GraphDefinitions.Count == 0) throw new InvalidDataException("No initialized local campaign graph configured");
            reply = JsonReply(cmd, payload, frame.Version);
        } else if (cmd.Type == CmdType.StartHook) {
            reply = ProtoReply(cmd, await StartHook(ct), frame.Version);
        } else if (cmd.Type == CmdType.GetFormats) {
            if (fixture?["payload"] is null) throw new InvalidDataException("Local format configuration missing");
            reply = ProtoReply(cmd, GetFormatsResponse.Parser.ParseJson(NormalizeProtoJson(fixture["payload"]!, GetFormatsResponse.Descriptor).ToString(Formatting.None)), frame.Version);
        } else if (cmd.Type == CmdType.GetVoucherDefinitions && cmd.PayloadCase == Cmd.PayloadOneofCase.ProtobufPayload) {
            if (fixture?["protobufPayload"] is null) throw new InvalidDataException("Local voucher definitions missing");
            reply = ProtoReply(cmd, GetVoucherDefinitionsResponse.Parser.ParseJson(NormalizeProtoJson(fixture["protobufPayload"]!, GetVoucherDefinitionsResponse.Descriptor).ToString(Formatting.None)), frame.Version);
        } else if (cmd.Type == CmdType.GetSets && cmd.PayloadCase == Cmd.PayloadOneofCase.ProtobufPayload) {
            if (fixture?["protobufPayload"] is null) throw new InvalidDataException("Local set metadata missing");
            reply = ProtoReply(cmd, SetMetadataResponse.Parser.ParseJson(NormalizeProtoJson(fixture["protobufPayload"]!, SetMetadataResponse.Descriptor).ToString(Formatting.None)), frame.Version);
        } else if (cmd.Type == CmdType.CosmeticsSetPreferredAvatar) {
            reply = ProtoReply(cmd, new PreferredCosmetics {
                Avatar = new CosmeticsAvatarEntry { Id = "Avatar_Default" }
            }, frame.Version);
        } else if (BackendCommands.Contains(name)) {
            var payload=await BackendCommand(name, RequestJson(cmd), ct);
            // Arena uses StringUtils.DeserializeString (raw UTF-8) for deletion.
            // JSON quoting turns success into a failed client-side deletion.
            if(cmd.Type==CmdType.DeckDeleteDeck) {
                if(payload.Type!=JTokenType.String)throw new InvalidDataException("Invalid deck deletion reply");
                reply=new Frame(frame.Version,1,2,new Response {RawTransId=cmd.RawTransId,
                    JsonPayload=ByteString.CopyFromUtf8(payload.Value<string>()!)}.ToByteArray());
            } else reply=JsonReply(cmd,payload,frame.Version);
        } else {
            if (fixture!["payload"] is null) throw new InvalidDataException("Startup fixture payload missing");
            reply = fixture["format"]?.Value<string>() switch {
                "json" => JsonReply(cmd, fixture["payload"]!, frame.Version),
                "protobuf" => ProtoReply(cmd, ParseProtoFixture(cmd.Type, fixture["payload"]!), frame.Version),
                _ => throw new InvalidDataException("Unsupported startup fixture format")
            };
        }
        Console.WriteLine($"Local FrontDoor startup/deck command {name}: handled.");
        return reply;
        } catch (Exception error) when (error is HttpRequestException or JsonException or InvalidDataException or InvalidProtocolBufferException or TaskCanceledException or ArgumentException) {
            Console.WriteLine($"Local FrontDoor startup/deck command {name} ERROR:\n{error}");
            return new Frame(frame.Version, 1, 2, new Response { RawTransId = cmd.RawTransId, Error = new ServerError { Code = -32603, Message = $"Local command {name} could not be completed." } }.ToByteArray());
        }
    }
    static Frame JsonReply(Cmd cmd, object payload, byte version) => new(version, 1, 2,
        new Response { RawTransId = cmd.RawTransId, JsonPayload = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(payload)) }.ToByteArray());
    static Frame ProtoReply(Cmd cmd, IMessage payload, byte version) => new(version, 1, 2,
        new Response { RawTransId = cmd.RawTransId, ProtobufPayload = Any.Pack(payload) }.ToByteArray());
    static JObject? Fixture(string name)
    {
        foreach (string file in new[] { "startup-fixtures.json", "deckbuilder-formats.json" }) {
            string path = Path.Combine(Environment.CurrentDirectory, "analysis", file);
            if (!File.Exists(path)) continue;
            if (JObject.Parse(File.ReadAllText(path))["commands"]?[name] is JObject fixture) return fixture;
        }
        return null;
    }
    static JToken NormalizeProtoJson(JToken value, Google.Protobuf.Reflection.MessageDescriptor descriptor)
    {
        if (value is not JObject source) throw new InvalidDataException("Protobuf fixture requires object");
        var target = new JObject();
        foreach (var property in source.Properties()) {
            var field = descriptor.Fields.InDeclarationOrder().FirstOrDefault(f => string.Equals(f.Name, property.Name, StringComparison.OrdinalIgnoreCase) || string.Equals(f.JsonName, property.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Unknown protobuf fixture field");
            JToken converted = property.Value.DeepClone();
            if (field.FieldType == Google.Protobuf.Reflection.FieldType.Message) {
                if (field.IsMap) {
                    if (property.Value is not JObject map) throw new InvalidDataException("Protobuf fixture map requires object");
                    var mapValue = field.MessageType.Fields[2];
                    if (mapValue.FieldType == Google.Protobuf.Reflection.FieldType.Message) {
                        var normalizedMap = new JObject();
                        foreach (var entry in map.Properties()) normalizedMap[entry.Name] = NormalizeProtoJson(entry.Value, mapValue.MessageType);
                        converted = normalizedMap;
                    }
                } else if (field.IsRepeated) {
                    if (property.Value is not JArray list) throw new InvalidDataException("Protobuf fixture list requires array");
                    converted = new JArray(list.Select(item => NormalizeProtoJson(item, field.MessageType)));
                } else converted = NormalizeProtoJson(property.Value, field.MessageType);
            }
            target[field.JsonName] = converted;
        }
        return target;
    }
    static IMessage ParseProtoFixture(CmdType command, JToken payload) => command switch {
        CmdType.GetPlayerInbox => GetPlayerInboxResp.Parser.ParseJson(NormalizeProtoJson(payload, GetPlayerInboxResp.Descriptor).ToString(Formatting.None)),
        CmdType.ChallengeReconnectAll => ChallengeReconnectAllResp.Parser.ParseJson(NormalizeProtoJson(payload, ChallengeReconnectAllResp.Descriptor).ToString(Formatting.None)),
        CmdType.GetWpnNotificationPreferences => GetWpnNotificationPreferencesResp.Parser.ParseJson(NormalizeProtoJson(payload, GetWpnNotificationPreferencesResp.Descriptor).ToString(Formatting.None)),
        _ => throw new InvalidDataException("Protobuf startup fixture type is not allowlisted")
    };
    static JToken RequestJson(Cmd cmd)
    {
        if (cmd.PayloadCase != Cmd.PayloadOneofCase.JsonPayload) throw new InvalidDataException("Command requires JSON request");
        byte[] payload = cmd.JsonPayload.ToByteArray();
        if (cmd.Compressed) {
            if (payload.Length < 4) throw new InvalidDataException("Truncated compressed payload");
            int length = BinaryPrimitives.ReadInt32LittleEndian(payload);
            if (length < 0 || length > Frames.MaxBody) throw new InvalidDataException("Compressed payload exceeds local limits");
            using var compressed = new MemoryStream(payload, 4, payload.Length - 4);
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            payload = new byte[length]; gzip.ReadExactly(payload);
            if (gzip.ReadByte() != -1) throw new InvalidDataException("Compressed payload length mismatch");
        }
        return payload.Length == 0 ? new JObject() : JToken.Parse(Encoding.UTF8.GetString(payload));
    }
    async Task<JToken> BackendCommand(string name, JToken payload, CancellationToken ct)
    {
        if (name == "CardGetAllCards" && _cachedCards != null) return _cachedCards;
        using var request = new HttpRequestMessage(HttpMethod.Post, _backend.TrimEnd('/') + "/api/arena/command");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        request.Content = new StringContent(JsonConvert.SerializeObject(new { name, payload }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var result = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
        var res = result["result"] ?? throw new InvalidDataException("Backend command result missing");
        if (name == "CardGetAllCards") _cachedCards = res;
        return res;
    }
    async Task<StartHookResponseV2> StartHook(CancellationToken ct)
    {
        var response = new StartHookResponseV2 {
            InventoryInfo = new InventoryInfo { SeqId = 1, Cosmetics = new CosmeticsClient() },
            PreferredCosmetics = new PreferredCosmetics(), HomePageAchievements = new HomePageAchievements(),
            PreferredCosmeticsCacheVersion = 1,
            TokenDefinitionsCacheVersion = 1, KillSwitchNotificationCacheVersion = 1, ClientPeriodicRewardsCacheVersion = 1,
            KillSwitchNotification = new KillSwitchNotification(), CardMetadataInfo = new CardMetadataInfo(),
            DeckLimit = 250, ServerTime = DateTimeOffset.UtcNow
        };
        var native = await BackendCommand("DeckGetDeckSummariesV3", new JObject(), ct);
        var summaries = native.ToObject<Wizards.Unification.Models.Deck.GetDeckSummariesResponseV3>()?.Summaries ?? throw new InvalidDataException("Deck summary list missing");
        if (summaries.Count > 250) throw new InvalidDataException("Local deck count exceeds limit");
        response.DeckSummariesCacheVersion = CacheVersion(native.ToString(Formatting.None));
        var deckVersions = new StringBuilder();
        foreach (var summary in summaries) {
            var proto = new DeckSummaryV3 { DeckId = summary.DeckId, Name = summary.Name ?? "", Mana = summary.Mana ?? "", Description = summary.Description ?? "",
                DeckTileId = summary.DeckTileId, DeckArtId = summary.DeckArtId, IsCompanionValid = summary.IsCompanionValid,
                PreferredCosmetics = summary.PreferredCosmetics?.Clone() ?? new PreferredCosmeticsOnDeck(), IsNetDeck = summary.IsNetDeck, NetDeckFolderId = summary.NetDeckFolderId };
            foreach (var attr in summary.Attributes ?? new()) proto.Attributes.Add(new Wizards.Arena.Models.Network.DeckAttribute { Name = attr.name, Value = attr.value });
            response.DeckSummaries.Add(proto);
            var body = (await BackendCommand("DeckGetDeck", JObject.FromObject(new { DeckId = summary.DeckId }), ct)).ToObject<Wizards.Unification.Models.Deck.Deck>() ?? throw new InvalidDataException("Deck body missing");
            deckVersions.Append(summary.DeckId.ToString("D")).Append(':').Append(JsonConvert.SerializeObject(body)).Append('\n');
            var deck = new Wizards.Arena.Models.Network.Deck();
            foreach (var card in body.MainDeck) deck.MainDeck.Add(new Wizards.Arena.Models.Network.DeckCard { CardId = card.cardId, Quantity = card.quantity });
            foreach (var card in body.Sideboard) deck.Sideboard.Add(new Wizards.Arena.Models.Network.DeckCard { CardId = card.cardId, Quantity = card.quantity });
            foreach (var card in body.CommandZone) deck.CommandZone.Add(new Wizards.Arena.Models.Network.DeckCard { CardId = card.cardId, Quantity = card.quantity });
            foreach (var card in body.Companions) deck.Companions.Add(new Wizards.Arena.Models.Network.DeckCard { CardId = card.cardId, Quantity = card.quantity });
            foreach (var skin in body.CardSkins) deck.CardSkins.Add(new Wizards.Arena.Models.Network.CardSkin { GrpId = skin.GrpId, CCV = skin.CCV ?? "" });
            response.DecksInternal[summary.DeckId.ToString("D")] = deck;
        }
        response.DecksCacheVersion = CacheVersion(deckVersions.ToString());
        return response;
    }
    async Task<Frame> HandleMatchmaking(Cmd cmd, byte version, CancellationToken ct)
    {
        string deckId = "";
        string? botDeckId = null;
        string eventName = _lastSelectedEventName;
        if (cmd.PayloadCase == Cmd.PayloadOneofCase.JsonPayload) {
            var reqJson = RequestJson(cmd);
            deckId = reqJson["deckId"]?.ToString() ?? reqJson["DeckId"]?.ToString() ?? "";
            botDeckId = reqJson["botDeckId"]?.ToString();
            eventName = EventName(reqJson,eventName);
        }
        if (string.IsNullOrEmpty(deckId) && !string.IsNullOrEmpty(_lastSelectedDeckId)) {
            deckId = _lastSelectedDeckId;
        }
        if (string.IsNullOrEmpty(deckId)) {
            throw new InvalidDataException("No deck selected for match");
        }
        using var startReq = new HttpRequestMessage(HttpMethod.Post, _backend.TrimEnd('/') + "/api/interactive/start");
        startReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        startReq.Content = new StringContent(JsonConvert.SerializeObject(new { deckId, botDeckId, mode=GameMode(eventName) }), Encoding.UTF8, "application/json");
        using var startResp = await _http.SendAsync(startReq, ct);
        startResp.EnsureSuccessStatusCode();
        var started=JObject.Parse(await startResp.Content.ReadAsStringAsync(ct));
        int playerCount=started["playerCount"]?.Value<int>()??2;
        if(playerCount is not (2 or 4))throw new InvalidDataException("Invalid local match player count");

        string matchId = Guid.NewGuid().ToString("D");
        var deckCards = new List<uint>();
        var sbCards = new List<uint>();
        var commanderCards = new List<uint>();
        try {
            var body = (await BackendCommand("DeckGetDeck", JObject.FromObject(new { DeckId = deckId }), ct)).ToObject<Wizards.Unification.Models.Deck.Deck>();
            if (body != null) {
                foreach (var c in body.MainDeck) {
                    for (int i = 0; i < c.quantity; i++) deckCards.Add(c.cardId);
                }
                foreach (var c in body.Sideboard) {
                    for (int i = 0; i < c.quantity; i++) sbCards.Add(c.cardId);
                }
                foreach (var c in body.CommandZone)
                    for (int i=0;i<c.quantity;i++)commanderCards.Add(c.cardId);
            }
        } catch (Exception ex) {
            Console.WriteLine($"Could not load deck cards for {deckId}: {ex.Message}");
        }
        if(started["featureScenario"] != null) {
            deckCards=started["deckCards"]!.Values<uint>().ToList();
            sbCards.Clear();commanderCards.Clear();
        }
        ActiveMatches.Register(matchId, _token!, deckId, deckCards, sbCards, commanderCards);

        var matchInfo = new Wizards.Arena.Models.Network.ClientMatchInfoV4 {
            MatchEndpointHost = "127.0.0.1",
            MatchEndpointPort = 9444,
            MatchId = matchId,
            McFabricId = "fabric:/match/" + matchId,
            EventId = eventName,
            // The quadrant camera moves outside the bounds of two-player scenes.
            // MUL is the installed client's four-quadrant battlefield asset.
            Battlefield = playerCount > 2 ? "MUL" : "Default",
            MatchType = Wizards.Arena.Enums.Event.MatchType.Queue,
            YourSeat = 1
        };
        matchInfo.PlayerInfos.Add(new Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2 {
            ScreenName = "Player", SeatId = 1, TeamId = 1
        });
        for(int seat=2;seat<=playerCount;seat++)
            matchInfo.PlayerInfos.Add(new Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2 {
                ScreenName = playerCount==2?"Forge AI":"Forge AI "+(seat-1), SeatId = seat, TeamId = seat
            });
        var push = new Wizards.Unification.Models.Player.PushNotification(Wizards.Arena.Enums.Player.ENotificationType.MatchCreated) {
            MatchInfoV4 = matchInfo
        };
        var pushResponse = new Response {
            RawTransId = Guid.NewGuid().ToString("D"),
            JsonPayload = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(push))
        };
        PendingPushes.Enqueue(new Frame(version, 1, 2, pushResponse.ToByteArray()));
        Console.WriteLine($"Initiated bot match {matchId} with deck {deckId}; enqueued MatchCreated push notification.");
        return JsonReply(cmd, "", version);
    }
}
