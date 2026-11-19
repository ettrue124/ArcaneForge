using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Wotc.Mtgo.Gre.External.Messaging;
using Action = Wotc.Mtgo.Gre.External.Messaging.Action;

sealed class MatchSessionState
{
    public uint NextMsgId = 1;
    public uint CurrentGameStateId = 1;
    public long LastPendingPromptId = 0;
    public long ButtonEventId = 0;
    public string InputType = "";
    public bool CanUndo;
    public bool CompletionSent;
    public uint MulliganCount;
    uint requestMsgId;
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,(long Button,string Input)> requests=new();
    public uint RequestMsgId {
        get=>requestMsgId;
        set {
            requestMsgId=value; requests[value]=(ButtonEventId,InputType);
            if(value>64)foreach(uint old in requests.Keys.Where(k=>k<value-64))requests.TryRemove(old,out _);
        }
    }
    public bool IsCurrentResponse(uint id) => requests.TryGetValue(id,out var request) && request.Button!=0 && request.Button==ButtonEventId && request.Input==InputType;
    public long LastPublishedButtonId;
    // Spell/ability targets clicked in Arena's native stack picker for the current Forge prompt.
    // Forge only receives them when Arena sends SubmitTargetsReq (the picker's Submit button).
    public readonly HashSet<uint> StackTargetSelection = new();
    public long StackSelectionButton;
    // Trigger keys in resolution order, from Arena's Order Triggers browser; answers Forge's follow-up picks.
    public readonly List<uint> TriggerOrderPlan = new();
    public long TriggerOrderButton;
    // Repeat targeting: targets chosen for a prompt (same card + ability text) this turn are reused when
    // the identical prompt returns (Storm copies, repeated triggers). Full Control disables it.
    public readonly Dictionary<string,uint[]> TargetMemory = new(StringComparer.Ordinal);
    public uint TargetMemoryTurn;
    public string? AutoTargetKey;
    internal static string? TargetKey(JsonElement snapshot) {
        if(!snapshot.TryGetProperty("input",out var input))return null;
        string type=input.GetProperty("type").GetString()??"";
        if(type is not ("InputGuiStackTargets" or "InputSelectTargets"))return null;
        string card=input.TryGetProperty("cardName",out var name)?name.GetString()??"":"";
        string text=input.TryGetProperty("description",out var description)?description.GetString()??"":"";
        return card.Length==0 && text.Length==0 ? null : $"{type}|{card}|{text}";
    }
    internal void Remember(string? key,uint turn,IEnumerable<uint> targets) {
        var chosen=targets.ToArray();
        if(key==null || chosen.Length==0)return;
        if(TargetMemoryTurn!=turn) { TargetMemory.Clear(); TargetMemoryTurn=turn; }
        TargetMemory[key]=chosen;
    }
    public readonly SemaphoreSlim PushGate = new(1, 1);
    public GameStateMessage? PreviousBoard;
    public uint NextAnnotationId = 1;
    public readonly BattleFlow Flow = new();
    public string? LastSnapshotJson;
    public long LastPlaybackId;
    public long LastSettingsRevision;
    public readonly StackPlaybackPacer StackPacer = new();
    public GameplayTrace? Trace;
    public DecisionTimerSettings? Timing;
    public readonly DecisionTimer Decision = new();
    // Forge keeps reporting an answered InputPassPriority while the AI responds.
    // Re-offering that decision left Arena without a Pass button once (trace
    // local-20261026-010226 seq 2705-2711), so hold it back briefly.
    public long PassedButton;
    public long PassedAtMs;
    public const long PassedHoldMs = 3000;
}

