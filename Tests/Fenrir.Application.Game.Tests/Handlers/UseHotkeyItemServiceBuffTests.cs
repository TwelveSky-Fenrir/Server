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

/// <summary>
///     End-to-end coverage (resolver -&gt; service -&gt; posted <c>HotkeySlotMirrorZoneCommand</c> -&gt; drained
///     onto the tick-owned <see cref="Zone" />/<see cref="PlayerRuntimeState" />) for the four fixed-value
///     self-buff scrolls/books added to potion types 12-15: Assassin Scroll (world.Items 1364), Departed
///     Spirit Scroll (1156), Attack Increase Book (1471), Dodge Increase Book (1472). See
///     <c>HotkeyItemConsumptionResolverTests</c> for the pure-resolver-level coverage of the same four items.
/// </summary>
public class UseHotkeyItemServiceBuffTests
{
    private const int AssassinScrollItemId = 1364;
    private const int DepartedSpiritScrollItemId = 1156;
    private const int AttackIncreaseBookItemId = 1471;
    private const int DodgeIncreaseBookItemId = 1472;
    private const byte ConsumableSort = HotkeyItemConsumptionResolver.ConsumableItemCategory;

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        UseHotkeyItemService Service) SetUp(int itemId, int potionType1, int quantity)
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

        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
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
        Assert.Equal(80, state.Buffs.Buff[15 * 2 + 1]); // 40s @ 500ms legacy tick

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal(10, upsert.CharacterId);
        Assert.Equal(AssassinScrollItemId, upsert.Sort); // bound id, still 1 unit left
        Assert.Equal(1, upsert.Value1); // remaining quantity
        Assert.Equal((int)HotkeyBindingKind.Item, upsert.Value2); // kind discriminator
    }

    [Fact]
    public async Task DepartedSpiritScroll_WritesBuffSlot15_Duration60Seconds()
    {
        var (zone, state, characters, service) = SetUp(DepartedSpiritScrollItemId, 13, 1);

        var outcome = await service.UseAsync(zone, state, 10, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(UseHotkeyItemOutcome.Success, outcome);
        Assert.Equal(3, state.Buffs.Buff[15 * 2]);
        Assert.Equal(120, state.Buffs.Buff[15 * 2 + 1]); // 60s @ 500ms legacy tick

        // Last unit consumed -> the hotkey slot is cleared outright, not written as a zeroed-but-bound row.
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
        Assert.Equal(120, state.Buffs.Buff[17 * 2 + 1]);
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
        Assert.Equal(120, state.Buffs.Buff[18 * 2 + 1]);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }
}
