using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Covers <see cref="ZoneMoveService.HandleCrossShardAsync" /> -- the Game-to-Game cross-shard zone-transfer
///     path -- specifically the fix for the confirmed teardown race where <c>GameConnectionHost.OnAcceptedAsync</c>'s
///     unconditional <c>runtime.AccountSessions</c> teardown could run ahead of the destination shard's own
///     <c>ZoneHandshakeService.ConsumeTicketAsync -&gt; TransitionToGameAsync</c> claim. The fix mirrors
///     <c>LoginClientSession.MarkHandoverIssued</c>'s role for the Login-&gt;Game handoff via
///     <see cref="ZoneClientSession.MarkCrossShardTransferPending" />, set immediately before the ticket-mint
///     path's success reply is sent (never speculatively -- see that flag's own remarks).
/// </summary>
public class ZoneMoveServiceCrossShardTests
{
    private const int CharacterId = 10;
    private const short SourceMapId = 2;
    private const short TargetMapId = 50;
    private const byte SourceShardId = 1;
    private const byte DestinationShardId = 2;

    private static (ZoneMoveService Service, ZoneClientSession Session, Zone SourceZone,
        FakeSessionTicketRepository Tickets) CreateService(
            IReadOnlyDictionary<byte, short[]> hostedMapsByShard, params ShardDirectoryEntryDto[] shards)
    {
        // Deliberately empty for TargetMapId: this shard's own ZoneRegistry does not host the target, forcing
        // ZoneMoveService.HandleAsync to fall through to HandleCrossShardAsync exactly as the confirmed gap
        // describes (zones.TryGet miss).
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([SourceMapId]);

        var worldState = ZoneTestKit.CreateWorldState();
        var tickets = new FakeSessionTicketRepository();
        var options = new GameServerOptions { ShardId = SourceShardId };
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            TribeGuardCorridorCatalog.Empty, new TribeGuardCorridorState(),
            new FakeGameServerDirectoryRepository(shards),
            new FakeShardMapAssignmentRepository(hostedMapsByShard),
            tickets,
            Options.Create(options), NullLogger<ZoneMoveService>.Instance);

