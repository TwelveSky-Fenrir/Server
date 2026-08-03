using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum TribeSymbolBattleSchedulePhase : byte
{
    WaitingForHour,

    Counting
}

public sealed class TribeSymbolBattleScheduler(
    WorldStateService worldState,
    ZoneEventBroadcaster broadcaster,
    ILogger<TribeSymbolBattleScheduler> logger,
    IReadOnlySet<DayOfWeek>? allowedDays = null,
    bool testMode = false)
{
    public const int OpenHour = 20;

    public const int CloseHour = 22;

    public const int CountdownMinutes = 10;

    private readonly MinuteCountdown _countdown = new();

    public IReadOnlySet<DayOfWeek> AllowedDays { get; } = allowedDays ?? new HashSet<DayOfWeek>();

    public TribeSymbolBattleSchedulePhase Phase { get; private set; } = TribeSymbolBattleSchedulePhase.WaitingForHour;

    public int MinutesRemainingInCountdown =>
        Phase != TribeSymbolBattleSchedulePhase.Counting ||
        _countdown.MinutesElapsed is 0 or > CountdownMinutes
            ? 0
            : CountdownMinutes + 1 - _countdown.MinutesElapsed;

    public void Tick(TimeSpan elapsed, DateTime localNow)
    {
        switch (Phase)
        {
            case TribeSymbolBattleSchedulePhase.WaitingForHour:
                EvaluateWaiting(localNow);
                break;
            case TribeSymbolBattleSchedulePhase.Counting:
                AdvanceCountdown(elapsed);
                break;
        }
    }

    private void EvaluateWaiting(DateTime localNow)
    {
        var closing = worldState.World.TribeSymbolBattle;
        var targetHour = closing ? CloseHour : OpenHour;

        if (localNow.Hour != targetHour)
            return;

        if (!testMode && !AllowedDays.Contains(localNow.DayOfWeek))
            return;

        Phase = TribeSymbolBattleSchedulePhase.Counting;
        _countdown.Reset();

        logger.LogInformation(
            "TribeSymbolBattle: qualifying hour {Hour} reached ({Direction}) -- countdown armed", targetHour,
            closing ? "closing" : "opening");
    }

    private void AdvanceCountdown(TimeSpan elapsed)
    {
        var wholeMinutes = _countdown.Advance(elapsed);
        for (var i = 0; i < wholeMinutes; i++)
            OnMinuteElapsed();
    }

    private void OnMinuteElapsed()
    {
        var closing = worldState.World.TribeSymbolBattle;
        var minute = _countdown.MinutesElapsed;

        if (minute is >= 1 and <= CountdownMinutes)
        {
            var minutesRemaining = CountdownMinutes + 1 - minute;
            logger.LogInformation("TribeSymbolBattle {Direction} in {MinutesRemaining} minute(s)",
                closing ? "closes" : "opens", minutesRemaining);
            broadcaster.AnnounceTribeSymbolBattleCountdown();
            return;
        }

        if (minute != CountdownMinutes + 1)
            return;

        if (closing)
            broadcaster.AnnounceTribeSymbolBattleEnded();
        else
            broadcaster.AnnounceTribeSymbolBattleStarted();

        Phase = TribeSymbolBattleSchedulePhase.WaitingForHour;
        _countdown.Reset();
    }
}
