using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Hotkeys;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Hotkeys;

public class HotkeyActionServiceTests
{
    private const int StackableConsumableItemId = 90001;
    private const int NonStackableItemId = 90002;
    private const byte StackableSort = 2;
    private const byte EquipmentSort = 1;

    private static (Zone Zone, PlayerRuntimeState State, FakeCharacterRepository Characters,
        HotkeyActionService Service) SetUp()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [StackableConsumableItemId] = new(
                WorldDataTestRows.Item(StackableConsumableItemId) with { Sort = StackableSort, PotionType1 = 1 },
                []),
            [NonStackableItemId] = new(
                WorldDataTestRows.Item(NonStackableItemId) with { Sort = EquipmentSort }, [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);

        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));

        var characters = new FakeCharacterRepository();
        var service = new HotkeyActionService(characters, worldData, NullLogger<HotkeyActionService>.Instance);
        return (zone, state!, characters, service);
    }

    [Fact]
    public async Task BindItemAsync_MovesStackableItemFromInventoryIntoEmptyHotkeySlot()
    {
        var (zone, state, characters, service) = SetUp();
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0)
                .SetItem(5, new ItemStack(StackableConsumableItemId, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        var move = new DefaultPData
            { Page1 = 0, Index1 = 5, Quantity1 = 3, Page2 = 0, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.BindItemAsync(zone, state, 10, move, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);

        var newSlot = state.GetHotkeySlot(0, 0);
        Assert.Equal(HotkeyBindingKind.Item, newSlot.Kind);
        Assert.Equal(StackableConsumableItemId, newSlot.Value1);
        Assert.Equal(3, newSlot.Value2);

        var remainingSource = state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 5);
        Assert.Equal(7, remainingSource!.Value.Quantity);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal(10, upsert.CharacterId);
        Assert.Equal(StackableConsumableItemId, upsert.Sort);
        Assert.Equal(3, upsert.Value1);
        Assert.Equal((int)HotkeyBindingKind.Item, upsert.Value2);

        var replaced = Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, replaced.Container);
    }

    [Fact]
    public async Task BindItemAsync_RejectsNonStackableItem_Disconnects()
    {
        var (zone, state, _, service) = SetUp();
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0)
                .SetItem(5, new ItemStack(NonStackableItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        var move = new DefaultPData
            { Page1 = 0, Index1 = 5, Quantity1 = 1, Page2 = 0, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.BindItemAsync(zone, state, 10, move, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public async Task BindItemAsync_RejectsExcludedPotionSubtype_Disconnects(int potionType1)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [StackableConsumableItemId] = new(
                WorldDataTestRows.Item(StackableConsumableItemId) with
                {
                    Sort = StackableSort, PotionType1 = (short)potionType1
                }, [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0)
                .SetItem(5, new ItemStack(StackableConsumableItemId, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        var characters = new FakeCharacterRepository();
        var service = new HotkeyActionService(characters, worldData, NullLogger<HotkeyActionService>.Instance);

        var move = new DefaultPData
            { Page1 = 0, Index1 = 5, Quantity1 = 3, Page2 = 0, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.BindItemAsync(zone, state, 10, move, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
        Assert.Null(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task WithdrawItemAsync_MovesBoundItemBackIntoEmptyInventorySlot()
    {
        var (zone, state, characters, service) = SetUp();
        state.SetHotkeySlot(0, 0, new HotkeySlot(HotkeyBindingKind.Item, StackableConsumableItemId, 5));

        var move = new DefaultPData
        {
            Page1 = 0, Index1 = 0, Quantity1 = 2, Page2 = 0, Index2 = 10, XPost2 = 1, YPost2 = 1
        };
        var outcome = await service.WithdrawItemAsync(zone, state, 10, move, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);

        var remainingHotkey = state.GetHotkeySlot(0, 0);
        Assert.Equal(HotkeyBindingKind.Item, remainingHotkey.Kind);
        Assert.Equal(3, remainingHotkey.Value2);

        var destination = state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 10);
        Assert.Equal(StackableConsumableItemId, destination!.Value.ItemId);
        Assert.Equal(2, destination.Value.Quantity);

        Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.NotNull(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task RearrangeAsync_SourceEqualsDestination_IsNoOpSuccess_NoPersistenceNoMirror()
    {
        var (zone, state, characters, service) = SetUp();
        state.SetHotkeySlot(1, 2, new HotkeySlot(HotkeyBindingKind.Emoticon, 3, 0));

        var move = new DefaultPData
            { Page1 = 1, Index1 = 2, Quantity1 = 0, Page2 = 1, Index2 = 2, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.RearrangeAsync(zone, state, 10, move, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.Null(characters.LastUpsertHotkeySlot);

        var unchanged = state.GetHotkeySlot(1, 2);
        Assert.Equal(HotkeyBindingKind.Emoticon, unchanged.Kind);
        Assert.Equal(3, unchanged.Value1);
    }

    [Fact]
    public async Task RearrangeAsync_MovesEmoticonBetweenTwoHotkeySlots()
    {
        var (zone, state, _, service) = SetUp();
        state.SetHotkeySlot(0, 0, new HotkeySlot(HotkeyBindingKind.Emoticon, 4, 0));

        var move = new DefaultPData
            { Page1 = 0, Index1 = 0, Quantity1 = 0, Page2 = 0, Index2 = 1, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.RearrangeAsync(zone, state, 10, move, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.True(state.GetHotkeySlot(0, 0).IsEmpty);
        var moved = state.GetHotkeySlot(0, 1);
        Assert.Equal(HotkeyBindingKind.Emoticon, moved.Kind);
        Assert.Equal(4, moved.Value1);
    }

    [Fact]
    public async Task UnbindAsync_ClearsEmoticonSlot()
    {
        var (zone, state, characters, service) = SetUp();
        state.SetHotkeySlot(0, 3, new HotkeySlot(HotkeyBindingKind.Emoticon, 2, 0));

        var outcome = await service.UnbindAsync(zone, state, 10, 0, 3, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.True(state.GetHotkeySlot(0, 3).IsEmpty);

        var upsert = Assert.NotNull(characters.LastUpsertHotkeySlot);
        Assert.Equal((int)HotkeyBindingKind.None, upsert.Value2);
    }

    [Fact]
    public async Task UnbindAsync_RejectsItemBinding_Disconnects()
    {
        var (zone, state, _, service) = SetUp();
        state.SetHotkeySlot(0, 3, new HotkeySlot(HotkeyBindingKind.Item, StackableConsumableItemId, 1));

        var outcome = await service.UnbindAsync(zone, state, 10, 0, 3, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
    }

    [Fact]
    public async Task BindSkillAsync_CopiesLearnedSkillIntoEmptyHotkeySlot_SourceUntouched()
    {
        var (zone, state, characters, service) = SetUp();
        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(555, 3));

        var outcome = await service.BindSkillAsync(zone, state, 10, 0, 2, 0, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        var bound = state.GetHotkeySlot(0, 0);
        Assert.Equal(HotkeyBindingKind.Skill, bound.Kind);
        Assert.Equal(555, bound.Value1);
        Assert.Equal(2, bound.Value2);

        Assert.Equal(3, state.LearnedSkills[0].Grade);
        Assert.NotNull(characters.LastUpsertHotkeySlot);
    }

    [Fact]
    public async Task BindSkillAsync_GradeAboveInvestedPoints_Disconnects()
    {
        var (zone, state, _, service) = SetUp();
        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(555, 1));

        var outcome = await service.BindSkillAsync(zone, state, 10, 0, 5, 0, 0, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
    }
}
