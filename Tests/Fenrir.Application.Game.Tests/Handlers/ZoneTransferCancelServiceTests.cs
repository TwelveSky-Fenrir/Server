using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

public class ZoneTransferCancelServiceTests
{
    private const int CharacterId = 10;
    private const short MapId = 2;
    private const byte ShardId = 1;

    private static (ZoneTransferCancelService Service, ZoneClientSession Session, Zone Zone,
        FakeCharacterShardLocationRepository ShardLocations) CreateService(bool markPending)
    {
        var shardLocations = new FakeCharacterShardLocationRepository();
        var options = new GameServerOptions { ShardId = ShardId };
        var zone = ZoneTestKit.CreateZone(MapId, options, characterShardLocations: shardLocations);

        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId, Guid.NewGuid());
        session.CurrentZone = zone;

        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        if (markPending)
        {
            session.MarkCrossShardTransferPending();
            zone.Post(ZoneCommand.MarkZoneTransferPending(CharacterId));
            zone.Tick(TimeSpan.FromMilliseconds(50));
        }

        var service = new ZoneTransferCancelService(shardLocations, Options.Create(options),
            NullLogger<ZoneTransferCancelService>.Instance);

        return (service, session, zone, shardLocations);
    }

    [Fact]
    public async Task NotPending_AbortsSessionWithStateViolation_AndNeverCallsTheBroker()
    {
        var (service, session, _, shardLocations) = CreateService(markPending: false);

        await service.HandleAsync(session, CancellationToken.None);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
        Assert.Empty(shardLocations.UpsertCalls);
    }

    [Fact]
    public async Task Pending_ClearsTheFlag_ClearsTheSessionCrossShardFlag_AndRefreshesTheShardLocation()
    {
        var (service, session, zone, shardLocations) = CreateService(markPending: true);
        Assert.True(zone.TryGetPlayer(CharacterId, out var pendingState));
        var registeredAtBeforeCancel = pendingState!.ZoneTransferRegisteredAtUtc;

        await service.HandleAsync(session, CancellationToken.None);

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.False(session.IsCrossShardTransferPending);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.False(state!.IsMovingZone);
        Assert.Single(shardLocations.UpsertCalls);
        Assert.Equal((CharacterId, ShardId, MapId, "Hero", (byte)1), shardLocations.UpsertCalls[0]);
        Assert.True(state.ZoneTransferRegisteredAtUtc >= registeredAtBeforeCancel);
    }

    [Fact]
    public async Task Pending_BrokerRefreshThrows_AbortsSessionWithProcessingFault_AndNeverRefreshesTheTimestamp()
    {
        var (service, session, zone, shardLocations) = CreateService(markPending: true);
        shardLocations.ThrowOnUpsert = true;
        Assert.True(zone.TryGetPlayer(CharacterId, out var pendingState));
        var registeredAtBeforeCancel = pendingState!.ZoneTransferRegisteredAtUtc;

        await service.HandleAsync(session, CancellationToken.None);

        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(DisconnectReason.ProcessingFault, session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        Assert.False(state!.IsMovingZone);
        Assert.Equal(registeredAtBeforeCancel, state.ZoneTransferRegisteredAtUtc);
    }
}
