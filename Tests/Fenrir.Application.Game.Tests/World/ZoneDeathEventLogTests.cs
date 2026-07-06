using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the <c>game.EventLog</c> (Category=Death) wiring added to <see cref="Zone.ApplyDeath" /> /
///     <c>Zone.ApplyDeathExperienceLoss</c>: every death queues a <see cref="Zone.PendingDeathEventLog" />
///     row (drained by <c>DeathEventLogFlushHost</c>, covered separately in
///     <c>DeathEventLogFlushHostTests</c>), and a monster-kill death that actually costs XP or CP queues a
///     second row for that consequence.
/// </summary>
public class ZoneDeathEventLogTests
{
    private const short TestLevel = 50;
    private const int TestExpRangeMin = 1000;

    private static FrozenDictionary<short, LevelRowDto> TestLevels()
    {
        var dict = new Dictionary<short, LevelRowDto>
        {
            [TestLevel] = new(TestLevel, TestExpRangeMin, 99_999, 0, 0, 0, 0, 0, 0, 0, 0)
        };
        return dict.ToFrozenDictionary();
    }

    [Fact]
    public void ApplyDeath_QueuesACharacterDeathRow_RegardlessOfCause()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.PlayerKill);

        var entries = zone.DrainPendingDeathEventLogs();
        var row = Assert.Single(entries);
        Assert.Equal(10, row.ActorCharacterId);
        Assert.Equal((byte)DeathCause.PlayerKill, row.Outcome);
        Assert.NotNull(row.Payload);
        Assert.Contains("Cause=PlayerKill", row.Payload);
        Assert.Contains("Level=5", row.Payload);
    }

    [Fact]
    public void ApplyDeath_DefaultUnknownCause_StillQueuesARow()
    {
        // Covers a GM-forced kill, which calls ApplyDeath with no cause argument.
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        var entries = zone.DrainPendingDeathEventLogs();
        var row = Assert.Single(entries);
        Assert.Equal((byte)DeathCause.Unknown, row.Outcome);
    }

    [Fact]
    public void ApplyDeath_AlreadyDead_DoesNotQueueASecondRow()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);
        zone.ApplyDeath(10); // duplicate killing blow -- must not queue a second row

        var entries = zone.DrainPendingDeathEventLogs();
        Assert.Single(entries);
    }

    [Fact]
    public void ApplyDeath_UnknownCharacter_QueuesNothing()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.ApplyDeath(999);

        Assert.Empty(zone.DrainPendingDeathEventLogs());
    }

    [Fact]
    public void ApplyDeath_MonsterKill_BelowMinimumLevel_QueuesOnlyTheDeathRow()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        // Below ExperienceFormulas.MinimumLevelForDeathExperienceLoss (10) -- no XP-loss row.
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 9)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.MonsterKill);

        var entries = zone.DrainPendingDeathEventLogs();
        Assert.Single(entries);
    }

    [Fact]
    public void ApplyDeath_MonsterKill_WithinRange_AlsoQueuesAnExperienceLossRow()
    {
        var worldData = ZoneTestKit.EmptyWorldData(levelsByLevel: TestLevels());
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Victim", 1, 0, 2, 3, TestLevel,
            1, 0f, 0f, 0f, 0f,
            1, 1, 1, 1, 1,
            Experience: 3000); // loss = (3000-1000)*0.05 = 100
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.MonsterKill);

        var entries = zone.DrainPendingDeathEventLogs();
        Assert.Equal(2, entries.Count);

        var deathRow = entries[0];
        Assert.Equal((byte)DeathCause.MonsterKill, deathRow.Outcome);

        var lossRow = entries[1];
        Assert.Equal(10, lossRow.ActorCharacterId);
        Assert.Equal((byte)0, lossRow.Outcome); // ExperienceLossOutcome
        Assert.NotNull(lossRow.Payload);
        Assert.Contains("Kind=Experience", lossRow.Payload);
        Assert.Contains("Loss=100", lossRow.Payload);

        zone.TryGetPlayer(10, out var victim);
        Assert.Equal(2900, victim!.Experience);
    }

    [Fact]
    public void ApplyDeath_MonsterKill_AtLevelCap_QueuesAContributionPointsLossRowInstead()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Victim", 1, 0, 2, 3, ExperienceFormulas.MaxLimitLevel,
            1, 0f, 0f, 0f, 0f,
            1, 1, 1, 1, 1,
            ContributionPoints: 50);
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10, DeathCause.MonsterKill);

        var entries = zone.DrainPendingDeathEventLogs();
        Assert.Equal(2, entries.Count);

        var lossRow = entries[1];
        Assert.Equal((byte)1, lossRow.Outcome); // ContributionPointsLossOutcome
        Assert.NotNull(lossRow.Payload);
        Assert.Contains("Kind=ContributionPoints", lossRow.Payload);
        Assert.Contains($"Loss={ExperienceFormulas.CpLossAtLevelCap}", lossRow.Payload);

        zone.TryGetPlayer(10, out var victim);
        Assert.Equal(50 - ExperienceFormulas.CpLossAtLevelCap, victim!.ContributionPoints);
    }

    [Fact]
    public void DrainPendingDeathEventLogs_ClearsTheQueue()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        Assert.Single(zone.DrainPendingDeathEventLogs());
        Assert.Empty(zone.DrainPendingDeathEventLogs());
    }

    [Fact]
    public async Task WaitForDeathEventLogAsync_ResolvesAssoonAsARowIsQueued()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, level: 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.ApplyDeath(10);

        var wait = zone.WaitForDeathEventLogAsync(CancellationToken.None);
        var completed = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(wait, completed);
    }
}
