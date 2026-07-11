using System.Collections.Frozen;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

public class ZoneMoveServiceTests
{
    private const int CharacterId = 10;

    private static (ZoneMoveService Service, ZoneClientSession Session, Zone SourceZone) CreateService(
        short sourceMapId, byte tribe, bool reviveHackFlag, params short[] destinationMapIds)
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: destinationMapIds
            .ToDictionary(mapId => mapId,
                mapId => new ZoneDefinition(new ZoneRowDto(mapId, 0f, 0f, 0f), [], [], [], []))
            .ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId, .. destinationMapIds]);

        var worldState = ZoneTestKit.CreateWorldState();
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[sourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, sourceMapId, tribe: tribe)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));

        if (reviveHackFlag)
        {
            Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
            state!.ReviveHackFlag = true;
        }

        return (service, session, sourceZone);
    }

    private static ZoneMoveRequest Request(short presentZone, short targetZone, int sort = 4)
    {
        return new ZoneMoveRequest { Sort = sort, ZoneNumber = targetZone, PresentZoneNumber = presentZone };
    }

        private static (ZoneMoveService Service, ZoneClientSession Session, Zone SourceZone) CreateServiceWithGrade(
        short sourceMapId, byte tribe, bool reviveHackFlag, short accountGrade, short[] destinationMapIds)
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: destinationMapIds
            .ToDictionary(mapId => mapId,
                mapId => new ZoneDefinition(new ZoneRowDto(mapId, 0f, 0f, 0f), [], [], [], []))
            .ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId, .. destinationMapIds]);

        var worldState = ZoneTestKit.CreateWorldState();
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId, accountGrade: accountGrade);
        var sourceZone = zones[sourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, sourceMapId, tribe: tribe)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));

        if (reviveHackFlag)
        {
            Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
            state!.ReviveHackFlag = true;
        }

        return (service, session, sourceZone);
    }

    [Fact]
    public async Task Flagged_FactionTerritoryMismatch_NotZone38_KicksTheSession()
    {
        var (service, session, sourceZone) = CreateService(2, 1, true, 50);

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_DestinationZone38_IsAlwaysExempt_EvenOnAFactionMismatch()
    {
        var (service, session, sourceZone) = CreateService(2, 1, true, 38);

        await service.HandleAsync(Request(2, 38), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_AvatarTribeMatchesOwningFaction_IsNotKicked()
    {
        var (service, session, sourceZone) = CreateService(2, 0, true, 50);

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task NotFlagged_TransfersNormally_EvenOnAFactionMismatch()
    {
        var (service, session, sourceZone) = CreateService(2, 1, false, 50);

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_CurrentZoneNotFactionTerritory_IsNeverKicked()
    {
        var (service, session, sourceZone) = CreateService(999, 1, true, 50);

        await service.HandleAsync(Request(999, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_Faction0Territory_OwningFactionAlliedWithNonZeroFaction_StillKicked()
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [50] = new(new ZoneRowDto(50, 0f, 0f, 0f), [], [], [], [])
        }.ToFrozenDictionary());
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([2, 50]);

        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(0, 2, true);
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[2];
        session.CurrentZone = sourceZone;
        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 2, tribe: 1)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        state!.ReviveHackFlag = true;

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_NonFaction0Territory_OwningFactionAlliedWithFaction0_SuspendsKick_ForAnyAvatar()
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [50] = new(new ZoneRowDto(50, 0f, 0f, 0f), [], [], [], [])
        }.ToFrozenDictionary());
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([7, 50]);

        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(1, 0, true);
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[7];
        session.CurrentZone = sourceZone;
        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 7, tribe: 3)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        state!.ReviveHackFlag = true;

        await service.HandleAsync(Request(7, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }


    [Fact]
    public async Task Flagged_SameZoneNoOpRequest_IsStillKicked_NotSilentlyIgnored()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, []);

        await service.HandleAsync(Request(2, 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_MalformedTargetZoneNumber_IsKickedForReviveHack_NotForMalformedInput()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, []);

        await service.HandleAsync(Request(2, 9999), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_PresentZoneMismatch_IsKickedForReviveHack_NotForMalformedInput()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [50]);

        await service.HandleAsync(Request(3, 50), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_Sort2WithoutOperatorRank_IsKickedForReviveHack_NotForTheGmSortCheck()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [50]);

        await service.HandleAsync(Request(2, 50, 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_MalformedSortValue_IsKickedForReviveHack_NotForTheSortRangeCheck()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [50]);

        await service.HandleAsync(Request(2, 50, 99), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_DestinationZone38_StillExempt_EvenWhenRequestIsOtherwiseMalformed()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [38]);

        await service.HandleAsync(Request(2, 38, 99), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }


    [Fact]
    public async Task NonOperatorSession_Sort2_IsStillRejectedWithFaulted()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 0, [50]);

        await service.HandleAsync(Request(2, 50, 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task OperatorSession_Sort2_IsNotRejected_FallsThroughToTheOrdinaryTransferPath()
    {
        var (service, session, sourceZone) = CreateServiceWithGrade(2, 1, false, 1, [50]);

        await service.HandleAsync(Request(2, 50, 2), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);

        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.False(sourceZone.TryGetPlayer(CharacterId, out _));
    }

    [Fact]
    public async Task OperatorSession_OtherSortValues_AreUnaffected_StillTransferNormally()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 1, [50]);

        await service.HandleAsync(Request(2, 50, 4), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }
}
