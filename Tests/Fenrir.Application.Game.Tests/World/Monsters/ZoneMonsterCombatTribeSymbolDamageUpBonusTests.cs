using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers the end-to-end wiring of the <c>tribesymbol-damage-magnitude</c> contract's flat damage-up bonus
///     through <c>Zone.Combat.cs</c>'s <c>ResolvePvmAttack</c> call site: <see cref="Zone" />'s own
///     <c>tribeSymbolCombatModifiers</c> collaborator, populated by a real
///     <see cref="TribeSymbolDamageModifierSystem" /> tick, actually reaches
///     <see cref="MonsterCombatResolver.ResolvePvmAttack" /> and increases the applied damage. Complements
///     <c>MonsterCombatResolverTribeSymbolDamageUpBonusTests</c> (the same term's pure resolver-level coverage)
///     and <c>ZoneMonsterCombatTribeSymbolMalusTests</c> (the companion malus term's own end-to-end coverage).
/// </summary>
public class ZoneMonsterCombatTribeSymbolDamageUpBonusTests
{
    private static readonly EffectiveStats StrongAttacker = new(1000, 1000, 1000, 0, 1000, 0, 0, 0, 0, 0, 0);

    private static WorldDataCache CacheWithOneRegion()
    {
        var monster = WorldDataTestRows.Monster(700) with
        {
            Life = 1_000_000,
            ItemLevel = 1,
            RealLevel = 1,
            GeneralExperience = 10,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            AttackBlock = 0,
            DefensePower = 0,
            ElementDefensePower = 0
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 700) with
        {
            Number = 1,
            LocationX = 100,
            LocationY = 0,
            LocationZ = 100,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static (Zone Zone, TribeSymbolCombatModifiers Modifiers, WorldStateService WorldState)
        CreateZoneWithSpawnedMonster(Action<WorldStateService> arrangeWorldState)
    {
        var cache = CacheWithOneRegion();
        var scheduler = new MonsterSpawnScheduler(cache);
        var modifiers = new TribeSymbolCombatModifiers();
        var worldState = ZoneTestKit.CreateWorldState();
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache,
            randomSource: new ScriptedRandomSource(0), tribeSymbolCombatModifiers: modifiers);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        // EnterData's own default tribe is 1 -- relied on here rather than overridden, so the bonus below
        // targets this exact attacker's own tribe.
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Attacker")));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var spawnedMonster));
        spawnedMonster!.AiState = MonsterAiState.Decision;

        Assert.True(zone.TryGetPlayer(10, out var attacker));
        attacker!.Stats = StrongAttacker;
        attacker.Level = 1; // strictly below MalusMinimumAttackerLevel -- isolates the bonus from the malus
        attacker.AttackSubPacketCeiling = int.MaxValue;

        arrangeWorldState(worldState);

        // Populate Zone's own tribeSymbolCombatModifiers exactly the way the real per-tick simulation system
        // would, using the SAME WorldStateService the bonus is derived from -- not a direct internal-setter
        // poke (TribeSymbolCombatModifiers.SetDamageUpBonusIncrementCount is internal to
        // Fenrir.Application.Game.Domain and deliberately not exercised directly from this test assembly,
        // matching TribeSymbolDamageUpBonusTests's own documented convention).
        new TribeSymbolDamageModifierSystem(worldState, modifiers).Simulate(zone, 1);

        zone.Tick(CombatResolver.ProtectDuration + TimeSpan.FromSeconds(1));

        return (zone, modifiers, worldState);
    }

    private static AttackForProtocol MeleeAgainstMonster(int attackerId, MonsterEntity monster)
    {
        return new AttackForProtocol
        {
            Case = 3,
            ServerIndex1 = attackerId,
            UniqueNumber1 = unchecked((uint)attackerId),
            ServerIndex2 = monster.ServerIndex,
            UniqueNumber2 = monster.UniqueNumber,
            SenderLocation = [100, 0, 100],
            AttackActionValue1 = 1,
            AttackActionValue2 = 0,
            AttackActionValue3 = 0,
            AttackActionValue4 = 0,
            AttackResultValue = 0,
            AttackCriticalExist = 0,
            AttackElementDamage = 0,
            AttackViewDamageValue = 0,
            AttackRealDamageValue = 0
        };
    }

    [Fact]
    public void AttackerTribeOwnsOneOtherSlot_DamageIsIncreasedByOneFlatIncrement()
    {
        var (zone, modifiers, _) = CreateZoneWithSpawnedMonster(worldState =>
            worldState.ResolveTribeSymbol(2, 1)); // tribe 1 (this attacker's own tribe) captures tribe 2's slot
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(1));
        var startingLife = monster!.Life;

        zone.PostCombatCommand(new CombatCommand
            { AttackerCharacterId = 10, AttackInfo = MeleeAgainstMonster(10, monster) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var damaged));
        // 1000 base attack power + (1 increment * 500) = 1500.
        Assert.Equal(startingLife - 1_500, damaged!.Life);
    }

    [Fact]
    public void AttackerTribeHasNoIncrements_DamageIsUnaffected()
    {
        var (zone, modifiers, _) = CreateZoneWithSpawnedMonster(_ => { });
        Assert.True(zone.TryGetMonster(1, out var monster));
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(1));
        var startingLife = monster!.Life;

        zone.PostCombatCommand(new CombatCommand
            { AttackerCharacterId = 10, AttackInfo = MeleeAgainstMonster(10, monster) });
        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(zone.TryGetMonster(1, out var damaged));
        Assert.Equal(startingLife - 1_000, damaged!.Life);
    }
}
