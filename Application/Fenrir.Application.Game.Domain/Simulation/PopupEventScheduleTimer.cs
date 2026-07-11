using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PopupEventScheduleTimer(PopupEventState state, ILogger<PopupEventScheduleTimer> logger)
{
    private static readonly int[] YanggokOpenHours = [0, 14, 18];
    private static readonly int[] YanggokCloseHours = [2, 16, 20];
    private static readonly int[] MonsterOpenHours = [1, 11];
    private static readonly int[] MonsterCloseHours = [3, 13];
    private static readonly int[] InvasionOpenHours = [12, 21];
    private static readonly int[] InvasionCloseHours = [14, 23];

        private static readonly (int Minute, int Remaining)[] CountdownSchedule = [(49, 10), (54, 5), (58, 1)];

    private int _lastMinuteOfDay = -1;

        public void Tick(DateTime utcNow)
    {
        var minuteOfDay = utcNow.Hour * 60 + utcNow.Minute;
        if (minuteOfDay == _lastMinuteOfDay)
            return;

        _lastMinuteOfDay = minuteOfDay;

        var hour = utcNow.Hour;
        var minute = utcNow.Minute;

        ProcessCountdownWindow(PopupEventType.YanggokPvp, YanggokOpenHours, YanggokCloseHours, hour, minute);
        ProcessCountdownWindow(PopupEventType.MonsterPve, MonsterOpenHours, MonsterCloseHours, hour, minute);
        ProcessSimpleWindow(PopupEventType.InvasionPvp, InvasionOpenHours, InvasionCloseHours, hour, minute);
    }

    private void ProcessCountdownWindow(PopupEventType type, int[] openHours, int[] closeHours, int hour,
        int minute)
    {
        if (minute == 59 && Array.IndexOf(openHours, hour) >= 0)
        {
            state.SetEnabled(type, true);
            logger.LogInformation("Popup window opened: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
            return;
        }

        foreach (var (countdownMinute, remaining) in CountdownSchedule)
            if (minute == countdownMinute && Array.IndexOf(openHours, hour) >= 0)
            {
                logger.LogInformation(
                    "Popup window countdown: {Type} remaining={Remaining} ({Hour}:{Minute} UTC) -- client " +
                    "notice NOT sent: no wire sort code for this notice was given by the A12 contract, see " +
                    "openQuestions", type, remaining, hour, minute);
                return;
            }

        if (minute == 0 && Array.IndexOf(closeHours, hour) >= 0)
        {
            state.SetEnabled(type, false);
            logger.LogInformation("Popup window closed: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
        }
    }

    private void ProcessSimpleWindow(PopupEventType type, int[] openHours, int[] closeHours, int hour, int minute)
    {
        if (minute != 0)
            return;

        if (Array.IndexOf(openHours, hour) >= 0)
        {
            state.SetEnabled(type, true);
            logger.LogInformation("Popup window opened: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
        }
        else if (Array.IndexOf(closeHours, hour) >= 0)
        {
            state.SetEnabled(type, false);
            logger.LogInformation("Popup window closed: {Type} ({Hour}:{Minute} UTC)", type, hour, minute);
        }
    }
}
