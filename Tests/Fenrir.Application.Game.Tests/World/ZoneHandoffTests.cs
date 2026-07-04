using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>Covers the in-process map-transfer mechanism (<see cref="ZoneTransfer" />): live state travels inside the <c>Leave</c>/<c>Enter</c> commands, never observable in two zones at once.</summary>
public class ZoneHandoffTests
{
    [Fact]
    public void Leave_WithHandoffTarget_RemovesFromSourceImmediately_ButTargetOnlyLearnsOnItsOwnTick()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(source.TryGetPlayer(10, out _));

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(source.TryGetPlayer(10, out _));

        // Enter was posted to the target but not yet drained -- the state belongs to no zone in between
        Assert.False(target.TryGetPlayer(10, out _));

        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out _));
    }

    [Fact]
    public void Leave_WithHandoffTarget_StateArrivesIntactAtTheTarget()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1,
            "Hero", 123f, 4f, 456f)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(source.TryGetPlayer(10, out var before));

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var after));
        Assert.Equal((short)2, after!.MapId);
        Assert.Equal(before!.Name, after.Name);
        Assert.Equal(before.PosX, after.PosX);
        Assert.Equal(before.PosY, after.PosY);
        Assert.Equal(before.PosZ, after.PosZ);
        Assert.Equal(before.Heading, after.Heading);
        Assert.Equal(before.Life, after.Life);
        Assert.Equal(before.MaxLife, after.MaxLife);
        Assert.Equal(before.Mana, after.Mana);
        Assert.Equal(before.MaxMana, after.MaxMana);
        Assert.Equal(before.Tribe, after.Tribe);
        Assert.Equal(before.Gender, after.Gender);
        Assert.Equal(before.HeadType, after.HeadType);
        Assert.Equal(before.FaceType, after.FaceType);
        Assert.Equal(before.Level, after.Level);
        Assert.Same(session, after.Session);

        // bumped by exactly one so the map change wins usp_Character_PersistBatch's strictly-greater idempotence guard
        Assert.Equal(before.FlushSequence + 1, after.FlushSequence);
    }

    [Fact]
    public void Leave_WithHandoffTarget_RepointsTheSessionsCurrentZone()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        session.CurrentZone = source;

        source.Post(ZoneCommand.Leave(10, target));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Same(target, session.CurrentZone);
    }

    [Fact]
    public void Request_UsesZoneTransfer_ToPerformTheSameHandoff()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(ZoneTransfer.Request(source, target, 10));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(source.TryGetPlayer(10, out _));
        Assert.True(target.TryGetPlayer(10, out _));
    }

    [Fact]
    public void Leave_WithHandoffPosition_LandsAtTheOverriddenPosition_NotWhereverThePlayerWasStanding()
    {
        // arrival point travels inside the Leave command itself, never by mutating PlayerRuntimeState directly
        var dirtyTracker = new DirtyTracker<int>();
        var source = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var target = ZoneTestKit.CreateZone(2, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);

        source.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 10f, posY: 0f, posZ: 10f)));
        source.Tick(TimeSpan.FromMilliseconds(50));

        source.Post(ZoneCommand.Leave(10, target, (500f, 7f, 900f)));
        source.Tick(TimeSpan.FromMilliseconds(50));
        target.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(target.TryGetPlayer(10, out var arrived));
        Assert.Equal(500f, arrived!.PosX);
        Assert.Equal(7f, arrived.PosY);
        Assert.Equal(900f, arrived.PosZ);
    }

    [Fact]
    public void Leave_WithoutHandoffTarget_IsAPlainDisconnect_PlayerEndsUpInNoZone()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Leave(10));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(10, out _));
        Assert.Equal(0, zone.PlayerCount);
    }
}
