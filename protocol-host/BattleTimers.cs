using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleTimers
{
    public const uint DecisionTimerId = 1;
    public const string SettingsFile = "match-settings.json";

    public static TimerInfo CreateTimerInfo(
        uint timerId,
        TimerType type,
        uint durationSec,
        uint elapsedSec,
        bool running,
        TimerBehavior behavior = TimerBehavior.Timeout,
        uint warningThresholdSec = 10,
        uint elapsedMs = 0)
    {
        return new TimerInfo {
            TimerId = timerId,
            Type = type,
            DurationSec = durationSec,
            ElapsedSec = elapsedSec,
            Running = running,
            Behavior = behavior,
            WarningThresholdSec = warningThresholdSec,
            ElapsedMs = elapsedMs != 0 ? elapsedMs : elapsedSec * 1000
        };
    }

    public static GREToClientMessage CreateTimerStateMessage(
        uint msgId,
        uint stateId,
        uint seatId,
        IEnumerable<TimerInfo> timers)
    {
        var message = new GREToClientMessage {
            Type = GREMessageType.TimerStateMessage,
            MsgId = msgId,
            GameStateId = stateId,
            SystemSeatIds = { seatId },
            TimerStateMessage = new TimerStateMessage {
                SeatId = seatId
            }
        };
        message.TimerStateMessage.Timers.AddRange(timers);
        return message;
    }

    public static GREToClientMessage CreateTimeoutMessage(
        uint msgId,
        uint stateId,
        uint seatId,
        uint timeoutCount,
        TimerInfo timer)
    {
        return new GREToClientMessage {
            Type = GREMessageType.TimeoutMessage,
            MsgId = msgId,
            GameStateId = stateId,
            SystemSeatIds = { seatId },
            TimeoutMessage = new TimeoutMessage {
                SeatId = seatId,
                TimeoutCount = timeoutCount,
                Timer = timer
            }
        };
    }

    // Read at each match connection, so edits apply to the next match.
    // A missing, disabled or invalid file leaves the match untimed.
    public static DecisionTimerSettings? LoadSettings(string? path = null)
    {
        path ??= Path.Combine(Environment.CurrentDirectory, SettingsFile);
        if(!File.Exists(path))return null;
        try {
            using var doc=JsonDocument.Parse(File.ReadAllText(path));
            if(!doc.RootElement.TryGetProperty("decisionTimer",out var timer) || timer.ValueKind!=JsonValueKind.Object)return null;
            if(!timer.TryGetProperty("enabled",out var enabled) || !enabled.GetBoolean())return null;
            uint seconds=timer.TryGetProperty("seconds",out var s)?s.GetUInt32():30;
            uint warning=timer.TryGetProperty("warningSeconds",out var w)?w.GetUInt32():10;
            uint limit=timer.TryGetProperty("timeoutsToLose",out var l)?l.GetUInt32():3;
            // The installed rope shows at 30 seconds remaining at the latest,
            // so a longer warning cannot be displayed.
            if(seconds is < 10 or > 600 || warning < 1 || warning > Math.Min(30,seconds) || limit is < 1 or > 10) {
                Console.WriteLine($"Ignored invalid decision timer in {SettingsFile}; this match is untimed.");
                return null;
            }
            return new DecisionTimerSettings(seconds,warning,limit);
        } catch(Exception ex) when(ex is JsonException or IOException or InvalidOperationException or FormatException or UnauthorizedAccessException) {
            Console.WriteLine($"Could not read {SettingsFile} ({ex.GetType().Name}); this match is untimed.");
            return null;
        }
    }

    // The answer Arena-style timing gives when the rope runs out. Only
    // decisions with a rules-neutral default are timed; targets, payments,
    // numbers and mandatory choices return null and stay untimed.
    public static TimeoutResponse? TimeoutAnswer(JsonElement snapshot)
    {
        if(!snapshot.TryGetProperty("input",out var input))return null;
        switch(input.GetProperty("type").GetString()) {
            case "InputPassPriority":
                return new TimeoutResponse("ok",false,true);
            case "InputAttack":
            case "InputBlock":
                // Submits whatever was already declared, possibly nothing.
                return new TimeoutResponse("ok",false,false);
            case "InputConfirmMulligan":
                return new TimeoutResponse("ok",false,false); // Keep the hand.
            case "InputConfirm":
                // Mirrors the order BattleRequests uses to pick the prompt.
                if(input.GetProperty("startingPlayer").GetBoolean())return new TimeoutResponse("ok",false,false); // Play first.
                if(input.TryGetProperty("optionalDraw",out var draw) && draw.GetBoolean())return new TimeoutResponse("cancel",false,false);
                if(input.TryGetProperty("optionalSacrificePrompt",out _))return null;
                if(input.TryGetProperty("optionalTrigger",out var trigger) && trigger.GetBoolean())return new TimeoutResponse("cancel",false,false);
                if(input.TryGetProperty("freeCastCardId",out _))return new TimeoutResponse("cancel",false,false);
                return null;
            case "InputGuiChoice": {
                int min=input.TryGetProperty("min",out var m)?m.GetInt32():input.TryGetProperty("modeMin",out var mm)?mm.GetInt32():1;
                return min==0?new TimeoutResponse(null,true,false):null;
            }
            default:
                return null;
        }
    }

    // Timeout pips are the spare timeouts before the losing one, so the
    // client's own "no timeouts left" expiry lines up with the loss.
    public static uint SpareTimeouts(DecisionTimerSettings timing,uint used) => used+1>=timing.TimeoutsToLose?0:timing.TimeoutsToLose-used-1;

    public static void Apply(GameStateMessage board,uint seatId,DecisionTimer timer,DecisionTimerSettings timing,long nowMs)
    {
        var player=board.Players.FirstOrDefault(p=>p.SystemSeatNumber==seatId);
        if(player==null)return;
        player.TimeoutCount=SpareTimeouts(timing,timer.TimeoutsUsed);
        // Each board rebuilds the player's timers from TimerIds, so a visible
        // rope must travel with every board published during the warning.
        if(timer.Visible(timing,nowMs) is { } info) {
            board.Timers.Add(info);
            player.TimerIds.Add(info.TimerId);
        }
    }
}

