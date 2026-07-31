using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public enum ZoneWarKind : byte
{
    Zone049 = 0,

    Zone051 = 1,

    Zone194 = 2,

    Zone241 = 3,

    Zone267 = 4,

    Zone297 = 5,

    Zone335 = 6
}

public sealed class ZoneWarTickService(ZoneRegistry zones, ILogger<ZoneWarTickService> logger) : BackgroundService
{
    public const int TribeCount = WorldStateService.TribeCount;

    private const int BroadcastCadenceLegacyTicks = 10;

    private const int LegacyTicksPerRemainTimeUnit = 2;

    private readonly SimulationTickAccumulator _accumulator = new();
    private readonly Lock _lock = new();
    private readonly int[] _monsterCounts = new int[TribeCount];
    private readonly int[] _standings = new int[TribeCount];

    private ZoneWarKind? _activeKind;
    private int _extraValue1;
    private int _extraValue2;
    private int _legacyTicksSinceStart;
    private int _remainTime;

    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                return _activeKind is not null;
            }
        }
    }

    public ZoneWarKind? ActiveKind
    {
        get
        {
            lock (_lock)
            {
                return _activeKind;
            }
        }
    }

    public int RemainTime
    {
        get
        {
            lock (_lock)
            {
                return _remainTime;
            }
        }
    }

    public void StartWar(ZoneWarKind kind, int remainTimeSeconds)
    {
        lock (_lock)
        {
            _activeKind = kind;
            _remainTime = remainTimeSeconds;
            _legacyTicksSinceStart = 0;
            Array.Clear(_standings);
            Array.Clear(_monsterCounts);
            _extraValue1 = 0;
            _extraValue2 = 0;
        }
    }

    public void EndWar()
    {
        lock (_lock)
        {
            _activeKind = null;
        }
    }

    public void ReportStanding(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _standings[tribeId] = value;
        }
    }

    public void ReportMonsterCount(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _monsterCounts[tribeId] = value;
        }
    }

    public void ReportExtraStatus(int value1, int value2)
    {
        lock (_lock)
        {
            _extraValue1 = value1;
            _extraValue2 = value2;
        }
    }

    public void Tick(TimeSpan elapsed)
    {
        var wholeTicks = _accumulator.Advance(elapsed);
        for (var i = 0; i < wholeTicks; i++)
            AdvanceOneLegacyTick();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    Tick(SimulationClock.LegacyTick);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "ZoneWar tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void AdvanceOneLegacyTick()
    {
        ZoneWarKind kind;
        int remainTime;
        int[] standings;
        int[] monsterCounts;
        int extraValue1;
        int extraValue2;

        lock (_lock)
        {
            if (_activeKind is not { } activeKind)
                return;

            kind = activeKind;
            _legacyTicksSinceStart++;

            if (_legacyTicksSinceStart % LegacyTicksPerRemainTimeUnit == 0 && _remainTime > 0)
                _remainTime--;

            var ended = _remainTime <= 0;
            var dueForBroadcast = _legacyTicksSinceStart == 1 ||
                                  _legacyTicksSinceStart % BroadcastCadenceLegacyTicks == 0 || ended;

            if (!dueForBroadcast)
                return;

            remainTime = _remainTime;
            standings = (int[])_standings.Clone();
            monsterCounts = (int[])_monsterCounts.Clone();
            extraValue1 = _extraValue1;
            extraValue2 = _extraValue2;

            if (ended)
                _activeKind = null;
        }

        if (kind == ZoneWarKind.Zone049)
            standings = ComputeLiveTribeHeadcount();

        Broadcast(kind, remainTime, standings, monsterCounts, extraValue1, extraValue2);
    }

    private int[] ComputeLiveTribeHeadcount()
    {
        var counts = new int[TribeCount];
        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            if (player.Tribe < TribeCount)
                counts[player.Tribe]++;

        return counts;
    }

    private void Broadcast(ZoneWarKind kind, int remainTime, int[] standings, int[] monsterCounts, int extraValue1,
        int extraValue2)
    {
        switch (kind)
        {
            case ZoneWarKind.Zone049:
                BroadcastToAll(new ZoneWar049StatusResponse { TribeUserNum = standings, RemainTime = remainTime });
                break;
            case ZoneWarKind.Zone051:
                BroadcastToAll(new ZoneWar051StatusResponse { ExistStone = standings, RemainTime = remainTime });
                break;
            case ZoneWarKind.Zone194:
                BroadcastToAll(new ZoneWar194CountdownResponse { RemainTime = remainTime });
                BroadcastToAll(new ZoneWar194StatusResponse { BattleInfo = standings, RemainTime = remainTime });
                break;
            case ZoneWarKind.Zone241:
                BroadcastToAll(new ZoneWar241StatusResponse { RemainTime = remainTime });
                break;
            case ZoneWarKind.Zone267:
                BroadcastToAll(new ZoneWar267StatusResponse { BattleInfo = standings, RemainTime = remainTime });
                break;
            case ZoneWarKind.Zone297:
                BroadcastToAll(new ZoneWar297StatusResponse
                    { Value00 = remainTime, Value01 = extraValue1, Value02 = extraValue2 });
                BroadcastToAll(new ZoneWar297MonsterCountResponse { MonsterNum = monsterCounts });
                break;
            case ZoneWarKind.Zone335:
                BroadcastToAll(new ZoneWarFfaBattleInfoResponse { RemainTime = remainTime });
                break;
        }
    }

    private void BroadcastToAll<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            player.Session.Send(packet);
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}
