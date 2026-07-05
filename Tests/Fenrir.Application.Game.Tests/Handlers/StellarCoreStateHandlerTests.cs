using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class StellarCoreStateHandlerTests
{
    private static readonly int StateResponseFrame = FrameWriter.FrameSizeOf<StellarCoreStateResponse>();
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

    private static StellarCoreStateHandler CreateHandler(FakeCharacterRepository characters)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [76527] = new(WorldDataTestRows.Item(76527), [])
        }.ToFrozenDictionary();

        var service = new StellarCoreStateService(characters, ZoneTestKit.EmptyWorldData(itemsById),
            NullLogger<StellarCoreStateService>.Instance);
        return new StellarCoreStateHandler(service);
    }

    [Fact]
    public async Task Select_OccupiedSlot_RepliesAndMirrorsIndex()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(2, 76527);
        var handler = CreateHandler(new FakeCharacterRepository());

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 1, Value = 2 }, session,
            CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(2, player!.StellarCoreIndex);
        Assert.Equal(StateResponseFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public async Task Select_EmptySlot_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = CreateHandler(new FakeCharacterRepository());

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 1, Value = 2 }, session,
            CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.StellarCoreIndex);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task Equip_Success_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        state.StellarCoreIndex = 4;
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(4, 76527);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var handler = CreateHandler(new FakeCharacterRepository());

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 3, Value = 0 }, session,
            CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(14, mover!.StellarCoreIndex);
        Assert.Equal(76527, mover.StellarCoreNumber);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);

        Assert.Equal(StateResponseFrame + StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public async Task Remove_Success_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.StellarCoreIndex = 14;
        state.StellarCoreNumber = 76527;
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(4, 76527);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var handler = CreateHandler(new FakeCharacterRepository());

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 4, Value = 0 }, session,
            CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(4, mover!.StellarCoreIndex);
        Assert.Equal(0, mover.StellarCoreNumber);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
        Assert.Equal(StateResponseFrame + StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public async Task ReturnToInventory_IndexMismatch_RepliesResult1NoDisconnect()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, _) = Setup(zone, 10);
        var handler = CreateHandler(new FakeCharacterRepository());

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 5, Value = 3 }, session,
            CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(StateResponseFrame, frame.Length);
        Assert.Equal(1,
            BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1))); // Result, right after the 1-byte header
    }

    [Fact]
    public async Task ReturnToInventory_Success_GrantsItemToInventoryAndClearsWardrobeSlot()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.StellarCoreIndex = 3;
        state.StellarCoreWardrobe = state.StellarCoreWardrobe.SetItem(3, 76527);
        var characters = new FakeCharacterRepository();
        var handler = CreateHandler(characters);

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 5, Value = 3 }, session,
            CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(76527, characters.LastReplacedContainer!.Value.Items[0].ItemId);

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.StellarCoreIndex);
        Assert.Equal(0, player.StellarCoreWardrobe[3]);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(StateResponseFrame, frame.Length);
        Assert.Equal(0,
            BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(1))); // Result, right after the 1-byte header
    }

    [Fact]
    public async Task UnsupportedSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, _) = Setup(zone, 10);
        var handler = CreateHandler(new FakeCharacterRepository());

        await handler.HandleAsync(new StellarCoreStateRequest { Sort = 9, Value = 0 }, session,
            CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }
}
