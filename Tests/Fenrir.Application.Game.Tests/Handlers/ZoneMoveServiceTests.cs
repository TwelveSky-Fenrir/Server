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

    /// <summary>
    ///     Same shape as <see cref="CreateService" /> but also lets ordering/GM-rank tests control
    ///     <paramref name="accountGrade" /> (<see cref="ZoneClientSession.IsGm" />'s backing value) -- the
    ///     shared helper above always mints a plain, non-GM session.
    /// </summary>
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
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(),
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

    // --- Ordering fix (Findings 3 & 10): mProtect_ReviveHack now runs UNCONDITIONALLY FIRST -- before the
    // same-zone no-op short-circuit, the malformed-target/present-zone validation, and the sort-reason checks
    // -- matching legacy's own line ordering (S04_MyWork02.cpp:2019-2064 strictly before 2065-2114). Each test
    // below picks an input that would have been rejected/ignored for a DIFFERENT reason under the old
    // ordering, and asserts the revive-hack kick (DisconnectReason.StateViolation) wins instead. ---

    [Fact]
    public async Task Flagged_SameZoneNoOpRequest_IsStillKicked_NotSilentlyIgnored()
    {
        // Source zone 2 (faction-0 territory), avatar tribe 1 (mismatch), destination == source (a same-zone
        // no-op request). Under the OLD ordering this would have hit the "same zone -- silent ignore" branch
        // before the revive-hack check ever ran; now the revive-hack check runs first.
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, []);

        await service.HandleAsync(Request(2, 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_MalformedTargetZoneNumber_IsKickedForReviveHack_NotForMalformedInput()
    {
        // Destination 9999 is out of the valid 1-349 range and would normally abort with Faulted. Flagged +
        // faction-mismatched + non-38 destination means the revive-hack check fires first instead.
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, []);

        await service.HandleAsync(Request(2, 9999), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_PresentZoneMismatch_IsKickedForReviveHack_NotForMalformedInput()
    {
        // Claimed present-zone (3) disagrees with the session's actual current zone (2) -- would normally
        // abort with Faulted. Revive-hack still fires first.
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [50]);

        await service.HandleAsync(Request(3, 50), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_Sort2WithoutOperatorRank_IsKickedForReviveHack_NotForTheGmSortCheck()
    {
        // Sort 2 (GM transfer) from a non-operator session would normally abort with Faulted. Revive-hack
        // still fires first, before that switch is ever reached.
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [50]);

        await service.HandleAsync(Request(2, 50, sort: 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_MalformedSortValue_IsKickedForReviveHack_NotForTheSortRangeCheck()
    {
        // Sort 99 is out of the valid 2-12 range and would normally abort with Faulted. Revive-hack still
        // fires first.
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [50]);

        await service.HandleAsync(Request(2, 50, sort: 99), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task Flagged_DestinationZone38_StillExempt_EvenWhenRequestIsOtherwiseMalformed()
    {
        // Destination 38 is always exempt from the revive-hack kick, regardless of how the rest of the
        // request is shaped -- here it's ALSO an invalid sort (99), which the sort-range check would reject
        // with Faulted once reached (this is not a same-zone no-op, so execution proceeds past the
        // revive-hack gate down to that check).
        var (service, session, _) = CreateServiceWithGrade(2, 1, true, 0, [38]);

        await service.HandleAsync(Request(2, 38, sort: 99), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    // --- Fix (Finding 5): Sort == 2 (GM transfer) is rejected only for a non-operator session -- previously
    // always rejected regardless of rank, per an outdated "Fenrir has no GM concept yet" comment.
    // ZoneClientSession.IsGm (AccountGrade >= 1, the same uUserSort < 1 threshold TribeGuardCorridorGate
    // itself bypasses on) is now consulted. ---

    [Fact]
    public async Task NonOperatorSession_Sort2_IsStillRejectedWithFaulted()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 0, [50]);

        await service.HandleAsync(Request(2, 50, sort: 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task OperatorSession_Sort2_IsNotRejected_FallsThroughToTheOrdinaryTransferPath()
    {
        var (service, session, sourceZone) = CreateServiceWithGrade(2, 1, false, 1, [50]);

        await service.HandleAsync(Request(2, 50, sort: 2), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);

        // Confirms the transfer actually proceeded (Leave posted to the source zone), not merely that no
        // abort happened.
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.False(sourceZone.TryGetPlayer(CharacterId, out _));
    }

    [Fact]
    public async Task OperatorSession_OtherSortValues_AreUnaffected_StillTransferNormally()
    {
        var (service, session, _) = CreateServiceWithGrade(2, 1, false, 1, [50]);

        await service.HandleAsync(Request(2, 50, sort: 4), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
    }
}
