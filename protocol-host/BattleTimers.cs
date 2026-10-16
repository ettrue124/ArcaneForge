using Wotc.Mtgo.Gre.External.Messaging;

static class BattleTimers
{
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
}