        var (session, _) = ZoneTestKit.CreateSession(1);
        var sessionToken = Guid.NewGuid();
        session.MarkTicketConsumed(1, CharacterId, sessionToken, accountGrade: 0);
        var sourceZone = zones[SourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, SourceMapId)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));

        return (service, session, sourceZone, tickets);
    }

    private static ZoneMoveRequest Request(short presentZone, short targetZone, int sort = 4)
    {
        return new ZoneMoveRequest { Sort = sort, ZoneNumber = targetZone, PresentZoneNumber = presentZone };
    }

    [Fact]
    public async Task LiveDestinationShardFound_MintsTicket_AndMarksTheSessionCrossShardTransferPending()
    {
        var (service, session, _, tickets) = CreateService(
            new Dictionary<byte, short[]> { [DestinationShardId] = [TargetMapId] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.2", 11001, 0, 100, 5f));

        Assert.False(session.IsCrossShardTransferPending);

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);

        Assert.True(session.IsCrossShardTransferPending);
        Assert.Single(tickets.CreatedTickets);
        Assert.Equal(DestinationShardId, tickets.CreatedTickets[0].ShardId);
        Assert.Equal(session.AccountSessionToken, tickets.CreatedTickets[0].SessionToken);
    }

    [Fact]
    public async Task LiveDestinationShardFound_DoesNotChangeZoneSessionState()
    {
        // MarkCrossShardTransferPending is an ancillary flag, never a State transition (mirrors
        // LoginClientSession.MarkAccountSessionToken's own posture) -- SessionStateGate never reads it.
        var (service, session, _, _) = CreateService(
            new Dictionary<byte, short[]> { [DestinationShardId] = [TargetMapId] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.2", 11001, 0, 100, 5f));
        var stateBefore = session.State;

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);

        Assert.Equal(stateBefore, session.State);
    }

    [Fact]
    public async Task NoLiveShardClaimsTheTargetZone_NeverMintsATicket_AndLeavesTheFlagUnset()
    {
        var (service, session, _, tickets) = CreateService(new Dictionary<byte, short[]>());

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);

        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);
    }

    [Fact]
    public async Task OwnShardIdIsSkippedEvenIfItAppearsInTheDirectory_NeverSelfMintsATicket()
    {
        // Guards against trusting a stale admin.ShardMapAssignments row that disagrees with what this shard's
        // own ZoneRegistry actually loaded at boot (ZoneMoveService's own remarks on this exact self-entry skip).
        var (service, session, _, tickets) = CreateService(
            new Dictionary<byte, short[]> { [SourceShardId] = [TargetMapId] },
            new ShardDirectoryEntryDto(SourceShardId, "10.0.0.1", 11000, 0, 100, 5f));

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);

        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);
    }

    // --- Fix (Finding 11): mProtect_ReviveHack now runs before zones.TryGet/HandleCrossShardAsync, so a
    // flagged session whose current zone/tribe combination fails the check can no longer reach a cross-shard
    // destination unchecked -- automatically closed by the same ordering fix covered from the same-shard side
    // in ZoneMoveServiceTests. ---

    [Fact]
    public async Task ReviveHackFlagged_FactionMismatch_IsKickedBeforeEverReachingTheCrossShardHandoff()
    {
        // CreateService's default EnterData tribe (1) already mismatches SourceMapId=2's owning faction (0) --
        // exactly the setup that would have leaked through the OLD ordering into HandleCrossShardAsync
        // unchecked, since the revive-hack check used to run only after a live destination shard was found.
        var (service, session, sourceZone, tickets) = CreateService(
            new Dictionary<byte, short[]> { [DestinationShardId] = [TargetMapId] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.2", 11001, 0, 100, 5f));
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        state!.ReviveHackFlag = true;

        await service.HandleAsync(Request(SourceMapId, TargetMapId), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);
    }

    [Fact]
    public async Task ReviveHackFlagged_DestinationZone38_IsExempt_StillReachesTheCrossShardHandoff()
    {
        var (service, session, sourceZone, tickets) = CreateService(
            new Dictionary<byte, short[]> { [DestinationShardId] = [38] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.2", 11001, 0, 100, 5f));
        Assert.True(sourceZone.TryGetPlayer(CharacterId, out var state));
        state!.ReviveHackFlag = true;

        await service.HandleAsync(Request(SourceMapId, 38), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(session.IsCrossShardTransferPending);
        Assert.Single(tickets.CreatedTickets);
    }

    // --- Fix (Finding 19): HandleCrossShardAsync now runs TribeGuardCorridorGate against the live destination
    // shard before minting a ticket -- previously a cross-shard destination bypassed the corridor gate
    // entirely, even though the same-shard branch already ran it. All tests below use a NON-empty catalog
    // (unlike every test above, which relies on TribeGuardCorridorCatalog.Empty's documented always-allow) to
    // actually exercise the gate rather than its no-op default. ---

    private const short CorridorHubZoneId = 100;
    private const byte CorridorOwnerTribe = 0;

    // Owner tribe 0's own chain: 201 (seg0) -> 202 (seg1) -> 203 (seg2) -> 204 (seg3, home). Deliberately
    // disjoint from SourceMapId/TargetMapId so these tests can freely choose their own origin/destination
    // without colliding with the plain-ticket-mint tests above. Every destination used below stays within
    // ZoneMoveService's own valid packet.ZoneNumber range (1-349) -- unlike the origin zone (only checked for
    // equality against PresentZoneNumber, never range-checked), the destination IS range-checked before
    // HandleCrossShardAsync is ever reached, so a corridor zone numbered e.g. 900+ would never survive that
    // earlier gate to exercise this one at all.
    private static TribeGuardCorridorCatalog CreateCorridorCatalog(short segment0OverrideZoneId = 201)
    {
        var chain = ImmutableArray.Create(segment0OverrideZoneId, (short)202, (short)203, (short)204);
        var chains = ImmutableDictionary<byte, TribeGuardCorridorChain>.Empty.Add(CorridorOwnerTribe,
            new TribeGuardCorridorChain(chain));

        return new TribeGuardCorridorCatalog(CorridorHubZoneId, chains,
            ImmutableDictionary<(byte, byte), ImmutableArray<int>>.Empty);
    }

    private static (ZoneMoveService Service, ZoneClientSession Session, Zone SourceZone, FakeDuplexPipe Pipe,
        FakeSessionTicketRepository Tickets) CreateServiceWithCorridor(
            short sourceMapId, byte requesterTribe, short accountGrade, TribeGuardCorridorCatalog corridorCatalog,
            TribeGuardCorridorState corridorState, IReadOnlyDictionary<byte, short[]> hostedMapsByShard,
            params ShardDirectoryEntryDto[] shards)
    {
        var worldData = ZoneTestKit.EmptyWorldData();
        var zones = ZoneTestKit.CreateRegistry(worldData: worldData);
        zones.Initialize([sourceMapId]);

        var worldState = ZoneTestKit.CreateWorldState();
        var tickets = new FakeSessionTicketRepository();
        var options = new GameServerOptions { ShardId = SourceShardId };
        var service = new ZoneMoveService(zones, worldData, new GuildRankingCache(), worldState,
            corridorCatalog, corridorState,
            new FakeGameServerDirectoryRepository(shards),
            new FakeShardMapAssignmentRepository(hostedMapsByShard),
            tickets,
            Options.Create(options), NullLogger<ZoneMoveService>.Instance);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var sessionToken = Guid.NewGuid();
        session.MarkTicketConsumed(1, CharacterId, sessionToken, accountGrade: accountGrade);
        var sourceZone = zones[sourceMapId];
        session.CurrentZone = sourceZone;

        sourceZone.Post(ZoneCommand.Enter(CharacterId,
            ZoneTestKit.EnterData(session, sourceMapId, tribe: requesterTribe)));
        sourceZone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe); // discard the Enter-triggered self/neighbor traffic, if any

        return (service, session, sourceZone, pipe, tickets);
    }

    [Fact]
    public async Task CorridorRejectsSoft_SendsFailureWithDestinationShardsOwnAddress_NoTicketMinted_NoPendingFlag()
    {
        // Origin (2) is not adjacent to segment-1 zone 202 at all (neither the hub nor segment 0) -- an
        // invalid single-step advance, soft-rejected since neither zone involved is 37.
        var catalog = CreateCorridorCatalog();
        var (service, session, _, pipe, tickets) = CreateServiceWithCorridor(
            2, requesterTribe: 1, accountGrade: 0, catalog, new TribeGuardCorridorState(),
            new Dictionary<byte, short[]> { [DestinationShardId] = [202] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(2, 202), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = new byte[FrameWriter.FrameSizeOf<ZoneMoveResponse>() +
                                 FrameWriter.FrameSizeOf<ReturnToHomeZoneResponse>()];
        var moveResponseSize = FrameWriter.WriteFrame(
            new ZoneMoveResponse { Result = 1, Ip = "10.0.0.9", Port = 11009 }, expected);
        FrameWriter.WriteFrame(new ReturnToHomeZoneResponse(), expected.AsSpan(moveResponseSize));
        Assert.Equal(expected, sent);
    }

    [Fact]
    public async Task CorridorHardDisconnect_OriginIsZone37_AbortsSession_NoTicketMinted_NothingSent()
    {
        var catalog = CreateCorridorCatalog(); // segment0 = 201, unrelated to 37
        var (service, session, _, pipe, tickets) = CreateServiceWithCorridor(
            37, requesterTribe: 1, accountGrade: 0, catalog, new TribeGuardCorridorState(),
            new Dictionary<byte, short[]> { [DestinationShardId] = [202] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(37, 202), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task CorridorHardDisconnect_DestinationIsZone37_AbortsSession_NoTicketMinted()
    {
        var catalog = CreateCorridorCatalog(segment0OverrideZoneId: 37); // segment0's own zone is (contrived) 37
        var (service, session, _, pipe, tickets) = CreateServiceWithCorridor(
            2, requesterTribe: 1, accountGrade: 0, catalog, new TribeGuardCorridorState(),
            new Dictionary<byte, short[]> { [DestinationShardId] = [37] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(2, 37), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public async Task CorridorAllows_OwningTribeBypass_MintsTicket_DespiteBadAdjacency()
    {
        // Owning tribe (0) itself, moving from an entirely unrelated origin straight into segment 1 -- would
        // fail adjacency if evaluated, but the owning-tribe bypass short-circuits before that check.
        var catalog = CreateCorridorCatalog();
        var (service, session, _, _, tickets) = CreateServiceWithCorridor(
            2, requesterTribe: CorridorOwnerTribe, accountGrade: 0, catalog, new TribeGuardCorridorState(),
            new Dictionary<byte, short[]> { [DestinationShardId] = [202] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(2, 202), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(session.IsCrossShardTransferPending);
        Assert.Single(tickets.CreatedTickets);
    }

    [Fact]
    public async Task CorridorWouldReject_ButGmRank_BypassesTheGate_MintsTicketDespiteBadTribeAndAdjacency()
    {
        var catalog = CreateCorridorCatalog();
        var (service, session, _, _, tickets) = CreateServiceWithCorridor(
            2, requesterTribe: 1, accountGrade: 1, catalog, new TribeGuardCorridorState(),
            new Dictionary<byte, short[]> { [DestinationShardId] = [202] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(2, 202), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(session.IsCrossShardTransferPending);
        Assert.Single(tickets.CreatedTickets);
    }

    [Fact]
    public async Task CorridorAllows_ValidSingleStepAdvance_OpenSegment_MintsTicket()
    {
        var catalog = CreateCorridorCatalog();
        var state = new TribeGuardCorridorState();
        state.TrySetOpen(CorridorOwnerTribe, 1, true); // segment gating entry into zone 202 (chain[1])
        var (service, session, _, _, tickets) = CreateServiceWithCorridor(
            201, requesterTribe: 1, accountGrade: 0, catalog, state,
            new Dictionary<byte, short[]> { [DestinationShardId] = [202] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(201, 202), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.True(session.IsCrossShardTransferPending);
        Assert.Single(tickets.CreatedTickets);
    }

    [Fact]
    public async Task CorridorRejectsSoft_ValidSingleStepAdvance_ClosedSegment_NoTicketMinted()
    {
        var catalog = CreateCorridorCatalog();
        var (service, session, _, _, tickets) = CreateServiceWithCorridor(
            201, requesterTribe: 1, accountGrade: 0, catalog, new TribeGuardCorridorState(), // closed by default
            new Dictionary<byte, short[]> { [DestinationShardId] = [202] },
            new ShardDirectoryEntryDto(DestinationShardId, "10.0.0.9", 11009, 0, 100, 5f));

        await service.HandleAsync(Request(201, 202), session, CancellationToken.None);

        Assert.Null(session.DisconnectReason);
        Assert.False(session.IsCrossShardTransferPending);
        Assert.Empty(tickets.CreatedTickets);
    }
}
