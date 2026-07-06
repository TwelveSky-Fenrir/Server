using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class StellarCoreStateServiceTests
{
    private static readonly int StateFlagFrame = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    private static StellarCoreStateService CreateService(FakeCharacterRepository characters)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [76527] = new(WorldDataTestRows.Item(76527), [])
        }.ToFrozenDictionary();

        return new StellarCoreStateService(characters, ZoneTestKit.EmptyWorldData(itemsById),
            NullLogger<StellarCoreStateService>.Instance);
    }

    [Fact]
    public async Task Select_OccupiedSlot_RepliesAndMirrorsIndex()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(2, 76527);
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, 1, 2, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(2, player!.StellarCoreIndex);
        Assert.Equal(StellarCoreStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);
    }

    [Fact]
    public async Task Select_EmptySlot_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, 1, 2, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.StellarCoreIndex);
        Assert.Equal(StellarCoreStateOutcome.NoReply, result.Outcome);
    }

    [Fact]
    public async Task Equip_Success_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        state.StellarCoreIndex = 4;
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(4, 76527);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, 3, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(14, mover!.StellarCoreIndex);
        Assert.Equal(76527, mover.StellarCoreNumber);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
        Assert.Equal(StellarCoreStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);

        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public async Task Remove_Success_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        state.StellarCoreIndex = 14;
        state.StellarCoreNumber = 76527;
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(4, 76527);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, 4, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(4, mover!.StellarCoreIndex);
        Assert.Equal(0, mover.StellarCoreNumber);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
        Assert.Equal(StellarCoreStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public async Task ReturnToInventory_IndexMismatch_RepliesResult1NoDisconnect()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, 5, 3, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(StellarCoreStateOutcome.Reply, result.Outcome);
        Assert.Equal(1, result.ResultCode);
    }

    [Fact]
    public async Task ReturnToInventory_Success_GrantsItemToInventoryAndClearsWardrobeSlot()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.StellarCoreIndex = 3;
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(3, 76527);
        var characters = new FakeCharacterRepository();
        var service = CreateService(characters);

        var result = await service.ApplyAsync(zone, state, 10, 5, 3, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(76527, characters.LastReplacedContainer!.Value.Items[0].ItemId);

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.StellarCoreIndex);
        Assert.Equal(0, player.StellarCoreWardrobe[3]);

        Assert.Equal(StellarCoreStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);
    }

    [Fact]
    public async Task UnsupportedSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, 9, 0, CancellationToken.None);

        Assert.Equal(StellarCoreStateOutcome.Disconnect, result.Outcome);
    }
}
