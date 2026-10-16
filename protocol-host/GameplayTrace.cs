using System.Text.Json;
using Google.Protobuf;

sealed class GameplayTrace : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private long _sequence;
    private bool _disposed;

    public string DirectoryPath { get; }

    private GameplayTrace(string directoryPath, string matchId)
    {
        DirectoryPath = directoryPath;
        Directory.CreateDirectory(directoryPath);
        _writer = new StreamWriter(Path.Combine(directoryPath, "gameplay.jsonl"), append: false, encoding: new System.Text.UTF8Encoding(false))
        { AutoFlush = true };
        File.WriteAllText(Path.Combine(directoryPath, "session.json"), JsonSerializer.Serialize(new {
            startedUtc = DateTimeOffset.UtcNow,
            matchId,
            contents = new[] { "decoded MatchDoor GRE messages", "full Forge snapshots visible to the local engine", "exact decrypted gameplay frames in both directions, including UI messages and heartbeats" },
            note = "Local diagnostic data. Forge snapshots can include both players' hidden cards. Authentication requests, session tickets and bearer tokens are not recorded."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static GameplayTrace Start(string matchId)
    {
        string root = Path.Combine(Environment.CurrentDirectory, "analysis", "arena-traces");
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(root, $"local-{stamp}-{Guid.NewGuid().ToString("N")[..8]}");
        var trace=new GameplayTrace(path, matchId);
        File.WriteAllText(Path.Combine(root,"latest-session.txt"),path);
        return trace;
    }

    public void RecordFrame(string direction, Frame frame) => Record(direction+".wire",new {
        version=frame.Version,type=frame.Type,format=frame.Format,bytes=frame.Body.Length,
        frameBase64=Convert.ToBase64String(Frames.Encode(frame))
    });

    public void RecordProtobuf(string type, IMessage message)
    {
        using var document = JsonDocument.Parse(JsonFormatter.Default.Format(message));
        Record(type, document.RootElement);
    }

    public void Record(string type, object data)
    {
        lock (_gate)
        {
            if (_disposed) return;
            long sequence = ++_sequence;
            _writer.WriteLine(JsonSerializer.Serialize(new {
                sequence,
                timestampUtc = DateTimeOffset.UtcNow,
                type,
                data
            }));
            _counts[type] = _counts.GetValueOrDefault(type) + 1;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Dispose();
            File.WriteAllText(Path.Combine(DirectoryPath, "summary.json"), JsonSerializer.Serialize(new {
                endedUtc = DateTimeOffset.UtcNow,
                events = _sequence,
                eventCounts = _counts,
                files = new[] { "gameplay.jsonl", "session.json", "summary.json" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