sealed record DecisionTimerSettings(uint Seconds,uint WarningSeconds,uint TimeoutsToLose);

sealed record TimeoutResponse(string? Action,bool EmptyChoice,bool Pass);

// Tracks the one human decision currently published to the client.
sealed class DecisionTimer
{
    readonly object gate=new();
    long button;
    string input="";
    long startedMs;
    bool warned;
    long exempt;
    public uint TimeoutsUsed { get { lock(gate)return timeoutsUsed; } }
    uint timeoutsUsed;

    public void Track(long button,string input,bool timed,long nowMs)
    {
        lock(gate) {
            if(!timed || button==exempt) { this.button=0; warned=false; return; }
            // Republishing the same decision must not restart its clock.
            if(button==this.button && input==this.input)return;
            this.button=button; this.input=input; startedMs=nowMs; warned=false;
        }
    }
    public bool IsCurrent(long button,string input) { lock(gate)return this.button!=0 && this.button==button && this.input==input; }
    public bool WarningDue(DecisionTimerSettings timing,long nowMs)
    {
        lock(gate)return button!=0 && !warned && nowMs-startedMs>=(long)(timing.Seconds-timing.WarningSeconds)*1000;
    }
    public bool ExpiryDue(DecisionTimerSettings timing,long nowMs)
    {
        lock(gate)return button!=0 && nowMs-startedMs>=(long)timing.Seconds*1000;
    }
    public TimerInfo MarkWarned(DecisionTimerSettings timing,long nowMs)
    {
        lock(gate) { warned=true; return Info(timing,nowMs,true); }
    }
    public TimerInfo? Visible(DecisionTimerSettings timing,long nowMs)
    {
        lock(gate)return button!=0 && warned?Info(timing,nowMs,true):null;
    }
    public uint RecordTimeout()
    {
        lock(gate) { button=0; warned=false; return ++timeoutsUsed; }
    }
    // Forge refused the default answer; leave this decision untimed.
    public void Exempt()
    {
        lock(gate) { exempt=button; button=0; warned=false; }
    }
    public static TimerInfo Expired(DecisionTimerSettings timing) =>
        BattleTimers.CreateTimerInfo(BattleTimers.DecisionTimerId,TimerType.Decision,timing.Seconds,timing.Seconds,false,TimerBehavior.Timeout,timing.WarningSeconds);
    TimerInfo Info(DecisionTimerSettings timing,long nowMs,bool running)
    {
        long elapsed=Math.Clamp(nowMs-startedMs,0,(long)timing.Seconds*1000);
        return BattleTimers.CreateTimerInfo(BattleTimers.DecisionTimerId,TimerType.Decision,timing.Seconds,(uint)(elapsed/1000),running,
            TimerBehavior.Timeout,timing.WarningSeconds,(uint)elapsed);
    }
}
