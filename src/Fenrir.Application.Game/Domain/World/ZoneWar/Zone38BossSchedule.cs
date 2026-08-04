namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum Zone38BossSchedulePhase : byte
{
    WaitingForSchedule,

    MonitoringBoss
}

public readonly record struct Zone38BossSpawnRequest(
    int MonsterId,
    float X,
    float Y,
    float Z);

public readonly record struct Zone38BossInstance(
    int ServerIndex,
    uint UniqueNumber);

public interface IZone38BossScheduleEffects
{
    public bool TrySpawn(Zone zone, in Zone38BossSpawnRequest request, out Zone38BossInstance boss);

    public bool IsValid(Zone zone, in Zone38BossInstance boss);

    public void AnnounceSpawned(Zone zone, in Zone38BossInstance boss);

    public void AnnounceFinished(Zone zone, in Zone38BossInstance boss);
}

public sealed class Zone38BossSchedule
{
    public const short ZoneId = 38;

    public const int BossMonsterId = 1407;

    public static Zone38BossSpawnRequest SpawnRequest { get; } = new(BossMonsterId, 1383f, 0f, 3068f);

    private readonly HashSet<Zone38BossScheduleOccurrence> _consumedOccurrences = [];

    private Zone38BossInstance _activeBoss;

    public Zone38BossSchedulePhase Phase { get; private set; } = Zone38BossSchedulePhase.WaitingForSchedule;

    public void Tick(Zone zone, DateTimeOffset localNow, IZone38BossScheduleEffects effects)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(effects);

        if (zone.MapId != ZoneId)
            return;

        var occurrenceDue = TryConsumeOccurrence(localNow);

        if (Phase == Zone38BossSchedulePhase.MonitoringBoss)
        {
            if (effects.IsValid(zone, _activeBoss))
                return;

            var finishedBoss = _activeBoss;
            _activeBoss = default;
            Phase = Zone38BossSchedulePhase.WaitingForSchedule;
            effects.AnnounceFinished(zone, finishedBoss);
            return;
        }

        if (!occurrenceDue || !effects.TrySpawn(zone, SpawnRequest, out var spawnedBoss))
            return;

        _activeBoss = spawnedBoss;
        Phase = Zone38BossSchedulePhase.MonitoringBoss;
        effects.AnnounceSpawned(zone, spawnedBoss);
    }

    private bool TryConsumeOccurrence(DateTimeOffset localNow)
    {
        var minute = localNow.Hour == 22 ? localNow.Minute : -1;
        if (minute is not (0 or 30))
            return false;

        return _consumedOccurrences.Add(new Zone38BossScheduleOccurrence(DateOnly.FromDateTime(localNow.Date), minute));
    }

    private readonly record struct Zone38BossScheduleOccurrence(DateOnly Date, int Minute);
}
