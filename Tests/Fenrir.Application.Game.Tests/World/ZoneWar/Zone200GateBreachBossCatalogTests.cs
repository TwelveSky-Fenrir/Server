using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Boss-756 (Zone200 gate-breach/kill-race) summon and mechanic data -- see
///     <see cref="Zone200GateBreachBossCatalog" />'s own remarks: the kill-drop side effect is already fully
///     implemented elsewhere (<see cref="BossDropCatalog" />/<see cref="BossEventDropResolver" />), verified
///     directly against this catalog's own citation range in <see cref="KillDrop_AlreadyImplemented_MatchesContractOrder" />
///     below rather than duplicated. Everything else here is data-only, not yet wired into any state machine.
/// </summary>
public class Zone200GateBreachBossCatalogTests
{
    [Fact]
    public void EligibleServerNumbers_MatchesFourConfiguredServers()
    {
        Assert.Equal([200, 297, 298, 299], Zone200GateBreachBossCatalog.EligibleServerNumbers);
    }

    [Fact]
    public void BossMonsterId_IsSevenHundredFiftySix()
    {
        Assert.Equal(756, Zone200GateBreachBossCatalog.BossMonsterId);
    }

    [Fact]
    public void KillQuotaPerTribe_IsOneHundredSeventy()
    {
        Assert.Equal(170, Zone200GateBreachBossCatalog.KillQuotaPerTribe);
    }

    [Fact]
    public void SummonPosition_MatchesFixedCoordinates()
    {
        Assert.Equal(16f, Zone200GateBreachBossCatalog.SummonX);
        Assert.Equal(264f, Zone200GateBreachBossCatalog.SummonY);
        Assert.Equal(7650f, Zone200GateBreachBossCatalog.SummonZ);
    }

    [Fact]
    public void ExistenceCheckActive_IsTrue_UnlikeBoss561()
    {
        Assert.True(Zone200GateBreachBossCatalog.ExistenceCheckActive);
    }

    [Fact]
    public void BattleWinBonusFixedItemIds_HasSevenOfEightKnownItems_EighthSlotDeliberatelyOmitted()
    {
        // The 8th slot (a server-computed random "animal" item) is never invented -- see the field's own
        // remarks. Only the 7 concretely-cited ids are present, in the source's own order.
        Assert.Equal([1072, 1103, 1449, 1422, 1145, 2249, 602], Zone200GateBreachBossCatalog.BattleWinBonusFixedItemIds);
    }

    [Fact]
    public void KillDrop_AlreadyImplemented_MatchesContractOrder()
    {
        // Regression cross-check: the A4-missing-bosses contract's Boss-756 kill-drop (item 1073, then 1447,
        // then 723) is already fully implemented by the C4 boss-drop workstream -- this test confirms that
        // remains true rather than re-implementing it in this catalog.
        var worldData = WorldDataCacheBuilder.Build(WorldDataTestRows.MinimalRows()).Cache;

        var outcome = BossEventDropResolver.Resolve(
            Zone200GateBreachBossCatalog.BossMonsterId, 0, new Random(1), worldData,
            BossDropCatalog.Default);

        Assert.Equal(3, outcome.Items.Count);
        Assert.Equal(1073, outcome.Items[0].ItemId);
        Assert.Equal(1447, outcome.Items[1].ItemId);
        Assert.Equal(723, outcome.Items[2].ItemId);
    }
}
