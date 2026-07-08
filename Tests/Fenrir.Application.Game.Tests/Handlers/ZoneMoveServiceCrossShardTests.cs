using System.Collections.Frozen;
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
}
