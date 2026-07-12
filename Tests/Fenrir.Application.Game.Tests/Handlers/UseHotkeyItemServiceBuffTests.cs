using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class UseHotkeyItemServiceBuffTests
{
    private const int AssassinScrollItemId = 1364;
    private const int DepartedSpiritScrollItemId = 1156;
    private const int AttackIncreaseBookItemId = 1471;
    private const int DodgeIncreaseBookItemId = 1472;
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        UseHotkeyItemService Service) SetUp(int itemId, int potionType1, int quantity, short mapId = 1)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [itemId] = new(
                WorldDataTestRows.Item(itemId) with
                {
                    Sort = ConsumableSort, PotionType1 = (short)potionType1, PotionType2 = 0
                },
                [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);

        var zone = ZoneTestKit.CreateZone(mapId, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.SetHotkeySlot(0, 0, new HotkeySlot(HotkeyBindingKind.Item, itemId, quantity));

        var characters = new FakeCharacterRepository();
        var service = new UseHotkeyItemService(characters, worldData, NullLogger<UseHotkeyItemService>.Instance);
        return (zone, state, characters, service);
    }

    [Fact]
    public async Task AssassinScroll_WritesBuffSlot15_AndPersistsTheDecrementedSlot()
    {
        var (zone, state, characters, service) = SetUp(AssassinScrollItemId, 12, 2);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(3, state.Buffs.Buff[15 * 2]);
        Assert.Equal(40, state.Buffs.Buff[15 * 2 + 1]);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal(10, upsert.CharacterId);
        Assert.Equal(AssassinScrollItemId, upsert.Sort);
        Assert.Equal(1, upsert.Value1);
        Assert.Equal((int)HotkeyBindingKind.Item, upsert.Value2);
    }

    [Fact]
    public async Task DepartedSpiritScroll_WritesBuffSlot15_Duration60LegacyTicks()
    {
        var (zone, state, characters, service) = SetUp(DepartedSpiritScrollItemId, 13, 1);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(3, state.Buffs.Buff[15 * 2]);
        Assert.Equal(60, state.Buffs.Buff[15 * 2 + 1]);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal(0, upsert.Sort);
        Assert.Equal(0, upsert.Value1);
        Assert.Equal((int)HotkeyBindingKind.None, upsert.Value2);
    }

    [Fact]
    public async Task AttackIncreaseBook_WritesBuffSlot17_HitRatePercent()
    {
        var (zone, state, characters, service) = SetUp(AttackIncreaseBookItemId, 14, 1);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(25, state.Buffs.Buff[17 * 2]);
        Assert.Equal(60, state.Buffs.Buff[17 * 2 + 1]);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task DodgeIncreaseBook_WritesBuffSlot18_DodgeRatePercent()
    {
        var (zone, state, characters, service) = SetUp(DodgeIncreaseBookItemId, 15, 1);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(25, state.Buffs.Buff[18 * 2]);
        Assert.Equal(60, state.Buffs.Buff[18 * 2 + 1]);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task AssassinScroll_SetsTheDarkAttackMarker()
    {
        var (zone, state, _, service) = SetUp(AssassinScrollItemId, 12, 1);

        await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(HotkeyItemConsumptionResolver.AssassinScrollMarkerValue, state.DarkAttackKind);
    }

    [Fact]
    public async Task DepartedSpiritScroll_SetsTheDarkAttackMarker()
    {
        var (zone, state, _, service) = SetUp(DepartedSpiritScrollItemId, 13, 1);

        await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(HotkeyItemConsumptionResolver.DepartedSpiritScrollMarkerValue, state.DarkAttackKind);
    }

    [Fact]
    public async Task AttackIncreaseBook_SetsTheHitRateMarker_NotTheDarkAttackMarker()
    {
        var (zone, state, _, service) = SetUp(AttackIncreaseBookItemId, 14, 1);

        await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(HotkeyItemConsumptionResolver.HitOrDodgeBuffMarkerValue, state.HitRateKind);
        Assert.Equal(0, state.DarkAttackKind);
    }

    [Fact]
    public async Task DodgeIncreaseBook_SetsTheDodgeRateMarker_NotTheDarkAttackMarker()
    {
        var (zone, state, _, service) = SetUp(DodgeIncreaseBookItemId, 15, 1);

        await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(HotkeyItemConsumptionResolver.HitOrDodgeBuffMarkerValue, state.DodgeRateKind);
        Assert.Equal(0, state.DarkAttackKind);
    }

    [Fact]
    public async Task AssassinScroll_OutsideAllowlistedZone_RejectedCleanly_SlotUntouched()
    {
        var (zone, state, characters, service) = SetUp(AssassinScrollItemId, 12, 2, mapId: 5);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(0, state.Buffs.Buff[15 * 2]);
        Assert.Null(characters.LastUpsertHotkeySlot);
        Assert.Equal(2, state.GetHotkeySlot(0, 0).Value2);
    }

    [Fact]
    public async Task DepartedSpiritScroll_WhileAssassinScrollActive_RejectedCleanly_SlotUntouched()
    {
        var (zone, state, characters, service) = SetUp(DepartedSpiritScrollItemId, 13, 1);
        state.DarkAttackKind = HotkeyItemConsumptionResolver.AssassinScrollMarkerValue;

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.RejectedClean, outcome);
        Assert.Equal(HotkeyItemConsumptionResolver.AssassinScrollMarkerValue, state.DarkAttackKind);
        Assert.Null(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task AssassinScroll_WhileAssassinScrollAlreadyActive_RefreshesTheBuff_NotRejected()
    {
        var (zone, state, characters, service) = SetUp(AssassinScrollItemId, 12, 2);
        state.DarkAttackKind = HotkeyItemConsumptionResolver.AssassinScrollMarkerValue;

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(3, state.Buffs.Buff[15 * 2]);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }
}
