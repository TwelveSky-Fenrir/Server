using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Inventory;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory;

public class PetBagActionServiceTests
{
    private const int PetEligibleItemId = 91001;
    private const byte PetEligibleSort = PetBagItemTransferPolicy.PetBagEligibleSort;

    private static (Zone Zone, PlayerRuntimeState State, FakePetBagRepository Repo, PetBagActionService Service)
        SetUp(int petEquippedItemId = 500)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [PetEligibleItemId] = new(WorldDataTestRows.Item(PetEligibleItemId) with { Sort = PetEligibleSort }, [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);

        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));

        if (petEquippedItemId > 0)
            state!.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
                state.Inventory.GetContainer(ContainerMatrix.Equipment)
                    .SetItem(PetSlots.EquipmentSlot,
                        new ItemStack(petEquippedItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        var repo = new FakePetBagRepository();
        var service = new PetBagActionService(repo, worldData, NullLogger<PetBagActionService>.Instance);
        return (zone, state!, repo, service);
    }

    [Fact]
    public async Task DepositAsync_MovesBareEligibleItemFromInventoryIntoEmptyBagSlot()
    {
        var (zone, state, repo, service) = SetUp();
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0)
                .SetItem(5, new ItemStack(PetEligibleItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        var move = new DefaultPData
            { Page1 = 0, Index1 = 5, Quantity1 = 0, Page2 = 3, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.DepositAsync(zone, state, 10, move, false, true, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.Null(state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 5));
        Assert.Equal(PetEligibleItemId, state.GetPetBagSlot(3));

        var deposit = Assert.NotNull(repo.LastDeposit);
        Assert.Equal(10, deposit.CharacterId);
        Assert.Equal(3, deposit.PetBagSlot);
        Assert.Equal(PetEligibleItemId, deposit.PetItemId);
    }

    [Fact]
    public async Task DepositAsync_PetNotEquipped_Disconnects()
    {
        var (zone, state, repo, service) = SetUp(0);
        state.Inventory.ReplaceContainer(ContainerMatrix.InventoryPage0,
            state.Inventory.GetContainer(ContainerMatrix.InventoryPage0)
                .SetItem(5, new ItemStack(PetEligibleItemId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        var move = new DefaultPData
            { Page1 = 0, Index1 = 5, Quantity1 = 0, Page2 = 3, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.DepositAsync(zone, state, 10, move, false, true, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Aborted, outcome.Status);
        Assert.Null(repo.LastDeposit);
    }

    [Fact]
    public async Task WithdrawAsync_MovesBagItemBackIntoEmptyInventorySlot()
    {
        var (zone, state, repo, service) = SetUp();
        state.SetPetBagSlot(2, PetEligibleItemId);

        var move = new DefaultPData
            { Page1 = 0, Index1 = 2, Quantity1 = 0, Page2 = 7, Index2 = 1, XPost2 = 1, YPost2 = 0 };
        var outcome = await service.WithdrawAsync(zone, state, 10, move, false, true, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.Null(state.GetPetBagSlot(2));
        var destination = state.Inventory.GetSlot(ContainerMatrix.InventoryPage0, 7);
        Assert.Equal(PetEligibleItemId, destination!.Value.ItemId);

        var withdraw = Assert.NotNull(repo.LastWithdraw);
        Assert.Equal(2, withdraw.PetBagSlot);
        Assert.Equal(ContainerMatrix.InventoryPage0, withdraw.InventoryContainer);
    }

    [Fact]
    public async Task RearrangeAsync_SourceEqualsDestination_IsNoOpSuccess_NoRepoCallNoMirror()
    {
        var (zone, state, repo, service) = SetUp();
        state.SetPetBagSlot(4, PetEligibleItemId);

        var move = new DefaultPData
            { Page1 = 0, Index1 = 4, Quantity1 = 0, Page2 = 4, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.RearrangeAsync(zone, state, 10, move, false, CancellationToken.None);

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.Null(repo.LastRearrange);
        Assert.Equal(PetEligibleItemId, state.GetPetBagSlot(4));
    }

    [Fact]
    public async Task RearrangeAsync_MovesItemBetweenTwoBagSlots()
    {
        var (zone, state, repo, service) = SetUp();
        state.SetPetBagSlot(1, PetEligibleItemId);

        var move = new DefaultPData
            { Page1 = 0, Index1 = 1, Quantity1 = 0, Page2 = 6, Index2 = 0, XPost2 = 0, YPost2 = 0 };
        var outcome = await service.RearrangeAsync(zone, state, 10, move, false, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(GenericActionStatus.Succeeded, outcome.Status);
        Assert.Null(state.GetPetBagSlot(1));
        Assert.Equal(PetEligibleItemId, state.GetPetBagSlot(6));

        var rearrange = Assert.NotNull(repo.LastRearrange);
        Assert.Equal(1, rearrange.SourceSlot);
        Assert.Equal(6, rearrange.DestinationSlot);
    }
}
