using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Which legacy zone-siege wire family <see cref="ZoneWarTickService" /> is currently reporting on. Each
///     legacy variant is its own map/expansion era (Server/ts25zone/S07_MyGame01.cpp's <c>mZone049Type...</c>/
///     <c>mZone051Type...</c>/etc. state machines) and only one is ever live in a given deployment, so one
///     shared engine parametrized by this enum stands in for all 7.
/// </summary>
public enum ZoneWarKind : byte
{
    /// <summary>ZC 049_TYPE_BATTLE_INFO (op95) -- TribeUserNum[4] headcount + RemainTime.</summary>
    Zone049 = 0,

    /// <summary>ZC 051_TYPE_BATTLE_INFO (op96) -- ExistStone[4] + RemainTime.</summary>
    Zone051 = 1,

    /// <summary>
    ///     ZC 194_TYPE_BATTLE_INFO/COUNTDOWN (op100/101) -- BattleInfo[4] + RemainTime, plus its own lighter countdown
    ///     frame.
    /// </summary>
    Zone194 = 2,

    /// <summary>ZC 241_TYPE_BATTLE_INFO (op104) -- RemainTime only.</summary>
    Zone241 = 3,

    /// <summary>ZC 267_TYPE_BATTLE_INFO (op103) -- BattleInfo[4] + RemainTime.</summary>
    Zone267 = 4,

    /// <summary>ZC 297_TYPE_REMAIN_INFO/MONSTER_INFO (op116/118) -- a 3-value status frame plus MonsterNum[4].</summary>
    Zone297 = 5,

    /// <summary>ZC FFA_TYPE_BATTLE_INFO (op200) -- RemainTime only; the twin ZC 198 countdown is dead code.</summary>
    Zone335 = 6
}

/// <summary>
///     Periodic producer for the ZoneWar049/051/194/241/267/297/335 status wire family -- the legacy zone
///     tick's own `% 10 == 0` broadcast cadence (S07_MyGame01.cpp:5159-5177 et al: decrement, and every 10th
///     legacy tick tell the whole server), reproduced here as one engine shared across every variant instead
///     of duplicating that loop 7 times.
///     <para>
///         Nothing yet flips a zone into a siege (no boss/stone/monster-count gameplay exists in Fenrir), so
///         <see cref="ActiveKind" /> starts, and stays, null until something calls <see cref="StartWar" /> --
///         same "real API, not yet called by anything" posture as <see cref="TowerWarState" />.
///         The one exception is <see cref="ZoneWarKind.Zone049" />'s TribeUserNum, which this class always
///         computes live from every connected player's <see cref="PlayerRuntimeState.Tribe" /> rather than
///         waiting on a caller to report it, since that figure needs no gameplay system to be real.
///     </para>
/// </summary>
public sealed class ZoneWarTickService(ZoneRegistry zones) : BackgroundService
{
    /// <summary>4 tribes -- every per-tribe status array this wire family carries is this wide.</summary>
    public const int TribeCount = WorldStateService.TribeCount;

    /// <summary>Every 10 legacy ticks (5 s) -- S07_MyGame01.cpp:5169's own `% 10 == 0` gate.</summary>
    private const int BroadcastCadenceLegacyTicks = 10;

    /// <summary>RemainTime counts real seconds (2 legacy ticks each), not raw legacy ticks.</summary>
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

    /// <summary>Arms a fresh window -- every reported standing/monster-count/extra value resets to zero.</summary>
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

    /// <summary>Ends the window immediately, with no final broadcast (an admin abort, not a natural expiry).</summary>
    public void EndWar()
    {
        lock (_lock)
        {
            _activeKind = null;
        }
    }

    /// <summary>
    ///     Feeds Zone051/194/267's per-tribe standings array (ExistStone/BattleInfo) -- Zone049 ignores this, see
    ///     remarks.
    /// </summary>
    public void ReportStanding(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _standings[tribeId] = value;
        }
    }

    /// <summary>Feeds Zone297's MonsterNum[4].</summary>
    public void ReportMonsterCount(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _monsterCounts[tribeId] = value;
        }
    }

    /// <summary>Feeds Zone297's Value01/Value02 (Value00 is always the current RemainTime).</summary>
    public void ReportExtraStatus(int value1, int value2)
    {
        lock (_lock)
        {
            _extraValue1 = value1;
            _extraValue2 = value2;
        }
    }

    /// <summary>
    ///     Advances the window by <paramref name="elapsed" /> real time, broadcasting on tick 1 and every
    ///     <see cref="BroadcastCadenceLegacyTicks" />th legacy tick after -- pure and timer-free so tests can
    ///     drive it directly, same convention as <see cref="Zone.Tick" />.
    /// </summary>
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
                Tick(SimulationClock.LegacyTick);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
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
                BroadcastToAll(new ZoneWar335CountdownResponse { RemainTime = remainTime });
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
