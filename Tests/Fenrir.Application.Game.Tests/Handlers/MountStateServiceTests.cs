using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class MountStateServiceTests
{
    private static async Task<MountStateResult> RunToCompletionAsync(ValueTask<MountStateResult> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("MountStateService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, float posX = 10f, float posZ = 10f)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, posX: posX, posZ: posZ)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    private static MountStateService CreateService(FakeCharacterRepository? characters = null,
        FakeEventLogRepository? eventLog = null)
    {
        return new MountStateService(characters ?? new FakeCharacterRepository(),
            eventLog ?? new FakeEventLogRepository(), NullLogger<MountStateService>.Instance);
    }

    [Fact]
    public async Task Select_ValidSlot_RepliesAndMirrorsIndex()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 1, 4, CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.Select, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(4, player!.AnimalIndex);
    }

    [Fact]
    public async Task Select_SlotOutOfRange_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 1, 99, CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.NoReply, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.AnimalIndex);
    }

    [Fact]
    public async Task Mount_ValidPreconditions_HealsAndBroadcastsMountThenAbsorbReset()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        state.AnimalIndex = 2;
        state.AnimalTime = 5;
        state.ActionSort = 1;
        state.AnimalAbsorbState = 1;
        state.MountGarage = ImmutableArray.Create(0, 0, 1006, 0, 0, 0, 0, 0, 0, 0);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 3, 0, CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.Mount, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(12, mover!.AnimalIndex);
        Assert.Equal(1006, mover.AnimalNumber);
        Assert.Equal(0, mover.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
    }

    [Fact]
    public async Task Mount_NotIdle_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AnimalIndex = 2;
        state.AnimalTime = 5;
        state.ActionSort = 0;
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 3, 0, CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(MountStateOutcome.NoReply, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(2, player!.AnimalIndex);
        Assert.Equal(0, player.AnimalNumber);
    }

    [Fact]
    public async Task Dismount_ValidPreconditions_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AnimalIndex = 12;
        state.AnimalNumber = 1006;
        state.AnimalAbsorbState = 1;
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 4, 0, CancellationToken.None), zone);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(MountStateOutcome.Dismount, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(2, mover!.AnimalIndex);
        Assert.Equal(0, mover.AnimalNumber);
        Assert.Equal(0, mover.AnimalAbsorbState);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(120)]
    public async Task UnsupportedSort_Aborts(int sort)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, sort, 0, CancellationToken.None), zone);

        Assert.Equal(MountStateOutcome.Disconnect, result.Outcome);
    }

    [Fact]
    public async Task DeleteMount_ValidOwnedMount_GrantsCpAndClearsSlot()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.AnimalIndex = 3;
        state.MountGarage = state.MountGarage.SetItem(3, 1006);
        state.ContributionPoints = 100;
        var eventLogRepo = new FakeEventLogRepository();
        var service = CreateService(eventLog: eventLogRepo);

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 5, 1006, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.DeleteMount, result.Outcome);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(350, player!.ContributionPoints);
        Assert.Equal(-1, player.AnimalIndex);
        Assert.Equal(0, player.MountGarage[3]);
        Assert.Single(eventLogRepo.LoggedEvents);
        Assert.Equal(EventLogCategory.MountAttribute, eventLogRepo.LoggedEvents[0].Category);
    }

    [Fact]
    public async Task DeleteMount_ValueDoesNotMatchAnyOwnedSlot_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.AnimalIndex = 3;
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 5, 4242, CancellationToken.None), zone);

        Assert.Equal(MountStateOutcome.Disconnect, result.Outcome);
    }

    [Fact]
    public async Task DeleteAttribute_MaterialPresent_ConsumesItemAndClearsSlot()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.AnimalIndex = 13; // mounted range -> garage slot 3
        state.MountRolledAttributes = state.MountRolledAttributes.SetItem(
            MountStateResolver.AttributeIndex(3, 4), 7);

        var slots = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, new ItemStack(1225, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var containers =
            ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, slots));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var characters = new FakeCharacterRepository();
        var eventLogRepo = new FakeEventLogRepository();
        var service = CreateService(characters, eventLogRepo);

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 7, 5, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.DeleteAttribute, result.Outcome);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.ItemId == 1225);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(0,
            player!.MountRolledAttributes[
                MountStateResolver.AttributeIndex(3, 4)]);
        Assert.Single(eventLogRepo.LoggedEvents);
    }

    [Fact]
    public async Task DeleteAttribute_MaterialMissing_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.AnimalIndex = 13;
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 7, 5, CancellationToken.None), zone);

        Assert.Equal(MountStateOutcome.Disconnect, result.Outcome);
    }

    [Fact]
    public async Task TransferAttribute_SelectionOutOfRange_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.AnimalIndex = -1;
        var service = CreateService();

        var result = await RunToCompletionAsync(
            service.ApplyAsync(zone, state, 10, 1, 8, 3, CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(MountStateOutcome.NoReply, result.Outcome);
    }
}
