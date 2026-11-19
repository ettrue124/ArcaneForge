using System.Collections.Concurrent;

public sealed class MatchContext
{
    public string Token { get; set; } = "";
    public string PersonaId { get; set; } = "";
    public string DeckId { get; set; } = "";
    public List<uint> DeckCards { get; set; } = new();
    public List<uint> SideboardCards { get; set; } = new();
    public List<uint> CommanderCards { get; set; } = new();
    // Shared by both players of a human-vs-human match (backend play room); empty against bots.
    public string RoomId { get; set; } = "";
}

static class ActiveMatches
{
    private static readonly ConcurrentDictionary<string, MatchContext> _matches = new();

    public static void Register(string matchId, string token, string deckId = "", List<uint>? deckCards = null, List<uint>? sbCards = null, List<uint>? commanderCards = null,string personaId="",string roomId="")
    {
        _matches[matchId] = new MatchContext {
            RoomId = roomId,
            Token = token,
            PersonaId = personaId,
            DeckId = deckId,
            DeckCards = deckCards ?? new(),
            SideboardCards = sbCards ?? new(),
            CommanderCards = commanderCards ?? new()
        };
    }

    public static MatchContext? Get(string matchId) => _matches.TryGetValue(matchId, out var ctx) ? ctx : null;
    public static string? GetToken(string matchId) => Get(matchId)?.Token;
}
