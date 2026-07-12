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
                mapId => new ZoneDefinition(new ZoneRowDto(mapId, 0f, 0f, 0f), [], [], [], [], []))
            .ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId, .. destinationMapIds]);

        var worldState = ZoneTestKit.CreateWorldState();
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeEventLogRepository(),
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
                mapId => new ZoneDefinition(new ZoneRowDto(mapId, 0f, 0f, 0f), [], [], [], [], []))
            .ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId, .. destinationMapIds]);

        var worldState = ZoneTestKit.CreateWorldState();
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeEventLogRepository(),
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
    public async Task Flagged_DestinationCapitalGroup_NoQualifyingAlliance_KicksTheSession()
    {
        var (service, session, sourceZone) = CreateService(2, 1, true, 7);

        await service.HandleAsync(Request(2, 7), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_DestinationZone38_IsAlwaysExempt()
    {
        var (service, session, sourceZone) = CreateService(2, 1, true, 38);

        await service.HandleAsync(Request(2, 38), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_DestinationOwnCapitalGroup_StillKicked_NoOwnTribeBypassExists()
    {
        var (service, session, sourceZone) = CreateService(2, 0, true, 3);

        await service.HandleAsync(Request(2, 3), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task NotFlagged_TransfersNormally_EvenIntoACapitalGroupWithNoQualifyingAlliance()
    {
        var (service, session, sourceZone) = CreateService(2, 1, false, 7);

        await service.HandleAsync(Request(2, 7), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_DestinationNotACapitalGroup_IsNeverKicked_RegardlessOfSourceZone()
    {
        var (service, session, sourceZone) = CreateService(2, 1, true, 50);

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_Tribe0CapitalGroupDestination_OwningFactionAlliedWithNonZeroTribe_StillKicked()
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [2] = new(new ZoneRowDto(2, 0f, 0f, 0f), [], [], [], [])
        }.ToFrozenDictionary());
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([50, 2]);

        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(0, 2, true);
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeEventLogRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[50];
        session.CurrentZone = sourceZone;
        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 50, tribe: 1)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        state!.ReviveHackFlag = true;

        await service.HandleAsync(Request(50, 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_Tribe1CapitalGroupDestination_OwningFactionAlliedWithTribe0_SuspendsKick_ForAnyAvatarTribe()
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [7] = new(new ZoneRowDto(7, 0f, 0f, 0f), [], [], [], [])
        }.ToFrozenDictionary());
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([50, 7]);

        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(1, 0, true);
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeEventLogRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[50];
        session.CurrentZone = sourceZone;
        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 50, tribe: 3)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        state!.ReviveHackFlag = true;

        await service.HandleAsync(Request(50, 7), session, CancellationToken.None);

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
    public async Task Flagged_PresentZoneMismatch_IsKickedForReviveHack_NotForMalformedInput()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [7]);

        await service.HandleAsync(Request(3, 7), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_Sort2WithoutOperatorRank_IsKickedForReviveHack_NotForTheGmSortCheck()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [7]);

        await service.HandleAsync(Request(2, 7, 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_MalformedSortValue_IsKickedForReviveHack_NotForTheSortRangeCheck()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [7]);

        await service.HandleAsync(Request(2, 7, 99), session, CancellationToken.None);

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

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task UnrecognizedActionCategory_IsRejectedWithFaulted(int sort)
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 0, [50]);

        await service.HandleAsync(Request(2, 50, sort), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public async Task RecognizedNonGmActionCategory_IsNotRejectedForItsCategory(int sort)
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 0, [50]);

        await service.HandleAsync(Request(2, 50, sort), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task DestinationZoneNumberJustBelowExclusiveUpperBound_IsAccepted()
    {
        var (service, session, sourceZone) = CreateServiceWithGrade(2, 1, false, 0, [349]);

        await service.HandleAsync(Request(2, 349), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);

        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.False(sourceZone.TryGetPlayer(CharacterId, out _));
    }

    [Fact]
    public async Task DestinationZoneNumberAtExclusiveUpperBound_IsRejectedAsMalformed()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 0, []);

        await service.HandleAsync(Request(2, 350), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task DestinationZoneNumberJustPastUpperBound_IsRejectedAsMalformed()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 0, []);

        await service.HandleAsync(Request(2, 351), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task SuccessfulTransfer_QueuesPendingTransferMarkerAsynchronously_StillCompletesHandoffOnNextTick()
    {
        var (service, session, sourceZone) = CreateService(2, 1, false, 50);

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        Assert.False(state!.IsMovingZone);

        sourceZone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(sourceZone.TryGetPlayer(CharacterId, out _));
    }

    private static (ZoneMoveService Service, ZoneClientSession Session, Zone SourceZone, FakeDuplexPipe Pipe)
        CreateServiceWithSymbolBattle(short sourceMapId, bool battleActive, params short[] destinationMapIds)
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: destinationMapIds
            .ToDictionary(mapId => mapId,
                mapId => new ZoneDefinition(new ZoneRowDto(mapId, 0f, 0f, 0f), [], [], [], [], []))
            .ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId, .. destinationMapIds]);

        var worldState = ZoneTestKit.CreateWorldState();
        if (battleActive)
            worldState.StartTribeSymbolBattle();

        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            new FakeCharacterShardLocationRepository(),
            new FakeEventLogRepository(),
            Options.Create(new GameServerOptions()), NullLogger<ZoneMoveService>.Instance);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[sourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, sourceMapId)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        return (service, session, sourceZone, pipe);
    }

    [Theory]
    [InlineData((short)40)]
    [InlineData((short)41)]
    [InlineData((short)42)]
    public async Task SymbolBattleActive_GuardedSourceZone_DestinationZone38_DisconnectsWithNothingSent(
        short sourceZoneId)
    {
        var (service, session, _, pipe) = CreateServiceWithSymbolBattle(sourceZoneId, true, 38);

        await service.HandleAsync(Request(sourceZoneId, 38), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task SymbolBattleActive_ReverseDirection_OutOfZone38_TransfersNormally()
    {
        var (service, session, _, _) = CreateServiceWithSymbolBattle(38, true, 40);

        await service.HandleAsync(Request(38, 40), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task SymbolBattleActive_BetweenGuardedZonesThemselves_TransfersNormally()
    {
        var (service, session, _, _) = CreateServiceWithSymbolBattle(40, true, 41);

        await service.HandleAsync(Request(40, 41), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task SymbolBattleActive_GuardedSourceZone_NonZone38Destination_TransfersNormally()
    {
        var (service, session, _, _) = CreateServiceWithSymbolBattle(40, true, 50);

        await service.HandleAsync(Request(40, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task SymbolBattleNotActive_GuardedSourceZone_DestinationZone38_TransfersNormally()
    {
        var (service, session, _, _) = CreateServiceWithSymbolBattle(40, false, 38);

        await service.HandleAsync(Request(40, 38), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }

    private static (ZoneMoveService Service, ZoneClientSession Session,
        FakeCharacterShardLocationRepository ShardLocations) CreateServiceWithShardLocations(
            short sourceMapId, byte tribe, params short[] destinationMapIds)
    {
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: destinationMapIds
            .ToDictionary(mapId => mapId,
                mapId => new ZoneDefinition(new ZoneRowDto(mapId, 0f, 0f, 0f), [], [], [], [], []))
            .ToFrozenDictionary());

        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId, .. destinationMapIds]);

        var worldState = ZoneTestKit.CreateWorldState();
        var shardLocations = new FakeCharacterShardLocationRepository();
        var options = new GameServerOptions { ShardId = 9 };
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(), PortalProximityCatalog.Empty,
            new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()),
            new FakeSessionTicketRepository(),
            shardLocations,
            new FakeEventLogRepository(),
            Options.Create(options), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        var sourceZone = zones[sourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, sourceMapId, tribe: tribe)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));

        return (service, session, shardLocations);
    }

    [Fact]
    public async Task SuccessfulTransfer_RegistersTheCharacterWithTheSharedLocationDirectory_BeforeTheReplyIsSent()
    {
        var (service, session, shardLocations) = CreateServiceWithShardLocations(2, 1, 50);

        await service.HandleAsync(Request(2, 50), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        var call = Assert.Single(shardLocations.UpsertCalls);
        Assert.Equal(CharacterId, call.CharacterId);
        Assert.Equal((byte)9, call.ShardId);
        Assert.Equal((short)50, call.MapId);
        Assert.Equal("Hero", call.AvatarName);
        Assert.Equal((byte)1, call.Tribe);
    }

    [Fact]
    public async Task LocationDirectoryRegistrationFails_PropagatesWithoutSendingAReply()
    {
        var (service, session, shardLocations) = CreateServiceWithShardLocations(2, 1, 50);
        shardLocations.ThrowOnUpsert = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.HandleAsync(Request(2, 50), session, CancellationToken.None).AsTask());
    }
}
