using System.Collections.Frozen;
using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

public class CostumeStateServiceTests
{
    private const int AccountId = 1;

    private static readonly int StateFlagFrame = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, float posX = 10f, float posZ = 10f)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(AccountId, characterId);
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

    private static CostumeStateService CreateService(FakeCharacterRepository characters,
        FakeEventLogRepository? eventLog = null)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [305] = new(WorldDataTestRows.Item(305), [])
        }.ToFrozenDictionary();

        return new CostumeStateService(characters, ZoneTestKit.EmptyWorldData(itemsById),
            eventLog ?? new FakeEventLogRepository(), NullLogger<CostumeStateService>.Instance);
    }

    [Fact]
    public async Task Select_OccupiedSlot_RepliesAndMirrorsIndex()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.CostumeWardrobe = state.CostumeWardrobe.SetItem(2, 305);
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 1, 2, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(2, player!.CostumeIndex);
        Assert.Equal(CostumeStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);
    }

    [Fact]
    public async Task Select_EmptySlot_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 1, 2, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.CostumeIndex);
        Assert.Equal(CostumeStateOutcome.NoReply, result.Outcome);
    }

    [Fact]
    public async Task Equip_Success_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe); // neighbor's own Enter-broadcast join packet, not under test
        state.CostumeIndex = 4;
        state.CostumeWardrobe = state.CostumeWardrobe.SetItem(4, 305);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 3, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(14, mover!.CostumeIndex);
        Assert.Equal(305, mover.CostumeNumber);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
        Assert.Equal(CostumeStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);

        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public async Task Remove_Success_HealsAndBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        state.CostumeIndex = 14;
        state.CostumeNumber = 305;
        state.CostumeWardrobe = state.CostumeWardrobe.SetItem(4, 305);
        state.MaxLife = 800;
        state.MaxMana = 300;
        state.Life = 1;
        state.Mana = 1;
        var service = CreateService(new FakeCharacterRepository());

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 4, 0, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover));
        Assert.Equal(4, mover!.CostumeIndex);
        Assert.Equal(0, mover.CostumeNumber);
        Assert.Equal(800, mover.Life);
        Assert.Equal(300, mover.Mana);
        Assert.Equal(CostumeStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public async Task ReturnToInventory_IndexMismatch_RepliesResult1NoDisconnect_AndLogsNothing()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(new FakeCharacterRepository(), eventLog);

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 5, 3, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(CostumeStateOutcome.Reply, result.Outcome);
        Assert.Equal(1, result.ResultCode);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task
        ReturnToInventory_Success_GrantsItemToInventoryAndClearsWardrobeSlot_AndLogsACosmeticDeleteAuditRow()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.CostumeIndex = 3;
        state.CostumeWardrobe = state.CostumeWardrobe.SetItem(3, 305);
        var characters = new FakeCharacterRepository();
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(characters, eventLog);

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 5, 3, CancellationToken.None);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(305, characters.LastReplacedContainer!.Value.Items[0].ItemId);

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(-1, player!.CostumeIndex);
        Assert.Equal(0, player.CostumeWardrobe[3]);

        Assert.Equal(CostumeStateOutcome.Reply, result.Outcome);
        Assert.Equal(0, result.ResultCode);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)1, logged.EventCode);
        Assert.Equal(EventLogCategory.CosmeticDelete, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Null(logged.TargetAccountId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal(305, logged.ItemId);
        Assert.Equal(1, logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
    }

    [Fact]
    public async Task UnsupportedSort_Aborts_AndLogsNothing()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        var eventLog = new FakeEventLogRepository();
        var service = CreateService(new FakeCharacterRepository(), eventLog);

        var result = await service.ApplyAsync(zone, state, 10, AccountId, 9, 0, CancellationToken.None);

        Assert.Equal(CostumeStateOutcome.Disconnect, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }
}
