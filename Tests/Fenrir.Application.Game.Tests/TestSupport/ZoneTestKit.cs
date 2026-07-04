using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Movement;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>
///     Deterministic zone construction for tests: real <see cref="Zone" /> instances (no fakes of the unit
///     under test), real <see cref="ZoneClientSession" />s over in-memory pipes, and time driven exclusively
///     through <see cref="Zone.Tick" /> — no <c>RunAsync</c>, no timers, no sleeps.
/// </summary>
internal static class ZoneTestKit
{
    public static GameServerOptions Options()
    {
        return new GameServerOptions();
    }

    public static Zone CreateZone(short mapId, GameServerOptions? options = null,
        DirtyTracker<int>? dirtyTracker = null, IReadOnlyList<ISimulationSystem>? simulationSystems = null,
        WorldDataCache? worldData = null, IRandomSource? randomSource = null)
    {
        var opts = options ?? Options();
        return new Zone(mapId, opts, new MovementRules(Microsoft.Extensions.Options.Options.Create(opts)),
            dirtyTracker ?? new DirtyTracker<int>(), simulationSystems ?? [], NullLogger<Zone>.Instance,
            worldData ?? EmptyWorldData(), randomSource);
    }

    public static (ZoneClientSession Session, FakeDuplexPipe Pipe) CreateSession(long sessionId)
    {
        var pipe = new FakeDuplexPipe();
        return (new ZoneClientSession(sessionId, pipe), pipe);
    }

    public static PlayerEnterData EnterData(ZoneClientSession session, short mapId, string name = "Hero",
        float posX = 100f, float posY = 0f, float posZ = 100f, long flushSequence = 7, byte tribe = 1,
        short level = 42)
    {
        return new PlayerEnterData(
            session,
            name,
            tribe,
            0,
            2,
            3,
            level,
            mapId,
            posX,
            posY,
            posZ,
            1.5f,
            800,
            840,
            300,
            320,
            flushSequence);
    }

    /// <summary>Drains every byte the session has written so far (empty array when nothing is pending).</summary>
    public static byte[] DrainOutbound(FakeDuplexPipe pipe)
    {
        if (!pipe.SessionToPeer.TryRead(out var result))
            return [];

        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
        return bytes;
    }

    /// <summary>
    ///     An entirely empty (but structurally valid) <see cref="WorldDataCache" /> -- every catalog lookup misses,
    ///     matching an un-seeded/irrelevant world for tests that don't care about item/skill/level data.
    /// </summary>
    public static WorldDataCache EmptyWorldData(
        FrozenDictionary<int, ItemDefinition>? itemsById = null,
        FrozenDictionary<int, SkillDefinition>? skillsById = null,
        FrozenDictionary<short, LevelRowDto>? levelsByLevel = null)
    {
        return new WorldDataCache
        {
            ItemsById = itemsById ?? EmptyFrozen<int, ItemDefinition>(),
            SkillsById = skillsById ?? EmptyFrozen<int, SkillDefinition>(),
            MonstersById = EmptyFrozen<int, MonsterDefinition>(),
            NpcsById = EmptyFrozen<int, NpcDefinition>(),
            QuestsById = EmptyFrozen<int, QuestDefinition>(),
            LevelsByLevel = levelsByLevel ?? EmptyFrozen<short, LevelRowDto>(),
            ZonesByNumber = EmptyFrozen<short, ZoneDefinition>(),
            GemSocketsById = EmptyFrozen<int, GemSocketRowDto>(),
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = EmptyFrozen<int, ItemMallProductRowDto>(),
            RewardBundleItemsByBundleId = EmptyFrozen<int, ImmutableArray<RewardBundleItemRowDto>>(),
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    private static FrozenDictionary<TKey, TValue> EmptyFrozen<TKey, TValue>() where TKey : notnull
    {
        return new Dictionary<TKey, TValue>().ToFrozenDictionary();
    }
}

/// <summary>
///     A trivial, fully deterministic <see cref="IRandomSource" /> -- returns a fixed sequence (wrapping back to
///     the start once exhausted), reduced modulo the CALLER'S requested bound so a test can name the intended
///     semantic value (e.g. "50" for a %100 hit-chance roll) without worrying about a smaller bound elsewhere in
///     the same sequence (e.g. a %2 variance-direction roll) silently taking an out-of-range raw value.
/// </summary>
internal sealed class ScriptedRandomSource(params int[] sequence) : IRandomSource
{
    private int _index;

    public int NextInt32(int exclusiveUpperBound)
    {
        var value = sequence[_index % sequence.Length] % exclusiveUpperBound;
        _index++;
        return value;
    }
}
