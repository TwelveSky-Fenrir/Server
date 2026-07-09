using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Gm;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Gm;

// Admin-tier GM grant-pet-experience command (legacy PROCESS_DATA_SEND, opcode 19, unlabeled tSort 700 --
// there is no dedicated legacy wire opcode, multiplexed inside the shared envelope --
// Server/ts25zone/S04_MyWork04.cpp:2062-2083). GmPetExperienceGrantService only ports the OUTER shape (the
// privilege gate, the unconditional accepted result code, and the equipped-pet-slot/catalog-Sort==22
// eligibility check) -- the inner growth-grant computation is deliberately not ported (see that type's own
// remarks), so every request that finds an eligible pet still echoes the real equipped item id but always
// PetExperience=0. Covers the privilege gate, both eligibility outcomes, and the game.EventLog
// (Category=GmAction) audit row -- a Fenrir-authored addition, since the cited legacy body writes none.
public class GmPetExperienceGrantServiceTests
{
    private const int AccountId = 1;
    private const int CharacterId = 10;
    private const short MapId = 1;
    private const int GenericActionDataLength = 130;
    private const int PetItemId = 7000;
    private const int NonPetItemId = 7001; // in the catalog, but Sort != 22.

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeEventLogRepository EventLog) SetUp(short accountGrade)
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId, null, accountGrade);
        session.MarkRegistering();
        session.MarkInWorld();
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        return (session, pipe, zone, state!, new FakeEventLogRepository());
    }

    private static GmPetExperienceGrantService CreateService(FakeEventLogRepository eventLog)
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [PetItemId] = new(WorldDataTestRows.Item(PetItemId) with { Sort = 22 }, []),
            [NonPetItemId] = new(WorldDataTestRows.Item(NonPetItemId), [])
        }.ToFrozenDictionary();

        return new GmPetExperienceGrantService(ZoneTestKit.EmptyWorldData(itemsById), eventLog,
            NullLogger<GmPetExperienceGrantService>.Instance);
    }

    /// <summary>Mirrors an equipment container (slot 8 == PetSlots.EquipmentSlot) into the zone/state, tick-driven.</summary>
    private static void EquipPetSlot(Zone zone, int characterId, int itemId)
    {
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty.SetItem(PetSlots.EquipmentSlot,
                new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0))));
        zone.PostInventoryCommand(new InventoryZoneCommand(characterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static byte[] RequestData()
    {
        return new byte[GenericActionDataLength];
    }

    [Fact]
    public async Task HandleAsync_CallerNotAdminTier_AbortsWithNoReply_AndLogsNothing()
    {
        var (session, pipe, _, state, eventLog) = SetUp((short)GmCommandTier.Elevated);
        var service = CreateService(eventLog);

        await service.HandleAsync(RequestData(), session, state, CancellationToken.None);

        Assert.Equal(DisconnectReason.GmCommandLogout, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_NoPetEquipped_EchoesNoPetSentinel_AndLogsOutcomeZero()
    {
        var (session, pipe, _, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        var service = CreateService(eventLog);
        var data = RequestData();

        await service.HandleAsync(data, session, state, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var expected = (byte[])data.Clone();
        // Server/ts25zone/S04_MyWork04.cpp:2071 -- legacy unconditionally initializes r->tPetID = -1 before the
        // equipped-pet check, so -1 (not 0) is the confirmed "no pet" sentinel on the wire.
        new GmPetExperienceGrantPayload { PetId = -1, PetExperience = 0 }.Write(expected);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = 700, Data = expected, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)2, logged.EventCode); // GmActionEventCodes.PetExperienceGrant (internal, not visible here).
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(AccountId, logged.ActorAccountId);
        Assert.Equal(CharacterId, logged.ActorCharacterId);
        Assert.Null(logged.ItemId);
        Assert.Null(logged.Quantity);
        Assert.Equal((byte)0, logged.Outcome);
        Assert.Equal("GrowthGrantSubsystemNotPorted;PetExperience=0", logged.Payload);
    }

    [Fact]
    public async Task HandleAsync_NonPetItemEquippedInPetSlot_TreatedAsNoEligiblePet()
    {
        var (session, _, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        EquipPetSlot(zone, CharacterId, NonPetItemId); // catalog Sort != 22.
        var service = CreateService(eventLog);

        await service.HandleAsync(RequestData(), session, state, CancellationToken.None);

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((byte)0, logged.Outcome);
        Assert.Null(logged.ItemId);
    }

    [Fact]
    public async Task HandleAsync_EligiblePetEquipped_EchoesEquippedPetId_AndLogsOutcomeOne()
    {
        var (session, pipe, zone, state, eventLog) = SetUp((short)GmCommandTier.Admin);
        EquipPetSlot(zone, CharacterId, PetItemId); // catalog Sort == 22.
        var service = CreateService(eventLog);
        var data = RequestData();

        await service.HandleAsync(data, session, state, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var expected = (byte[])data.Clone();
        new GmPetExperienceGrantPayload { PetId = PetItemId, PetExperience = 0 }.Write(expected);
        await PacketAssert.AssertSentAsync(pipe,
            new GenericActionResponse { Result = 0, Sort = 700, Data = expected, RuneValue = 0 });

        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Equal((short)2, logged.EventCode);
        Assert.Equal(EventLogCategory.GmAction, logged.Category);
        Assert.Equal(PetItemId, logged.ItemId);
        Assert.Null(logged.Quantity);
        Assert.Equal((byte)1, logged.Outcome);
        Assert.Equal("GrowthGrantSubsystemNotPorted;PetExperience=0", logged.Payload);
    }
}
