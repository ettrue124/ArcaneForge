using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

// Arena's IntermissionWorkflow plays defeat VFX, then releases the scene lock.
// MatchCompleted alone leaves the client on the battlefield indefinitely.
static class BattleCompletion
{
    public static ResultSpec[] Results(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("stage", out var stage) || stage.GetInt32() != 3) return [];
        if (!snapshot.TryGetProperty("winnerSeat", out var winner)) return [];
        uint seat = winner.GetUInt32();
        bool draw = snapshot.TryGetProperty("draw", out var drawn) && drawn.GetBoolean();
        var seats=snapshot.TryGetProperty("players",out var players)?players.EnumerateArray().Select(p=>p.GetProperty("seat").GetUInt32()).ToArray():new uint[]{1,2};
        if (draw ? seat != 0 : !seats.Contains(seat)) throw new InvalidDataException("Invalid completed Forge outcome");
        var reason = snapshot.TryGetProperty("lossReason", out var loss) && loss.GetString() == "Conceded"
            ? ResultReason.Concede : ResultReason.Game;
        return new[] { MatchScope.Game, MatchScope.Match }.Select(scope => new ResultSpec {
            Scope = scope, Result = draw ? ResultType.Draw : ResultType.WinLoss,
            WinningTeamId = seat, Reason = reason
        }).ToArray();
    }

    public static GREToClientMessage Intermission(GameStateMessage board, uint msgId, uint viewer)
        => new() { Type = GREMessageType.IntermissionReq, MsgId = msgId, GameStateId = board.GameStateId,
            SystemSeatIds = { viewer }, IntermissionReq = new IntermissionReq {
                Result = board.GameInfo.Results.Single(r => r.Scope == MatchScope.Game).Clone()
            } };

    public static MatchServiceToClientMessage Notification(GameStateMessage board) => new() {
        MatchGameRoomStateChangedEvent = new MatchGameRoomStateChangedEvent {
            GameRoomInfo = new MatchGameRoomInfo {
                StateType = MatchGameRoomStateType.MatchCompleted,
                GameRoomConfig = new MatchGameRoomConfig { MatchId = board.GameInfo.MatchID },
                FinalMatchResult = new FinalMatchResult {
                    MatchId = board.GameInfo.MatchID, MatchCompletedReason = MatchCompletedReasonType.Success,
                    ResultList = { board.GameInfo.Results.Select(r => r.Clone()) }
                }
            }
        }
    };

    public static void LossAnnotation(JsonElement snapshot, GameStateMessage board, ref uint nextId)
    {
        var result = board.GameInfo.Results.FirstOrDefault(r => r.Scope == MatchScope.Game);
        if (result == null) return;
        // Draw intermissions also play avatar effects and require a valid
        // player context. The result remains Draw with no winning team.
        uint loser = snapshot.TryGetProperty("loserSeat",out var lost) ? lost.GetUInt32()
            : board.Players.FirstOrDefault(p=>p.Status==PlayerStatus.Removed)?.SystemSeatNumber
                ?? board.Players.First(p=>result.Result==ResultType.Draw || p.TeamId!=result.WinningTeamId).SystemSeatNumber;
        string reason = snapshot.TryGetProperty("lossReason", out var loss) ? loss.GetString() ?? "" : "";
        string nativeReason = reason switch {
            "LifeReachedZero" => "SBA_LifeTotal", "Milled" => "SBA_DrawFromEmptyLib",
            "Poisoned" => "SBA_Poison", "Conceded" => "SBA_Concede",
            "OpponentWon" or "SpellEffect" => "GameEffect", _ => "Other"
        };
        board.Annotations.Add(new AnnotationInfo { Id = nextId++, Type = { AnnotationType.LossOfGame },
            AffectedIds = { loser }, Details = { new KeyValuePairInfo {
                Key = "reason", Type = KeyValuePairValueType.String, ValueString = { nativeReason }
            } } });
    }
}
