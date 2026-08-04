using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class PopupEventScheduleTimer(
    PopupEventState state,
    ZoneCenterBroadcastIngestor centerBroadcast)
{
    private static readonly int[] YanggokOpenHours = [0, 14, 18];
    private static readonly int[] YanggokCloseHours = [2, 16, 20];
    private static readonly int[] MonsterOpenHours = [1, 11];
    private static readonly int[] MonsterCloseHours = [3, 13];
    private static readonly int[] InvasionOpenHours = [12, 21];
    private static readonly int[] InvasionCloseHours = [14, 23];

    private static readonly (int Minute, int Remaining)[] CountdownSchedule = [(49, 10), (54, 5), (58, 1)];

    public void Tick(DateTime localNow)
    {
        foreach (var occurrence in GetDueOccurrences(localNow))
            if (occurrence.Kind == PopupEventScheduleOccurrenceKind.Close || localNow.Second == 0)
                Publish(occurrence);
    }

    public IEnumerable<PopupEventScheduleOccurrence> GetDueOccurrences(DateTime localNow)
    {
        var scheduledMinute = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour,
            localNow.Minute, 0, localNow.Kind);

        if (TryGetDueOccurrence(PopupEventType.YanggokPvp, YanggokOpenHours, YanggokCloseHours, 10, 0,
                scheduledMinute, out var yanggokOccurrence))
            yield return yanggokOccurrence;

        if (TryGetDueOccurrence(PopupEventType.MonsterPve, MonsterOpenHours, MonsterCloseHours, 0, 400,
                scheduledMinute, out var monsterOccurrence))
            yield return monsterOccurrence;

        if (TryGetDueOccurrence(PopupEventType.InvasionPvp, InvasionOpenHours, InvasionCloseHours, 5, 0,
                scheduledMinute, out var invasionOccurrence))
            yield return invasionOccurrence;
    }

    public void Publish(PopupEventScheduleOccurrence occurrence)
    {
        switch (occurrence.Kind)
        {
            case PopupEventScheduleOccurrenceKind.Countdown:
                SendCountdown(occurrence.Type, occurrence.RemainingMinutes);
                return;
            case PopupEventScheduleOccurrenceKind.Open:
                SendCountdown(occurrence.Type, 0);
                SendState(occurrence.Type, true, occurrence.PvpRequirement, occurrence.PvmRequirement);
                return;
            case PopupEventScheduleOccurrenceKind.Close:
                SendClose(occurrence.Type);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(occurrence), occurrence.Kind,
                    "Unsupported popup-event schedule occurrence kind.");
        }
    }

    private bool TryGetDueOccurrence(PopupEventType type, int[] openHours, int[] closeHours, int pvpRequirement,
        int pvmRequirement, DateTime scheduledMinute, out PopupEventScheduleOccurrence occurrence)
    {
        if (state.IsEnabled(type))
        {
            if (Array.IndexOf(closeHours, scheduledMinute.Hour) >= 0)
            {
                occurrence = new PopupEventScheduleOccurrence(type, PopupEventScheduleOccurrenceKind.Close,
                    scheduledMinute, 0, 0, 0);
                return true;
            }

            occurrence = default;
            return false;
        }

        if (Array.IndexOf(openHours, scheduledMinute.Hour) < 0)
        {
            occurrence = default;
            return false;
        }

        foreach (var (countdownMinute, remaining) in CountdownSchedule)
            if (scheduledMinute.Minute == countdownMinute)
            {
                occurrence = new PopupEventScheduleOccurrence(type, PopupEventScheduleOccurrenceKind.Countdown,
                    scheduledMinute, remaining, 0, 0);
                return true;
            }

        if (scheduledMinute.Minute == 59)
        {
            occurrence = new PopupEventScheduleOccurrence(type, PopupEventScheduleOccurrenceKind.Open,
                scheduledMinute, 0, pvpRequirement, pvmRequirement);
            return true;
        }

        occurrence = default;
        return false;
    }

    private void SendCountdown(PopupEventType type, int remainingMinutes)
    {
        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, (int)type);
        BinaryPrimitives.WriteInt32LittleEndian(payload[sizeof(int)..], remainingMinutes);
        centerBroadcast.Ingest(1512, payload);
    }

    private void SendClose(PopupEventType type)
    {
        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, (int)type);
        centerBroadcast.Ingest(1513, payload);
        SendState(type, false, 0, 0);
    }

    private void SendState(PopupEventType type, bool enabled, int pvpRequirement, int pvmRequirement)
    {
        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, enabled ? 1 : 0);
        BinaryPrimitives.WriteInt32LittleEndian(payload[sizeof(int)..], (int)type);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(sizeof(int) * 2)..], pvpRequirement);
        BinaryPrimitives.WriteInt32LittleEndian(payload[(sizeof(int) * 3)..], pvmRequirement);
        centerBroadcast.Ingest(1511, payload);
    }
}

public enum PopupEventScheduleOccurrenceKind : byte
{
    Countdown = 0,
    Open = 1,
    Close = 2
}

public readonly record struct PopupEventScheduleOccurrence(
    PopupEventType Type,
    PopupEventScheduleOccurrenceKind Kind,
    DateTime ScheduledAtLocal,
    int RemainingMinutes,
    int PvpRequirement,
    int PvmRequirement);
