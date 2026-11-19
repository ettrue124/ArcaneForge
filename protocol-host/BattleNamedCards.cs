using System.Text.Json;

// Native StaticCardNameTranslation returns TitleId, not a printing/instance ID.
static class BattleNamedCards
{
    internal static readonly Lazy<Dictionary<uint,string>> Names = new(() => {
        using var catalogue=JsonDocument.Parse(File.ReadAllText("data/catalogue.json"));
        var result=new Dictionary<uint,string>();
        foreach(var card in catalogue.RootElement.EnumerateArray()) {
            if(card.TryGetProperty("arenaMetadata",out var metadata)
                && metadata.TryGetProperty("TitleId",out var title) && title.TryGetUInt32(out var id) && id!=0
                && card.TryGetProperty("name",out var name) && name.GetString() is {} text)
                result.TryAdd(id,text);
        }
        return result;
    });
}