static class MatchDoorHost
{
    static readonly HttpClient BattleBackendHttp=new(new SocketsHttpHandler {UseProxy=false}) {Timeout=TimeSpan.FromSeconds(20)};
    internal static GREToClientMessage CreateBottomCardRequest(uint msgId, uint stateId, uint count, IEnumerable<uint> handIds)
    {
        var ids = handIds.ToArray();
        if (count == 0 || count > ids.Length || ids.Distinct().Count() != ids.Length)
            throw new InvalidDataException("Invalid London selection count or hand");
        // Arena's own London mulligan screen (LondonWorkflow) opens for a GroupReq with
        // GroupingContext.LondonMulligan. It reads GroupSpecs[1].LowerBound as the number
        // of cards to put back and replies with a hand group and a library-bottom group.
        // BaseUserRequest ignores prompt IDs 0/1; the workflow supplies its own header text.
        var request = new GREToClientMessage {
            Type = GREMessageType.GroupReq, SystemSeatIds = { 1 }, MsgId = msgId, GameStateId = stateId,
            Prompt = new Prompt { PromptId = 40 },
            GroupReq = new GroupReq { GroupType = GroupType.Ordered, Context = GroupingContext.LondonMulligan }
        };
        request.GroupReq.InstanceIds.AddRange(ids);
        uint keep = (uint)ids.Length - count;
        request.GroupReq.GroupSpecs.Add(new GroupSpecification { LowerBound = keep, UpperBound = keep, ZoneType = ZoneType.Hand, SubZoneType = SubZoneType.Top });
        request.GroupReq.GroupSpecs.Add(new GroupSpecification { LowerBound = count, UpperBound = count, ZoneType = ZoneType.Library, SubZoneType = SubZoneType.Bottom });
        return request;
    }
    internal static GREToClientMessage CreateMulliganRequest(uint msgId, uint stateId, int handSize, uint mulliganCount = 0)
    {
        if (handSize < 0 || handSize > 250) throw new InvalidDataException("Invalid starting hand size");
        return new GREToClientMessage {
            Type = GREMessageType.MulliganReq, SystemSeatIds = { 1 }, MsgId = msgId,
            GameStateId = stateId, MulliganReq = new MulliganReq { MulliganType = MulliganType.London, MulliganCount = mulliganCount },
            Prompt = new Prompt { Parameters = { new PromptParameter { Type = ParameterType.Number, NumberValue = handSize } } }
        };
    }
    internal static bool TryParseManaCost(string cost, IList<ManaRequirement> requirements)
    {
        var parsed=new List<ManaRequirement>();
        var tokens = System.Text.RegularExpressions.Regex.Matches(cost, @"\{([^}]+)\}");
        if (tokens.Count == 0) return string.IsNullOrEmpty(cost) || cost == "0" || cost == "{0}";
        if (string.Concat(tokens.Select(t => t.Value)) != cost) return false;
        foreach (System.Text.RegularExpressions.Match token in tokens) {
            string symbol = token.Groups[1].Value.ToUpperInvariant();
            if (int.TryParse(symbol, out int generic) && generic >= 0) {
                if (generic > 0) {
                    parsed.Add(new ManaRequirement { Count = generic, Color = { ManaColor.Generic } });
                }
                continue;
            }
            if (symbol == "X") {
                parsed.Add(new ManaRequirement { Count = 1, Color = { ManaColor.X } });
                continue;
            }
            if (symbol == "Y") {
                parsed.Add(new ManaRequirement { Count = 1, Color = { ManaColor.Y } });
                continue;
            }
            if (symbol == "S") {
                parsed.Add(new ManaRequirement { Count = 1, Color = { ManaColor.Snow } });
                continue;
            }
            if (symbol.Contains('/')) {
                var parts = symbol.Split('/');
                var req = new ManaRequirement { Count = 1 };
                foreach (var p in parts) {
                    if (p == "P") {
                        req.Color.Add(ManaColor.Phyrexian);
                    } else if (p == "2") {
                        req.Color.Add(ManaColor.TwoGeneric);
                    } else {
                        var c = p switch {
                            "W" => ManaColor.White,
                            "U" => ManaColor.Blue,
                            "B" => ManaColor.Black,
                            "R" => ManaColor.Red,
                            "G" => ManaColor.Green,
                            "C" => ManaColor.Colorless,
                            _ => ManaColor.None
                        };
                        if (c == ManaColor.None) return false;
                        req.Color.Add(c);
                    }
                }
                if (req.Color.Count > 0) parsed.Add(req);
                continue;
            }
            var singleColor = symbol switch {
                "W" => ManaColor.White,
                "U" => ManaColor.Blue,
                "B" => ManaColor.Black,
                "R" => ManaColor.Red,
                "G" => ManaColor.Green,
                "C" => ManaColor.Colorless,
                _ => ManaColor.None
            };
            if (singleColor == ManaColor.None) return false;
            parsed.Add(new ManaRequirement { Count = 1, Color = { singleColor } });
        }
        foreach(var requirement in parsed)requirements.Add(requirement);
        return true;
    }
    internal static bool TryManaCost(string cost, Action action)
    {
        return TryParseManaCost(cost, action.ManaCost);
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Stream, SemaphoreSlim> Writes = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Stream, GameplayTrace> WireTraces = new();
    private static async Task WriteFrame(Stream stream, Frame frame, CancellationToken ct)
    {
        var gate = Writes.GetValue(stream, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try {
            await stream.WriteAsync(Frames.Encode(frame), ct);
            if(WireTraces.TryGetValue(stream,out var trace)) {
                bool auth=frame.Type==1 && frame.Format==2 && MatchServiceToClientMessage.Parser.ParseFrom(frame.Body).AuthenticateResponse!=null;
                if(!auth)trace.RecordFrame("server",frame);
            }
        }
        finally { gate.Release(); }
    }
    internal static Frame? HeartbeatReply(Frame frame) => frame.Type == 2 ? frame with { Type = 3 } : null;
    // Live MatchDoor connections per human-vs-human play room, for relaying emotes.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<Stream, (byte Version, MatchSessionState State)>> Rooms = new();
    const int MaxEmoteLength = 200;
    /// The emote as the opponent's client should receive it, or null when the UI message is not a valid emote.
    internal static MatchServiceToClientMessage? EmoteRelay(UIMessage? ui, uint msgId)
    {
        if (ui?.OnChat is not { } chat || string.IsNullOrEmpty(chat.Text) || chat.Text.Length > MaxEmoteLength) return null;
        return new MatchServiceToClientMessage { GreToClientEvent = new GreToClientEvent { GreToClientMessages = {
            new GREToClientMessage { Type = GREMessageType.Uimessage, MsgId = msgId, SystemSeatIds = { 1 },
                UiMessage = new UIMessage { SeatIds = { 1 }, OnChat = new OnChat { Text = chat.Text } } } } } };
    }
    /// Arena's "not allowed" reply for a prompt answer the host could not apply.
    internal static MatchServiceToClientMessage IllegalRequestReply(ClientToGREMessage gre, uint msgId, uint gameStateId) => new() {
        GreToClientEvent = new GreToClientEvent { GreToClientMessages = {
            new GREToClientMessage { Type = GREMessageType.IllegalRequest, MsgId = msgId, GameStateId = gameStateId, SystemSeatIds = { 1 },
                IllegalRequestMessage = new IllegalRequestMessage { InvalidMessage = gre } } } } };
    public static async Task Serve(TcpClient client, X509Certificate2 cert, string backend, X509Certificate2Collection? extraCertificates = null)
    {
        // Small priority and animation frames must not wait for TCP batching.
        client.NoDelay = true;
        using (client)
        using (var stream = new SslStream(client.GetStream(), false))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
        {
            string? activeMatchId = null;
            string? clientToken = null;
            string? authenticatedClientId = null;
            var state = new MatchSessionState();
            CancellationTokenSource? pollCts = null;
            try {
                var options = new SslServerAuthenticationOptions { EnabledSslProtocols = SslProtocols.Tls12 };
                options.ServerCertificateContext = SslStreamCertificateContext.Create(cert, extraCertificates ?? new X509Certificate2Collection(), offline: true);
                await stream.AuthenticateAsServerAsync(options, timeout.Token);
                Console.WriteLine("MatchDoor client connected and authenticated TLS.");

                while (await Frames.Read(stream, timeout.Token) is { } frame) {
                    if(state.Trace!=null) {
                        bool auth=frame.Type==1 && frame.Format==2 && ClientToMatchServiceMessage.Parser.ParseFrom(frame.Body)
                            .ClientToMatchServiceMessageType==ClientToMatchServiceMessageType.AuthenticateRequest;
                        if(!auth)state.Trace.RecordFrame("client",frame);
                    }
                    if (HeartbeatReply(frame) is { } pong) {
                        await WriteFrame(stream, pong, timeout.Token);
                        continue;
                    }
                    if (frame.Type != 1) continue;
                    if (frame.Format != 2) continue;
                    var msg = ClientToMatchServiceMessage.Parser.ParseFrom(frame.Body);
                    switch (msg.ClientToMatchServiceMessageType) {
                        case ClientToMatchServiceMessageType.AuthenticateRequest: {
                            var authReq = AuthenticateRequest.Parser.ParseFrom(msg.Payload);
                            clientToken = authReq.PlayFabSessionTicket;
                            authenticatedClientId=authReq.ClientId;
                            var authResp = new AuthenticateResponse {
                                ClientId = authReq.ClientId,
                                SessionId = Guid.NewGuid().ToString("D"),
                                InactivityTimeoutMs = 60000,
                                ScreenName = authReq.ClientId
                            };
                            var authRespMsg = new MatchServiceToClientMessage {
                                RequestId = msg.RequestId,
                                TransactionId = msg.TransactionId,
                                AuthenticateResponse = authResp
                            };
                            await WriteFrame(stream, new Frame(frame.Version, 1, 2, authRespMsg.ToByteArray()), timeout.Token);
                            Console.WriteLine($"MatchDoor AuthenticateRequest accepted for ClientId: {authReq.ClientId}");
                            break;
                        }

                        case ClientToMatchServiceMessageType.ClientToMatchDoorConnectRequest: {
                            var connReq = ClientToMatchDoorConnectRequest.Parser.ParseFrom(msg.Payload);
                            activeMatchId = connReq.MatchId;
                            var authorizedMatch=activeMatchId is null?null:ActiveMatches.Get(activeMatchId);
                            if(authorizedMatch is null || string.IsNullOrEmpty(authorizedMatch.PersonaId)
                                || authenticatedClientId!=authorizedMatch.PersonaId)
                                throw new AuthenticationException("Match does not belong to this client");
                            if(!string.IsNullOrEmpty(clientToken)) {
                                using var identityRequest=new HttpRequestMessage(HttpMethod.Get,backend.TrimEnd('/')+"/api/me");
                                identityRequest.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",clientToken);
                                using var identityReply=await BattleBackendHttp.SendAsync(identityRequest,timeout.Token);
                                if(!identityReply.IsSuccessStatusCode)throw new AuthenticationException("Invalid local account session");
                                using var identity=JsonDocument.Parse(await identityReply.Content.ReadAsStringAsync(timeout.Token));
                                var root=identity.RootElement;
                                string persona=root.TryGetProperty("persona_id",out var personaId)?personaId.ToString():root.GetProperty("id").ToString();
                                if(persona!=authorizedMatch.PersonaId)throw new AuthenticationException("Account does not own this match");
                            }
                            bool newTrace=state.Trace==null;
                            state.Trace ??= GameplayTrace.Start(activeMatchId ?? "unknown");
                            if(!WireTraces.TryGetValue(stream,out _))WireTraces.Add(stream,state.Trace);
                            if(newTrace)state.Trace.RecordFrame("client",frame);
                            Console.WriteLine($"Local gameplay trace: {state.Trace.DirectoryPath}");
                            state.Trace.RecordProtobuf("client.match", connReq);
                            if (string.IsNullOrEmpty(clientToken) && activeMatchId != null) {
                                clientToken = ActiveMatches.GetToken(activeMatchId);
                            }
                            Console.WriteLine($"MatchDoor ClientToMatchDoorConnectRequest for match: {activeMatchId}");
                            if (!string.IsNullOrEmpty(authorizedMatch.RoomId))
                                Rooms.GetOrAdd(authorizedMatch.RoomId, _ => new())[stream] = (frame.Version, state);
                            state.Timing = BattleTimers.LoadSettings();
                            if (state.Timing is { } timing)
                                Console.WriteLine($"Decision timer: {timing.Seconds}s, rope at {timing.WarningSeconds}s, loss on timeout {timing.TimeoutsToLose}.");

                            // 1. Send MatchGameRoomStateChangedEvent (Playing)
                            var roomMsg = new MatchServiceToClientMessage {
                                RequestId = msg.RequestId,
                                TransactionId = msg.TransactionId,
                                MatchGameRoomStateChangedEvent = new MatchGameRoomStateChangedEvent {
                                    GameRoomInfo = new MatchGameRoomInfo {
                                        StateType = MatchGameRoomStateType.Playing,
                                        GameRoomConfig = new MatchGameRoomConfig {
                                            MatchId = activeMatchId ?? ""
                                        }
                                    }
                                }
                            };
                            state.Trace.RecordProtobuf("server.match", roomMsg);
                            await WriteFrame(stream, new Frame(frame.Version, 1, 2, roomMsg.ToByteArray()), timeout.Token);

                            // 2. Send ConnectResp
                            var matchCtx = activeMatchId != null ? ActiveMatches.Get(activeMatchId) : null;
                            var deckCards = matchCtx?.DeckCards != null ? new List<uint>(matchCtx.DeckCards) : new List<uint>();
                            var sideboardCards = matchCtx?.SideboardCards != null ? new List<uint>(matchCtx.SideboardCards) : new List<uint>();

                            if (deckCards.Count == 0) {
                                try {
                                    string snapJson = await FetchSnapshot(backend, clientToken, timeout.Token);
                                    using var snapDoc = JsonDocument.Parse(snapJson);
                                    if (snapDoc.RootElement.TryGetProperty("cards", out var cardsArr)) {
                                        foreach (var c in cardsArr.EnumerateArray()) {
                                            if (c.GetProperty("ownerSeat").GetUInt32() == 1) {
                                                uint gid = c.GetProperty("grpId").GetUInt32();
                                                if (gid > 0) deckCards.Add(gid);
                                            }
                                        }
                                    }
                                } catch {}
                            }

                            var deckMsg = new DeckMessage();
                            deckMsg.DeckCards.AddRange(deckCards);
                            deckMsg.SideboardCards.AddRange(sideboardCards);
                            if(matchCtx!=null)deckMsg.CommanderCards.AddRange(matchCtx.CommanderCards);

                            var connectMsg = new MatchServiceToClientMessage {
                                RequestId = msg.RequestId,
                                TransactionId = msg.TransactionId,
                                GreToClientEvent = new GreToClientEvent {
                                    GreToClientMessages = {
                                        new GREToClientMessage {
                                            Type = GREMessageType.ConnectResp,
                                            SystemSeatIds = { 1 },
                                            MsgId = state.NextMsgId++,
                                            GameStateId = state.CurrentGameStateId,
                                            ConnectResp = new ConnectResp {
                                                Status = ConnectionStatus.Success,
                                                ProtoVer = ProtoVersion.PersistentAnnotations,
                                                // The installed ConnectResponseNotificationParser
                                                // forwards this directly to the phase ladder UI.
                                                Settings = state.Flow.SnapshotSettings(),
                                                DeckMessage = deckMsg
                                            }
                                        }
                                    }
                                }
                            };
                            state.Trace.RecordProtobuf("server.match", connectMsg);
                            await WriteFrame(stream, new Frame(frame.Version, 1, 2, connectMsg.ToByteArray()), timeout.Token);

                            // 3. Send initial game state and actions
                            await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);

                            // 4. Start background poll loop
                            pollCts?.Cancel();
                            pollCts = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                            var pollToken = pollCts.Token;
                            _ = Task.Run(() => PollBattle(stream,backend,clientToken,activeMatchId!,frame.Version,state,pollToken),pollToken);
                            break;
                        }

                        case ClientToMatchServiceMessageType.ClientToGremessage:
                        case ClientToMatchServiceMessageType.ClientToGreuimessage: {
                            var gre = ClientToGREMessage.Parser.ParseFrom(msg.Payload);
                            state.Trace?.RecordProtobuf("client.gre", gre);
                            if(state.CompletionSent)break;
                            if (gre.UiMessage != null) {
                                // Emotes go to the other human player. Hover/select/generic UI
                                // signals use per-view object IDs and are not relayed.
                                var room = activeMatchId is null ? "" : ActiveMatches.Get(activeMatchId)?.RoomId ?? "";
                                if (gre.UiMessage.OnChat != null && room.Length > 0 && Rooms.TryGetValue(room, out var peers)) {
                                    foreach (var (peer, info) in peers) {
                                        if (ReferenceEquals(peer, stream)) continue;
                                        if (EmoteRelay(gre.UiMessage, info.State.NextMsgId++) is not { } relay) break;
                                        try { await WriteFrame(peer, new Frame(info.Version, 1, 2, relay.ToByteArray()), timeout.Token); }
                                        catch (Exception e) when (e is IOException or ObjectDisposedException) { }
                                    }
                                }
                                break;
                            }
                            bool answersPrompt=gre.PerformActionResp!=null || gre.SelectTargetsResp!=null || gre.DeclareAttackersResp!=null || gre.DeclareBlockersResp!=null
                                || gre.CastingTimeOptionsResp!=null || gre.SelectNResp!=null || gre.MulliganResp!=null || gre.OptionalResp!=null || gre.ChooseStartingPlayerResp!=null
                                || gre.NumericInputResp!=null || gre.StringInputResp!=null || gre.OrderResp!=null || gre.GroupResp!=null || gre.DistributionResp!=null || gre.AssignDamageResp!=null || gre.CancelActionReq!=null
                                || gre.PerformAutoTapActionsResp!=null || gre.SearchFromGroupsResp!=null || gre.SelectCountersResp!=null || gre.SelectFromGroupsResp!=null || gre.SubmitDeckResp!=null || gre.GatherResp!=null
                                || gre.SearchResp!=null || gre.SelectReplacementResp!=null || gre.SelectNGroupResp!=null
                                || gre.Type is (ClientMessageType)31 or (ClientMessageType)33 or (ClientMessageType)37;
                            if(answersPrompt && gre.RespId != 0 && !state.IsCurrentResponse(gre.RespId)) {
                                Console.WriteLine("Ignored a response to a superseded Forge input; refreshing the current request.");
                                state.LastPublishedButtonId=0;
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                                break;
                            }
                            if (gre.Type == ClientMessageType.GetSettingsReq) {
                                var response=new GREToClientMessage {Type=GREMessageType.GetSettingsResp,MsgId=state.NextMsgId++,SystemSeatIds={1},GetSettingsResp=new GetSettingsResp {Settings=state.Flow.SnapshotSettings()}};
                                var settingsReply = new MatchServiceToClientMessage {GreToClientEvent=new GreToClientEvent {GreToClientMessages={response}}};
                                state.Trace?.RecordProtobuf("server.gre", settingsReply);
                                await WriteFrame(stream,new Frame(frame.Version,1,2,settingsReply.ToByteArray()),timeout.Token);
                            } else if (gre.SetSettingsReq != null) {
                                state.Flow.Settings(gre.SetSettingsReq.Settings, gre.SetSettingsReq.TurnNumber != 0
                                    ? gre.SetSettingsReq.TurnNumber : state.PreviousBoard?.TurnInfo?.TurnNumber ?? 1);
                                var response = new GREToClientMessage { Type = (GREMessageType)10, MsgId = state.NextMsgId++,
                                    SystemSeatIds = { 1 }, SetSettingsResp = new SetSettingsResp { Settings = state.Flow.SnapshotSettings() } };
                                var settingsReply = new MatchServiceToClientMessage {
                                    GreToClientEvent = new GreToClientEvent { GreToClientMessages = { response } }
                                };
                                state.Trace?.RecordProtobuf("server.gre", settingsReply);
                                await WriteFrame(stream, new Frame(frame.Version, 1, 2, settingsReply.ToByteArray()), timeout.Token);
                                state.LastPublishedButtonId = 0;
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if(gre.Type==ClientMessageType.UndoReq) {
                                // Arena's native undo deliberately has RespId=0.
                                // Forge validates reversibility and current priority.
                                if(state.CanUndo && state.InputType=="InputPassPriority")
                                    await SendEngineAction(state,backend,clientToken,JsonSerializer.Serialize(new {type="undo",eventId=state.ButtonEventId}),timeout.Token);
                                else if(state.InputType.StartsWith("InputPayMana",StringComparison.Ordinal))
                                    await SendEngineAction(state,backend,clientToken,JsonSerializer.Serialize(new {type="cancel",eventId=state.ButtonEventId}),timeout.Token);
                                else state.LastPublishedButtonId=0;
                                await Task.Delay(30,timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if(state.InputType=="InputGuiChoice" && gre.SelectNResp is {} triggerOrder && triggerOrder.Ids.Count>1
                                && triggerOrder.Ids.All(id=>id>=BattlePresentation.TriggerOrderBase && id<BattlePresentation.TriggerOrderBase+1_000_000u)) {
                                // Order Triggers browser: Arena lists left ("Last") to right ("First"); Forge wants first-to-resolve first.
                                state.TriggerOrderPlan.Clear();
                                state.TriggerOrderPlan.AddRange(triggerOrder.Ids.Reverse().Select(id=>id-BattlePresentation.TriggerOrderBase));
                                state.TriggerOrderButton=state.ButtonEventId;
                                state.LastPublishedButtonId=0;
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if(state.InputType=="InputGuiStackTargets" && gre.SelectTargetsResp!=null) {
                                // A click only updates the selection; Arena keeps its picker open until Submit.
                                // Answering Forge here left the picker (Submit 0) over the next prompt.
                                if(state.StackSelectionButton!=state.ButtonEventId) { state.StackTargetSelection.Clear(); state.StackSelectionButton=state.ButtonEventId; }
                                uint maxTargets=1;
                                if(state.LastSnapshotJson!=null) {
                                    using var targetSnapshot=JsonDocument.Parse(state.LastSnapshotJson);
                                    if(targetSnapshot.RootElement.GetProperty("input").TryGetProperty("max",out var maxProp))maxTargets=maxProp.GetUInt32();
                                }
                                foreach(var clicked in gre.SelectTargetsResp.Target.Targets.Select(t=>t.TargetInstanceId)) {
                                    if(!state.StackTargetSelection.Remove(clicked)) {
                                        if(maxTargets<=1)state.StackTargetSelection.Clear();
                                        if(state.StackTargetSelection.Count<maxTargets)state.StackTargetSelection.Add(clicked);
                                    }
                                }
                                state.LastPublishedButtonId=0;
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if(state.InputType=="InputGuiStackTargets" && gre.Type==(ClientMessageType)37) {
                                // SubmitTargetsReq: hand Forge the spells/abilities picked in the native picker.
                                var chosenTargets=state.StackSelectionButton==state.ButtonEventId ? state.StackTargetSelection.ToArray() : Array.Empty<uint>();
                                state.StackTargetSelection.Clear();
                                if(state.LastSnapshotJson!=null) {
                                    using var submitted=JsonDocument.Parse(state.LastSnapshotJson);
                                    state.Remember(MatchSessionState.TargetKey(submitted.RootElement),
                                        submitted.RootElement.GetProperty("turn").GetProperty("number").GetUInt32(),chosenTargets);
                                }
                                await SendEngineAnswer(backend,clientToken,state.ButtonEventId,chosenTargets,timeout.Token);
                                await Task.Delay(30,timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if(BattleGuiResponses.TryAnswer(gre,state.InputType,out var guiAnswer,state.LastSnapshotJson)) {
                                await SendEngineAnswer(backend,clientToken,state.ButtonEventId,guiAnswer!,timeout.Token);
                                await Task.Delay(30,timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if(gre.CastingTimeOptionsResp?.CastingTimeOptionResp?.NumericInputResp is { } numeric) {
                                if(state.InputType!="InputChooseNumber" || gre.CastingTimeOptionsResp.CastingTimeOptionResp.CtoId!=state.ButtonEventId)
                                    throw new InvalidDataException("Number response without its current Forge prompt");
                                await SendEngineAnswer(backend,clientToken,state.ButtonEventId,numeric.NumericInputValue,timeout.Token);
                                await Task.Delay(30,timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if (gre.OptionalResp != null) {
                                if(state.InputType!="InputConfirm" || (int)gre.OptionalResp.Response is not (1 or 2 or -1))
                                    throw new InvalidDataException("Invalid optional confirmation response");
                                await SendEngineAction(state,backend,clientToken,JsonSerializer.Serialize(new { type=(int)gre.OptionalResp.Response==1?"ok":"cancel",eventId=state.ButtonEventId }),timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if (gre.ChooseStartingPlayerResp != null) {
                                if (state.InputType != "InputConfirm" || gre.ChooseStartingPlayerResp.SystemSeatId is not (1 or 2))
                                    throw new InvalidDataException("Invalid starting player response");
                                await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = gre.ChooseStartingPlayerResp.SystemSeatId == 1 ? "ok" : "cancel", eventId = state.ButtonEventId }), timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.Type is (ClientMessageType)31 or (ClientMessageType)33 or (ClientMessageType)37) {
                                string expectedInput=(int)gre.Type switch {31=>"InputAttack",33=>"InputBlock",_=>"InputSelectTargets"};
                                if(state.InputType!=expectedInput) {
                                    state.LastPublishedButtonId=0;
                                    await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                                    break;
                                }
                                if(expectedInput=="InputSelectTargets" && state.LastSnapshotJson!=null) {
                                    using var submitted=JsonDocument.Parse(state.LastSnapshotJson);
                                    var submittedInput=submitted.RootElement.GetProperty("input");
                                    if(submittedInput.TryGetProperty("selected",out var picked))
                                        state.Remember(MatchSessionState.TargetKey(submitted.RootElement),
                                            submitted.RootElement.GetProperty("turn").GetProperty("number").GetUInt32(),
                                            picked.EnumerateArray().Select(t=>t.GetUInt32()));
                                }
                                await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "ok", eventId = state.ButtonEventId }), timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.SelectTargetsResp != null || gre.DeclareAttackersResp != null || gre.DeclareBlockersResp != null) {
                                string action;
                                if (gre.SelectTargetsResp != null) {
                                    if (gre.SelectTargetsResp.Target.Targets.Count != 1) throw new InvalidDataException("Expected one native target update");
                                    action = JsonSerializer.Serialize(new { type = "selectTarget", instanceId = gre.SelectTargetsResp.Target.Targets[0].TargetInstanceId, eventId = state.ButtonEventId });
                                } else if (gre.DeclareAttackersResp != null) {
                                    action = gre.DeclareAttackersResp.AutoDeclare ? JsonSerializer.Serialize(new { type="alphaStrike",eventId=state.ButtonEventId })
                                        : JsonSerializer.Serialize(new { type = "attackers", submit = false, assignments = gre.DeclareAttackersResp.SelectedAttackers.Select(a => new { instanceId = a.AttackerInstanceId, recipient = a.SelectedDamageRecipient == null ? 0 : BattleRequests.RecipientId(a.SelectedDamageRecipient) }), eventId = state.ButtonEventId });
                                } else {
                                    var blocks=gre.DeclareBlockersResp ?? throw new InvalidDataException("Missing blocker response");
                                    action = JsonSerializer.Serialize(new { type = "blockers", submit = false, assignments = blocks.SelectedBlockers.Select(b => new { instanceId = b.BlockerInstanceId, recipients = b.SelectedAttackerInstanceIds.ToArray() }), eventId = state.ButtonEventId });
                                }
                                await SendEngineAction(state, backend, clientToken, action, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.CancelActionReq != null) {
                                if(state.InputType=="InputGuiChoice") {
                                    using var current=JsonDocument.Parse(state.LastSnapshotJson!);
                                    var inp=current.RootElement.GetProperty("input");
                                    int min=inp.TryGetProperty("min",out var m)?m.GetInt32():inp.TryGetProperty("modeMin",out var mm)?mm.GetInt32():1;
                                    if(min!=0)
                                        state.LastPublishedButtonId=0;
                                    else
                                        await SendEngineAnswer(backend,clientToken,state.ButtonEventId,Array.Empty<uint>(),timeout.Token);
                                } else if(state.InputType=="InputChooseNumber" || state.InputType.StartsWith("InputGui",StringComparison.Ordinal))
                                    state.LastPublishedButtonId=0;
                                else await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "cancel", eventId = state.ButtonEventId }), timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if (gre.PerformActionResp != null) {
                                var act = gre.PerformActionResp.Actions.FirstOrDefault();
                                if(state.InputType=="InputConfirm" && state.LastSnapshotJson!=null) {
                                    using var confirmation=JsonDocument.Parse(state.LastSnapshotJson);
                                    if(confirmation.RootElement.GetProperty("input").TryGetProperty("freeCastCardId",out var offered)) {
                                        if(gre.PerformActionResp.Actions.Count!=1 || act==null ||
                                            !(act.ActionType==ActionType.Pass || act.ActionType is ActionType.Cast or ActionType.Play && act.InstanceId==offered.GetUInt32() && act.Selection==0))
                                            throw new InvalidDataException("Invalid free-cast confirmation action");
                                        await SendEngineAction(state,backend,clientToken,JsonSerializer.Serialize(new {
                                            type=act.ActionType==ActionType.Pass?"cancel":"ok",eventId=state.ButtonEventId}),timeout.Token);
                                        await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                                        continue;
                                    }
                                }
                                if(act!=null && act.Selection==0 && act.ActionType!=ActionType.Pass && state.LastSnapshotJson!=null) {
                                    using var actionSnapshot=JsonDocument.Parse(state.LastSnapshotJson);
                                    var matches=BattlePresentation.Actions(actionSnapshot.RootElement).Where(a=>a.InstanceId==act.InstanceId
                                        && a.ActionType==act.ActionType && (act.GrpId==0 || a.GrpId==act.GrpId) && (act.AbilityGrpId==0 || a.AbilityGrpId==act.AbilityGrpId)).ToArray();
                                    if(matches.Length==0 && act.GrpId!=0) {
                                        matches=BattlePresentation.Actions(actionSnapshot.RootElement).Where(a=>a.InstanceId==act.InstanceId && a.GrpId==act.GrpId).ToArray();
                                    }
                                    if(matches.Length==1)act.Selection=matches[0].Selection;
                                }
                                bool? passIntent=act!=null && act.ActionType is ActionType.Cast or ActionType.CastMdfc or ActionType.Activate
                                    ? gre.PerformActionResp.AutoPassPriority==(AutoPassPriority)2 : null;
                                if (act == null || act.ActionType == ActionType.Pass || act.InstanceId == 0) {
                                    if(state.LastSnapshotJson!=null) {
                                        using var passedSnapshot=JsonDocument.Parse(state.LastSnapshotJson);
                                        state.Flow.ManualPass(passedSnapshot.RootElement);
                                    }
                                    if(state.InputType=="InputGuiChoice" && state.LastSnapshotJson!=null) {
                                        using var current=JsonDocument.Parse(state.LastSnapshotJson);
                                        if(current.RootElement.TryGetProperty("input",out var inp) && inp.TryGetProperty("min",out var m) && m.GetInt32()==0) {
                                            await SendEngineAnswer(backend,clientToken,state.ButtonEventId,Array.Empty<uint>(),timeout.Token);
                                            await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                                            continue;
                                        }
                                    }
                                    if(state.InputType=="InputPassPriority") { state.PassedButton=state.ButtonEventId; state.PassedAtMs=Environment.TickCount64; }
                                    await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "ok", eventId = state.ButtonEventId }), timeout.Token);
                                } else {
                                    await SendEngineAction(state, backend, clientToken, act.Selection != 0
                                        ? JsonSerializer.Serialize(new { type = "playAbility", abilityId = act.Selection, eventId = state.ButtonEventId })
                                        : JsonSerializer.Serialize(new { type = "selectCard", instanceId = act.InstanceId, eventId = state.ButtonEventId }), timeout.Token,passIntent);
                                }
                                await Task.Delay(50, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.PerformAutoTapActionsResp != null) {
                                // Auto-tap acknowledgments can arrive after the
                                // payment prompt has finished. Never treat one
                                // as a pass/confirmation for the next decision.
                                if(state.InputType.StartsWith("InputPayMana",StringComparison.Ordinal))
                                    await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "ok", eventId = state.ButtonEventId }), timeout.Token);
                                else state.LastPublishedButtonId=0;
                                await Task.Delay(50, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if(gre.SelectNResp!=null && state.InputType=="InputConfirm") {
                                if(gre.SelectNResp.Ids.Count!=1 || gre.SelectNResp.Ids[0] is not (1 or 2))throw new InvalidDataException("Invalid confirmation choice");
                                await SendEngineAction(state,backend,clientToken,JsonSerializer.Serialize(new {type=gre.SelectNResp.Ids[0]==1?"ok":"cancel",eventId=state.ButtonEventId}),timeout.Token);
                                await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                            } else if (gre.SelectNResp != null && state.InputType is "InputSelectCardsFromList" or "InputSelectEntitiesFromList") {
                                await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "selectMany", instanceIds = gre.SelectNResp.Ids.ToArray(), eventId = state.ButtonEventId }), timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if ((gre.GroupResp != null || gre.SelectNResp != null) && state.InputType == "InputLondonMulligan") {
                                // London screen replies with groups; the library-bottom group holds the cards to put back.
                                var bottomIds = gre.GroupResp != null
                                    ? gre.GroupResp.Groups.Where(g => g.ZoneType == ZoneType.Library).SelectMany(g => g.Ids).ToArray()
                                    : gre.SelectNResp!.Ids.ToArray();
                                await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "bottomCards", instanceIds = bottomIds, eventId = state.ButtonEventId }), timeout.Token);
                                await Task.Delay(50, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.MulliganResp != null) {
                                bool accept = gre.MulliganResp.Decision == MulliganOption.AcceptHand;
                                if (state.InputType != "InputConfirmMulligan") throw new InvalidDataException("Mulligan response without a Forge mulligan input");
                                await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = accept ? "ok" : "cancel", eventId = state.ButtonEventId }), timeout.Token);
                                await Task.Delay(50, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.SubmitDeckResp != null) {
                                // A deck response outside sideboarding must not
                                // replace the match deck or confirm/pass a turn.
                                if(state.InputType!="InputSubmitDeck") {
                                    state.LastPublishedButtonId=0;
                                    await PushStateAndActions(stream,backend,clientToken,activeMatchId,frame.Version,state,timeout.Token);
                                    break;
                                }
                                var submittedDeck = gre.SubmitDeckResp.Deck;
                                if (submittedDeck != null && activeMatchId != null) {
                                    var matchCtx = ActiveMatches.Get(activeMatchId);
                                    if (matchCtx != null) {
                                        matchCtx.DeckCards = submittedDeck.DeckCards.ToList();
                                        matchCtx.SideboardCards = submittedDeck.SideboardCards.ToList();
                                    }
                                }
                                var conf = new GREToClientMessage {
                                    Type = GREMessageType.SubmitDeckConfirmation,
                                    MsgId = state.NextMsgId++,
                                    GameStateId = state.CurrentGameStateId,
                                    SystemSeatIds = { 1 }
                                };
                                var confReply = new MatchServiceToClientMessage {
                                    GreToClientEvent = new GreToClientEvent { GreToClientMessages = { conf } }
                                };
                                state.Trace?.RecordProtobuf("server.gre", confReply);
                                await WriteFrame(stream, new Frame(frame.Version, 1, 2, confReply.ToByteArray()), timeout.Token);
                                await SendEngineAction(state, backend, clientToken, JsonSerializer.Serialize(new { type = "ok", eventId = state.ButtonEventId }), timeout.Token);
                                await Task.Delay(50, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else if (gre.ConcedeReq != null) {
                                await SendEngineAction(state, backend, clientToken, "{\"type\":\"concede\"}", timeout.Token);
                                // Concede runs on Forge's EDT. Keep polling until its actual
                                // outcome is available instead of racing it with an empty result.
                                await Task.Delay(50, timeout.Token);
                                await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                            } else {
                                // Previously dropped silently. Log it so live play shows which gaps occur.
                                Console.WriteLine($"MatchDoor: unhandled client message {gre.Type} while Forge waits on {state.InputType}.");
                                if (answersPrompt) {
                                    var illegal = IllegalRequestReply(gre, state.NextMsgId++, state.CurrentGameStateId);
                                    state.Trace?.RecordProtobuf("server.gre", illegal);
                                    await WriteFrame(stream, new Frame(frame.Version, 1, 2, illegal.ToByteArray()), timeout.Token);
                                    state.LastPublishedButtonId = 0;
                                    await PushStateAndActions(stream, backend, clientToken, activeMatchId, frame.Version, state, timeout.Token);
                                }
                            }
                            break;
                        }
                        default:
                            Console.WriteLine($"MatchDoor: unhandled match service message {msg.ClientToMatchServiceMessageType}.");
                            break;
                    }
                }
                pollCts?.Cancel();
            } catch (Exception ex) when (ex is IOException or AuthenticationException or OperationCanceledException or InvalidProtocolBufferException) {
                Console.WriteLine($"MatchDoor connection ended: {ex.GetType().Name}");
            } finally {
                pollCts?.Cancel();
                foreach (var (room, peers) in Rooms) {
                    if (peers.TryRemove(stream, out _) && peers.IsEmpty) Rooms.TryRemove(room, out _);
                }
                if (state.Trace is not null) {
                    Console.WriteLine($"Saved local gameplay trace: {state.Trace.DirectoryPath}");
                    state.Trace.Dispose();
                    WireTraces.Remove(stream);
                }
            }
        }
    }

    private static async Task PushStateAndActions(Stream stream, string backend, string? token, string? matchId, byte version, MatchSessionState state, CancellationToken ct, string? polled = null)
    {
        await state.PushGate.WaitAsync(ct);
        try {
        if(state.CompletionSent)return;
        string pollJson = polled ?? await FetchPoll(backend, token, ct);
        using var pollDoc = JsonDocument.Parse(pollJson);
        var root = pollDoc.RootElement;
        long currentButton = root.TryGetProperty("buttonEventId", out var currentButtonId) ? currentButtonId.GetInt64() : 0;
        bool unchangedPoll=currentButton==state.LastPublishedButtonId && state.LastSnapshotJson!=null
            && root.TryGetProperty("events",out var pollEvents) && pollEvents.GetArrayLength()==0
            && root.TryGetProperty("pending",out var pollPending) && pollPending.GetArrayLength()==0;
        // Animation timer ticks do not need another full mana/AI legality scan.
        string snapshotJson = unchangedPoll?state.LastSnapshotJson!:await FetchSnapshot(backend, token, ct, state.LastPlaybackId);
        using var snapDoc = JsonDocument.Parse(snapshotJson);
        currentButton = snapDoc.RootElement.TryGetProperty("buttonEventId", out var snapshotButton) ? snapshotButton.GetInt64() : currentButton;
        bool onStack=snapDoc.RootElement.TryGetProperty("stack",out var stack) && stack.GetArrayLength()>0;
        if (currentButton == state.LastPublishedButtonId && snapshotJson == state.LastSnapshotJson) {
            // The animation pause needs a timer tick, not another identical
            // board/playback update. Settings changes force a fresh publication.
            if(state.StackPacer.Pending) {
                if(!state.Flow.ShouldPass(snapDoc.RootElement))state.StackPacer.Reset();
                else if(state.StackPacer.Ready(currentButton,onStack,Environment.TickCount64)) {
                    await SendEngineAction(backend,token,JsonSerializer.Serialize(new {type="ok",eventId=currentButton}),ct);
                    state.StackPacer.Reset();
                    state.LastSnapshotJson = null;
                    state.LastPublishedButtonId = 0;
                    if(state.InputType=="InputPassPriority" && onStack)state.Flow.PriorityPassed();
                }
            }
            return;
        }
        state.Trace?.Record("forge.snapshot", snapDoc.RootElement.Clone());
        state.CurrentGameStateId++;
        var greStateMsg = ForgeEncoder.Encode(snapshotJson, state.NextMsgId++, state.CurrentGameStateId, matchId);
        var board = greStateMsg.GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        BattlePresentation.StateTargets(snapDoc.RootElement,board,ref state.NextAnnotationId);
        var playback=BattlePresentation.Playback(snapDoc.RootElement,state.PreviousBoard,board,state.LastPlaybackId,ref state.NextAnnotationId);
        BattlePresentation.Transitions(state.PreviousBoard, board, ref state.NextAnnotationId,playback);
        BattlePresentation.BatchMillAnimations(board);
        bool completed=board.GameInfo.Stage==GameStage.GameOver && board.GameInfo.Results.Count>0;
        if(completed)BattleCompletion.LossAnnotation(snapDoc.RootElement,board,ref state.NextAnnotationId);
        state.InputType = snapDoc.RootElement.GetProperty("input").GetProperty("type").GetString() ?? "";
        state.ButtonEventId = currentButton;
        bool shouldPass=!completed && state.ButtonEventId!=0 && state.Flow.ShouldPass(snapDoc.RootElement);
        // Trigger order is only asked in Full Control (held or toggled) or with auto-pass cleared;
        // otherwise take Forge's default order (its first option) without showing a prompt.
        uint? autoTriggerOrder=null;
        if(!completed && state.ButtonEventId!=0 && state.InputType=="InputGuiChoice"
            && snapDoc.RootElement.GetProperty("input") is var orderInput
            && orderInput.TryGetProperty("title",out var orderTitle)
            && (orderTitle.GetString()??"") is var orderText
            && (orderText.StartsWith("Order your triggers",StringComparison.Ordinal) || orderText.StartsWith("Choose the trigger to resolve next",StringComparison.Ordinal))
            && orderInput.TryGetProperty("options",out var orderOptions) && orderOptions.GetArrayLength()>0) {
            // A new ordering session discards an older plan; its own first prompt keeps the plan just chosen.
            if(orderText.StartsWith("Order your triggers",StringComparison.Ordinal) && state.TriggerOrderButton!=state.ButtonEventId)
                state.TriggerOrderPlan.Clear();
            if(state.TriggerOrderPlan.Count>0 && orderInput.TryGetProperty("triggers",out var orderTriggers)) {
                var optionByKey=new Dictionary<uint,uint>();
                foreach(var trigger in orderTriggers.EnumerateArray())
                    optionByKey.TryAdd(trigger.GetProperty("key").GetUInt32(),trigger.GetProperty("option").GetUInt32());
                while(state.TriggerOrderPlan.Count>0 && !optionByKey.ContainsKey(state.TriggerOrderPlan[0]))state.TriggerOrderPlan.RemoveAt(0);
                if(state.TriggerOrderPlan.Count>0) { autoTriggerOrder=optionByKey[state.TriggerOrderPlan[0]]; state.TriggerOrderPlan.RemoveAt(0); }
            }
            // Only Full Control (held or toggled) or cleared auto-pass asks; otherwise Forge's default order.
            if(autoTriggerOrder==null && (int)state.Flow.Mode is not (7 or 4))
                autoTriggerOrder=orderOptions[0].GetProperty("id").GetUInt32();
        }
        // Repeat targeting: an identical prompt this turn (same card + ability text) reuses the remembered
        // targets while they are still legal. Spell targets answer at once; a permanent/player target is
        // selected first and submitted on the following update. Full Control always asks.
        uint[]? autoTargets=null;
        bool autoTargetSubmit=false;
        string? targetKey=!completed && state.ButtonEventId!=0 && (int)state.Flow.Mode!=7 ? MatchSessionState.TargetKey(snapDoc.RootElement) : null;
        if(targetKey!=null) {
            uint turnNow=snapDoc.RootElement.GetProperty("turn").GetProperty("number").GetUInt32();
            if(state.TargetMemoryTurn!=turnNow) { state.TargetMemory.Clear(); state.TargetMemoryTurn=turnNow; }
            var targetInput=snapDoc.RootElement.GetProperty("input");
            var selectedNow=targetInput.TryGetProperty("selected",out var selectedTargets)
                ? selectedTargets.EnumerateArray().Select(v=>v.GetUInt32()).ToHashSet() : new HashSet<uint>();
            if(state.AutoTargetKey==targetKey && state.TargetMemory.TryGetValue(targetKey,out var applied) && selectedNow.SetEquals(applied))
                autoTargetSubmit=true;
            else if(selectedNow.Count==0 && state.TargetMemory.TryGetValue(targetKey,out var remembered)) {
                var offered=targetInput.GetProperty("targets").EnumerateArray().Select(v=>v.GetUInt32()).ToHashSet();
                uint minTargets=targetInput.GetProperty("min").GetUInt32(),maxTargets=targetInput.GetProperty("max").GetUInt32();
                bool stackPrompt=state.InputType=="InputGuiStackTargets";
                if(remembered.All(offered.Contains) && remembered.Length>=minTargets && remembered.Length<=maxTargets
                    && (stackPrompt || remembered.Length==1))
                    autoTargets=remembered;
            }
        }
        // Matches are untimed unless match-settings.json enables the decision
        // timer; EnforceDecisionTimer then owns the rope and its timeouts.
        var followups=new List<GREToClientMessage>();
        if(state.LastSettingsRevision!=state.Flow.SettingsRevision) {
            var settingsUpdate=new GREToClientMessage {Type=GREMessageType.SetSettingsResp,MsgId=state.NextMsgId++,GameStateId=state.CurrentGameStateId,SystemSeatIds={1},SetSettingsResp=new SetSettingsResp {Settings=state.Flow.SnapshotSettings()}};
            followups.Add(settingsUpdate);
        }
        state.MulliganCount = snapDoc.RootElement.TryGetProperty("mulliganCount", out var count) ? count.GetUInt32() : 0;
        if(completed) {
            state.RequestMsgId=state.NextMsgId++;
            followups.Add(BattleCompletion.Intermission(board,state.RequestMsgId,1));
        }
        else if (state.InputType == "InputConfirmMulligan" && state.ButtonEventId != 0) {
            int handSize = board.Zones.Single(z=>z.ZoneId==102).ObjectInstanceIds.Count;
            state.RequestMsgId = state.NextMsgId++;
            followups.Add(CreateMulliganRequest(state.RequestMsgId,state.CurrentGameStateId,handSize,state.MulliganCount));
        }
        else if (state.InputType == "InputLondonMulligan" && state.ButtonEventId != 0) {
            state.RequestMsgId = state.NextMsgId++;
            uint bottomCount=snapDoc.RootElement.TryGetProperty("bottomCount",out var bottom)?bottom.GetUInt32():state.MulliganCount;
            followups.Add(CreateBottomCardRequest(state.RequestMsgId,state.CurrentGameStateId,bottomCount,board.Zones.Single(z=>z.ZoneId==102).ObjectInstanceIds));
        }
        else if(state.InputType=="InputPassPriority" && state.ButtonEventId==state.PassedButton
            && Environment.TickCount64-state.PassedAtMs<MatchSessionState.PassedHoldMs) {
            // Already passed; publish the board only. PollBattle re-offers the
            // decision if Forge is still on it once the hold expires.
        }
        else if(autoTriggerOrder!=null || autoTargets!=null || autoTargetSubmit) {
            // Answered below after the board is published; no prompt for the player.
        }
        else if(!shouldPass && state.ButtonEventId!=0) {
            var request=BattleRequests.Create(snapDoc.RootElement,state.NextMsgId,state.CurrentGameStateId);
            bool payingMana=state.InputType.StartsWith("InputPayMana",StringComparison.Ordinal);
            if(request==null && (state.InputType=="InputPassPriority" || payingMana)) {
                request=new GREToClientMessage {Type=GREMessageType.ActionsAvailableReq,SystemSeatIds={1},MsgId=state.NextMsgId,GameStateId=state.CurrentGameStateId,
                    ActionsAvailableReq=new ActionsAvailableReq {Actions={new Action {ActionType=ActionType.Pass}}}};
                foreach(var offered in BattlePresentation.Actions(snapDoc.RootElement)) {
                    if(offered.Highlight==HighlightType.Cold)
                        request.ActionsAvailableReq.InactiveActions.Add(offered);
                    else request.ActionsAvailableReq.Actions.Add(offered);
                }
            }
            if(request?.SelectTargetsReq is {} stackRequest && state.InputType=="InputGuiStackTargets"
                && state.StackSelectionButton==state.ButtonEventId && state.StackTargetSelection.Count>0) {
                // Forge's GUI choice has no running selection; show the host-tracked one.
                var targetSet=stackRequest.Targets[0];
                foreach(var t in targetSet.Targets)
                    if(state.StackTargetSelection.Contains(t.TargetInstanceId))t.LegalAction=SelectAction.Unselect;
                targetSet.SelectedTargets=(uint)state.StackTargetSelection.Count;
            }
            if(request!=null) {
                request.AllowUndo=(state.InputType=="InputPassPriority" || payingMana) && snapDoc.RootElement.TryGetProperty("canUndo",out var canUndo) && canUndo.GetBoolean();
                // Preserve the payment adapter's mandatory-cost restriction.
                // Overwriting it here used to expose Cancel on mandatory costs.
                state.RequestMsgId=state.NextMsgId++;followups.Add(request);
            }
        }
        if(state.Timing is { } timing) {
            // Only a decision actually put to the player, with a safe default answer, is timed.
            long now=Environment.TickCount64;
            bool timed=!completed && followups.Any(m=>m.Type!=GREMessageType.SetSettingsResp)
                && BattleTimers.TimeoutAnswer(snapDoc.RootElement)!=null;
            state.Decision.Track(state.ButtonEventId,state.InputType,timed,now);
            BattleTimers.Apply(board,1,state.Decision,timing,now);
        }
        var wire=greStateMsg.GreToClientEvent.GreToClientMessages.Single();
        wire.GameStateMessage=BattleUpdates.WireState(board,state.PreviousBoard);
        var batch=BattleUpdates.Batch(wire,followups);
        state.Trace?.RecordProtobuf("server.gre", batch);
        await WriteFrame(stream,new Frame(version,1,2,batch.ToByteArray()),ct);
        // Commit state/cursors only after the complete board+prompt batch is sent.
        state.PreviousBoard=board.Clone();state.LastPlaybackId=playback.Cursor;
        state.LastSnapshotJson=snapshotJson;state.LastPublishedButtonId=state.ButtonEventId;
        state.LastSettingsRevision=state.Flow.SettingsRevision;
        state.CanUndo=followups.Any(m=>m.AllowUndo);
        if(completed) {
            state.StackPacer.Reset();
            var completion = BattleCompletion.Notification(board);
            state.Trace?.RecordProtobuf("server.match", completion);
            await WriteFrame(stream,new Frame(version,1,2,completion.ToByteArray()),ct);
            state.CompletionSent=true;
            Console.WriteLine("Sent final Forge outcome, Arena intermission and match completion.");
            return;
        }
        if(autoTargetSubmit) {
            state.AutoTargetKey=null;
            await SendEngineAction(backend,token,JsonSerializer.Serialize(new {type="ok",eventId=state.ButtonEventId}),ct);
            state.LastSnapshotJson=null;
            state.LastPublishedButtonId=0;
            return;
        }
        if(autoTargets!=null) {
            if(state.InputType=="InputGuiStackTargets")
                await SendEngineAnswer(backend,token,state.ButtonEventId,autoTargets,ct);
            else {
                state.AutoTargetKey=targetKey;
                await SendEngineAction(backend,token,JsonSerializer.Serialize(new {type="selectTarget",instanceId=autoTargets[0],eventId=state.ButtonEventId}),ct);
            }
            state.LastSnapshotJson=null;
            state.LastPublishedButtonId=0;
            return;
        }
        if(autoTriggerOrder is uint firstTrigger) {
            await SendEngineAnswer(backend,token,state.ButtonEventId,new uint[]{firstTrigger},ct);
            state.LastSnapshotJson=null;
            state.LastPublishedButtonId=0;
            return;
        }
        if(shouldPass) {
            if(!state.StackPacer.Ready(state.ButtonEventId,onStack,Environment.TickCount64))return;
            await SendEngineAction(backend,token,JsonSerializer.Serialize(new {type="ok",eventId=state.ButtonEventId}),ct);
            state.StackPacer.Reset();
            state.LastSnapshotJson = null;
            state.LastPublishedButtonId = 0;
            if(state.InputType=="InputPassPriority" && onStack)state.Flow.PriorityPassed();
        }
        else state.StackPacer.Reset();
        } finally { state.PushGate.Release(); }
    }

    internal static async Task PollBattle(Stream stream,string backend,string? token,string matchId,byte version,MatchSessionState state,CancellationToken ct)
    {
        int failures=0;
        while(!ct.IsCancellationRequested) {
            try {
                await Task.Delay(failures==0?75:Math.Min(2000,250*failures),ct);
                string json=await FetchPoll(backend,token,ct);
                using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
                bool events=root.TryGetProperty("events",out var ev) && ev.GetArrayLength()>0;
                bool pending=root.TryGetProperty("pending",out var pe) && pe.GetArrayLength()>0;
                bool closed=root.TryGetProperty("closed",out var cl) && cl.GetBoolean();
                bool terminal=root.TryGetProperty("state",out var view) && view.ValueKind==JsonValueKind.Object
                    && view.TryGetProperty("gameOver",out var over) && over.GetBoolean();
                // Events may have been drained before a failed snapshot/action.
                // Recovery always fetches the current board and resumes auto-pass.
                if(failures>0 || events || pending || closed || terminal || state.StackPacer.Pending || state.LastPublishedButtonId==0)
                    await PushStateAndActions(stream,backend,token,matchId,version,state,ct,json);
                if(failures>0)Console.WriteLine("Battle updates recovered after a temporary backend error.");
                failures=0;
                if(state.CompletionSent || closed)break;
                if(state.PassedButton!=0 && Environment.TickCount64-state.PassedAtMs>=MatchSessionState.PassedHoldMs) {
                    // The pass never advanced Forge: offer the same decision again.
                    if(state.ButtonEventId==state.PassedButton)state.LastPublishedButtonId=0;
                    state.PassedButton=0;
                }
                await EnforceDecisionTimer(stream,backend,token,matchId,version,state,ct);
            } catch(OperationCanceledException) when(ct.IsCancellationRequested) {break;}
              catch(Exception ex) when(ex is HttpRequestException http && (http.StatusCode is null || (int)http.StatusCode>=500 || (int)http.StatusCode is 408 or 429) || ex is TaskCanceledException) {
                failures++;
                state.LastSnapshotJson=null;
                state.LastPublishedButtonId=0;
                Console.WriteLine($"Temporary battle backend error; retry {failures}: {ex.Message}");
                if(failures>=20) {stream.Dispose();break;}
            } catch(Exception ex) {
                Console.WriteLine($"Battle poll failed: {ex.GetType().Name}: {ex.Message}");
                stream.Dispose();break;
            }
        }
    }

    // Shows the rope at the warning point and answers the decision when it
    // runs out: the safe default, or a concession on the losing timeout.
    private static async Task EnforceDecisionTimer(Stream stream,string backend,string? token,string matchId,byte version,MatchSessionState state,CancellationToken ct)
    {
        if(state.Timing is not { } timing)return;
        long now=Environment.TickCount64;
        if(!state.Decision.WarningDue(timing,now) && !state.Decision.ExpiryDue(timing,now))return;
        bool concede=false;
        await state.PushGate.WaitAsync(ct);
        try {
            // Only the decision the client is actually showing can time out.
            if(state.CompletionSent || state.LastSnapshotJson==null || state.LastPublishedButtonId!=state.ButtonEventId
                || !state.Decision.IsCurrent(state.ButtonEventId,state.InputType))return;
            now=Environment.TickCount64;
            if(!state.Decision.ExpiryDue(timing,now)) {
                if(state.Decision.WarningDue(timing,now))
                    await WriteGre(stream,version,state,BattleTimers.CreateTimerStateMessage(state.NextMsgId++,state.CurrentGameStateId,1,
                        new[] {state.Decision.MarkWarned(timing,now)}),ct);
                return;
            }
            using var snapshot=JsonDocument.Parse(state.LastSnapshotJson);
            if(BattleTimers.TimeoutAnswer(snapshot.RootElement) is not { } answer) { state.Decision.Exempt(); return; }
            uint used=state.Decision.TimeoutsUsed+1;
            concede=used>=timing.TimeoutsToLose;
            try {
                if(concede)await SendEngineAction(backend,token,"{\"type\":\"concede\"}",ct);
                else if(answer.EmptyChoice)await SendEngineAnswer(backend,token,state.ButtonEventId,Array.Empty<uint>(),ct);
                else {
                    if(answer.Pass)state.Flow.ManualPass(snapshot.RootElement);
                    await SendEngineAction(backend,token,JsonSerializer.Serialize(new {type=answer.Action,eventId=state.ButtonEventId}),ct);
                }
            } catch(HttpRequestException ex) when(ex.StatusCode==System.Net.HttpStatusCode.BadRequest) {
                // For example a required attack: leave the decision with the player.
                Console.WriteLine("Forge rejected the timeout default; this decision is no longer timed.");
                state.Decision.Exempt();
                return;
            }
            used=state.Decision.RecordTimeout();
            Console.WriteLine(concede?$"Decision timer expired ({used}/{timing.TimeoutsToLose}); conceding the game."
                :$"Decision timer expired ({used}/{timing.TimeoutsToLose}); took the default answer.");
            await WriteGre(stream,version,state,BattleTimers.CreateTimeoutMessage(state.NextMsgId++,state.CurrentGameStateId,1,
                BattleTimers.SpareTimeouts(timing,used),DecisionTimer.Expired(timing)),ct);
            state.LastSnapshotJson=null; state.LastPublishedButtonId=0;
        } finally { state.PushGate.Release(); }
        // Concede runs on Forge's EDT, as with the client's own concession.
        await Task.Delay(concede?50:30,ct);
        await PushStateAndActions(stream,backend,token,matchId,version,state,ct);
    }

    private static async Task WriteGre(Stream stream,byte version,MatchSessionState state,GREToClientMessage message,CancellationToken ct)
    {
        var reply=new MatchServiceToClientMessage {GreToClientEvent=new GreToClientEvent {GreToClientMessages={message}}};
        state.Trace?.RecordProtobuf("server.gre",reply);
        await WriteFrame(stream,new Frame(version,1,2,reply.ToByteArray()),ct);
    }

    private static async Task<string> FetchSnapshot(string backend, string? token, CancellationToken ct, long afterPlaybackId = 0)
    {
        var http = BattleBackendHttp;
        using var req = new HttpRequestMessage(HttpMethod.Post, backend.TrimEnd('/') + "/api/interactive/snapshot");
        req.Content = new StringContent(JsonSerializer.Serialize(new { afterPlaybackId }),Encoding.UTF8,"application/json");
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        if(resp.StatusCode==System.Net.HttpStatusCode.BadRequest) {
            // Karn's restart rebuilds zones, permissions and opening-hand
            // decisions. A rejected snapshot must not kill a healthy match.
            Console.WriteLine("Forge snapshot temporarily unavailable: "+await resp.Content.ReadAsStringAsync(ct));
            throw new HttpRequestException("Rules snapshot is transitioning",null,System.Net.HttpStatusCode.ServiceUnavailable);
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static async Task<string> FetchPoll(string backend, string? token, CancellationToken ct)
    {
        var http = BattleBackendHttp;
        using var req = new HttpRequestMessage(HttpMethod.Get, backend.TrimEnd('/') + "/api/interactive/poll");
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        if(resp.StatusCode==System.Net.HttpStatusCode.BadRequest) {
            Console.WriteLine("Forge poll temporarily unavailable: "+await resp.Content.ReadAsStringAsync(ct));
            throw new HttpRequestException("Rules state is transitioning",null,System.Net.HttpStatusCode.ServiceUnavailable);
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static async Task SendEngineAction(MatchSessionState state, string backend, string? token, string json, CancellationToken ct, bool? passIntent=null)
    {
        await state.PushGate.WaitAsync(ct);
        try {
            if(passIntent.HasValue)state.Flow.PassAfterAction=passIntent.Value;
            await SendEngineAction(backend,token,json,ct);
            state.LastSnapshotJson=null;
        } finally { state.PushGate.Release(); }
    }
    private static async Task SendEngineAction(string backend, string? token, string json, CancellationToken ct)
    {
        var http = BattleBackendHttp;
        using var req = new HttpRequestMessage(HttpMethod.Post, backend.TrimEnd('/') + "/api/interactive/action");
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        if(resp.StatusCode==System.Net.HttpStatusCode.BadRequest) {
            string detail=await resp.Content.ReadAsStringAsync(ct);
            if(detail.Contains("Stale human action",StringComparison.OrdinalIgnoreCase) || detail.Contains("Previous human action still running",StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine("Forge input advanced before an action arrived; refreshing without disconnecting.");
                return;
            }
        }
        resp.EnsureSuccessStatusCode();
    }

    private static async Task SendEngineAnswer(string backend, string? token, long eventId, object answer, CancellationToken ct)
    {
        var http = BattleBackendHttp;
        using var req = new HttpRequestMessage(HttpMethod.Post, backend.TrimEnd('/') + "/api/interactive/answer");
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(JsonSerializer.Serialize(new { eventId, answer }), Encoding.UTF8, "application/json");
        using var resp = await http.SendAsync(req, ct);
        if(resp.StatusCode==System.Net.HttpStatusCode.BadRequest) {
            string detail=await resp.Content.ReadAsStringAsync(ct);
            if(detail.Contains("Unknown or stale prompt",StringComparison.OrdinalIgnoreCase)
                || detail.Contains("Prompt already answered",StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine("Forge decision already advanced; refreshing without disconnecting.");
                return;
            }
        }
        resp.EnsureSuccessStatusCode();
    }
}
