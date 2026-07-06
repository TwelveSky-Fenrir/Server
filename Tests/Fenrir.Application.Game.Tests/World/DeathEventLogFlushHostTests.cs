using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="DeathEventLogFlushHost" />'s drain/persist loop end-to-end against a real
///     <see cref="Zone" />/<see cref="ZoneRegistry" /> pair and a <see cref="FakeEventLogRepository" /> --
///     the write-behind twin of <see cref="ZoneDeathEventLogTests" />, which covers only the in-memory
///     queueing side (<see cref="Zone.PendingDeathEventLog" />) without a repository.
/// </summary>
public class DeathEventLogFlushHostTests
{
    [Fact]
    public async Task FlushAllZonesAsync_PersistsAQueuedDeathRowViaLogAsync()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);
        Assert.True(registry.TryGet(1, out var zone));

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone!.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.PlayerKill);

        var fakeRepo = new FakeEventLogRepository();
        var host = new DeathEventLogFlushHost(registry, fakeRepo, NullLogger<DeathEventLogFlushHost>.Instance);

        await host.FlushAllZonesAsync(CancellationToken.None);

        var logged = Assert.Single(fakeRepo.LoggedEvents);
        Assert.Equal(EventLogCategory.Death, logged.Category);
        Assert.Equal(10, logged.ActorCharacterId);
        Assert.Null(logged.TargetCharacterId);
        Assert.Equal((byte)DeathCause.PlayerKill, logged.Outcome);
        Assert.NotNull(logged.Payload);
        Assert.Contains("Cause=PlayerKill", logged.Payload);

        // Drained -- a second flush with nothing new queued must not log again.
        await host.FlushAllZonesAsync(CancellationToken.None);
        Assert.Single(fakeRepo.LoggedEvents);
    }

    [Fact]
    public async Task FlushAllZonesAsync_NothingQueued_DoesNotCallTheRepository()
    {
        var registry = ZoneTestKit.CreateRegistry();
        registry.Initialize([1]);

        var fakeRepo = new FakeEventLogRepository();
        var host = new DeathEventLogFlushHost(registry, fakeRepo, NullLogger<DeathEventLogFlushHost>.Instance);

        await host.FlushAllZonesAsync(CancellationToken.None);

        Assert.Empty(fakeRepo.LoggedEvents);
    }
}
