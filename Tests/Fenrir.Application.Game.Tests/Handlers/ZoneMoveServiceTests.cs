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

/// <summary>
///     Covers <see cref="ZoneMoveService" />'s <c>mProtect_ReviveHack</c> zone-transfer companion check
///     (S04_MyWork02.cpp:2017-2064) -- immediate kick while flagged, the zone-38 exemption, and the
///     deliberately asymmetric alliance wording versus the tick-loop gate. Pre-existing transfer/validation
///     behavior is left uncovered here (out of this contract's scope).
/// </summary>
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
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(),
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

    [Fact]
    public async Task Flagged_FactionTerritoryMismatch_NotZone38_KicksTheSession()
    {
        // Source zone 2 (faction-0 territory), avatar tribe 1 (mismatch), no alliance configured.
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
        // Legacy quirk: the faction-0 block's companion check never grants alliance-based leniency at all --
        // tribe 0 (zone 2's owner) is allied with tribe 2 here, which must NOT suspend the kick.
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [50] = new(new ZoneRowDto(50, 0f, 0f, 0f), [], [], [], [])
        }.ToFrozenDictionary());
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([2, 50]);

        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(0, 2, true);
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(),
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
        // Zone 7 is a faction-1 territory block. Tribe 1 (owner) becomes allied with tribe 0 specifically --
        // per the legacy quirk, this suspends the kick for EVERY avatar leaving the zone, including tribe 3
        // (neither the owner nor the ally).
        var worldData = ZoneTestKit.EmptyWorldData(zonesByNumber: new Dictionary<short, ZoneDefinition>
        {
            [50] = new(new ZoneRowDto(50, 0f, 0f, 0f), [], [], [], [])
        }.ToFrozenDictionary());
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([7, 50]);

        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(1, 0, true); // tribe 1 (zone 7's owner) allied with tribe 0
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(),
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
}
