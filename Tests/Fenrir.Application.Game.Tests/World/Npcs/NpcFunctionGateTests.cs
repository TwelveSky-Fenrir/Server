using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using Fenrir.Application.Game.Commerce;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.World;
using Fenrir.Application.Game.World.Npcs;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.World.Npcs;

public class NpcFunctionGateTests
{
    private static NpcDefinition NpcOffering(int npcId, int functionId)
    {
        return new NpcDefinition(
            WorldDataTestRows.Npc(npcId),
            [new NpcMenuOptionRowDto(npcId, (short)functionId, 2)],
            [], [], [], []);
    }

    private static ZoneDefinition ZoneWith(params (int NpcId, float X, float Y, float Z)[] spawns)
    {
        var rows = spawns.Select((s, i) =>
                new ZoneNpcSpawnRowDto(1, (short)i, s.NpcId, s.X, s.Y, s.Z, 0f))
            .ToImmutableArray();
        return new ZoneDefinition(WorldDataTestRows.Zone(1), [], [], rows, []);
    }

    private static WorldDataCache WorldWithNpcs(params NpcDefinition[] npcs)
    {
        return new WorldDataCache
        {
            ItemsById = FrozenDictionary<int, ItemDefinition>.Empty,
            SkillsById = FrozenDictionary<int, SkillDefinition>.Empty,
            MonstersById = FrozenDictionary<int, MonsterDefinition>.Empty,
            NpcsById = npcs.ToFrozenDictionary(n => n.Npc.NpcId),
            QuestsById = FrozenDictionary<int, QuestDefinition>.Empty,
            LevelsByLevel = FrozenDictionary<short, LevelRowDto>.Empty,
            ZonesByNumber = FrozenDictionary<short, ZoneDefinition>.Empty,
            GemSocketsById = FrozenDictionary<int, GemSocketRowDto>.Empty,
            BloodExchangeCatalog = [],
            EventDefinitions = [],
            ItemMallProductsById = FrozenDictionary<int, ItemMallProductRowDto>.Empty,
            RewardBundleItemsByBundleId = FrozenDictionary<int, ImmutableArray<RewardBundleItemRowDto>>.Empty,
            CashCatalog = CashCatalogBuilder.Build([]),
            CashCatalogVersion = 0
        };
    }

    [Fact]
    public void NpcWithinRadius_OfferingFunction_IsAvailable()
    {
        var zone = ZoneWith((NpcId: 10, X: 0, Y: 0, Z: 0));
        var world = WorldWithNpcs(NpcOffering(10, NpcFunctionGate.NpcShop));

        var available = NpcFunctionGate.IsAvailable(zone, world, NpcFunctionGate.NpcShop, 50, 0, 0);

        Assert.True(available);
    }

    [Fact]
    public void NpcBeyondRadius_IsNotAvailable()
    {
        var zone = ZoneWith((NpcId: 10, X: 0, Y: 0, Z: 0));
        var world = WorldWithNpcs(NpcOffering(10, NpcFunctionGate.NpcShop));

        // 100.1 units away -- just past the sqrt(10000)=100 radius.
        var available = NpcFunctionGate.IsAvailable(zone, world, NpcFunctionGate.NpcShop, 100.1f, 0, 0);

        Assert.False(available);
    }

    [Fact]
    public void NpcExactlyAtRadius_IsNotAvailable_SquaredCompareIsStrictlyLessThan()
    {
        var zone = ZoneWith((NpcId: 10, X: 0, Y: 0, Z: 0));
        var world = WorldWithNpcs(NpcOffering(10, NpcFunctionGate.NpcShop));

        var available = NpcFunctionGate.IsAvailable(zone, world, NpcFunctionGate.NpcShop, 100f, 0, 0);

        Assert.False(available);
    }

    [Fact]
    public void NpcNotOfferingThisFunction_IsNotAvailable_EvenWhenAdjacent()
    {
        var zone = ZoneWith((NpcId: 10, X: 0, Y: 0, Z: 0));
        var world = WorldWithNpcs(NpcOffering(10, NpcFunctionGate.LearnSkillTree1));

        var available = NpcFunctionGate.IsAvailable(zone, world, NpcFunctionGate.NpcShop, 0, 0, 0);

        Assert.False(available);
    }

    [Fact]
    public void NoNpcsInZone_IsNotAvailable()
    {
        var zone = ZoneWith();
        var world = WorldWithNpcs();

        Assert.False(NpcFunctionGate.IsAvailable(zone, world, NpcFunctionGate.NpcShop, 0, 0, 0));
    }

    [Fact]
    public void SecondFartherNpc_StillMakesFunctionAvailable_WhenFirstIsOutOfRange()
    {
        var zone = ZoneWith((NpcId: 10, X: 500, Y: 0, Z: 500), (NpcId: 11, X: 0, Y: 0, Z: 0));
        var world = WorldWithNpcs(NpcOffering(10, NpcFunctionGate.NpcShop), NpcOffering(11, NpcFunctionGate.NpcShop));

        Assert.True(NpcFunctionGate.IsAvailable(zone, world, NpcFunctionGate.NpcShop, 0, 0, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void FunctionIdOutOfLegacyBounds_IsNeverAvailable(int functionId)
    {
        var zone = ZoneWith((NpcId: 10, X: 0, Y: 0, Z: 0));
        var world = WorldWithNpcs(NpcOffering(10, functionId));

        Assert.False(NpcFunctionGate.IsAvailable(zone, world, functionId, 0, 0, 0));
    }
}
