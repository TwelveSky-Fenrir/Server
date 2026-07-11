using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneHolyStoneBattleRankResetTests
{

        private const int RankPointClearedStatSort = 66;

        private const int RankPointDateResetStatSort = 67;

        private const int RankBuffClearedStatSort = 68;

    private static byte[] ExpectedFrames(params ReadOnlySpan<AvatarStatUpdateResponse> packets)
    {
        var frameSize = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var buffer = new byte[frameSize * packets.Length];
        for (var i = 0; i < packets.Length; i++)
            FrameWriter.WriteFrame(in packets[i], buffer.AsSpan(i * frameSize, frameSize));
        return buffer;
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) SetUp(int characterId = 10)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public void MainBranch_RankPointZero_BuffInactive_OnlySendsAlwaysOnDateNotification()
    {
        var (zone, state, pipe) = SetUp();
        Assert.Equal(0, state.RankPoint);
        Assert.Equal(0, state.RankBuffType);

        Assert.True(zone.PostHolyStoneBattleRankReset());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var today = GameDate.Today();
        Assert.Equal(today, state.RankPointDate);
        Assert.Equal(0, state.RankPoint);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(
            new AvatarStatUpdateResponse { Sort = RankPointDateResetStatSort, Value = today, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public void MainBranch_RankPointAboveZero_SendsClearNotificationBeforeDateNotification_AndZeroesIt()
    {
        var (zone, state, pipe) = SetUp();
        state.RankPoint = 250;

        Assert.True(zone.PostHolyStoneBattleRankReset());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var today = GameDate.Today();
        Assert.Equal(0, state.RankPoint);
        Assert.Equal(today, state.RankPointDate);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(
            new AvatarStatUpdateResponse { Sort = RankPointClearedStatSort, Value = 0, Value2 = 0 },
            new AvatarStatUpdateResponse { Sort = RankPointDateResetStatSort, Value = today, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public void MainBranch_RankBuffActive_ClearsTier_SendsClearNotification_AndRecomputesStats()
    {
        var (zone, state, pipe) = SetUp();
        state.RankBuffType = 3;

        Assert.True(zone.PostHolyStoneBattleRankReset());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.RankBuffType);

        var today = GameDate.Today();
        var expectedPrefix = ExpectedFrames(
            new AvatarStatUpdateResponse { Sort = RankPointDateResetStatSort, Value = today, Value2 = 0 },
            new AvatarStatUpdateResponse { Sort = RankBuffClearedStatSort, Value = 0, Value2 = 0 });

        var sent = ZoneTestKit.DrainOutbound(pipe);
        Assert.True(sent.Length > expectedPrefix.Length);
        Assert.Equal(expectedPrefix, sent[..expectedPrefix.Length]);
    }

    [Fact]
    public void TransferringPlayer_DateNotAlreadyToday_ClearsFieldsSilently_WithNoNotifications()
    {
        var (zone, state, pipe) = SetUp();
        state.IsMovingZone = true;
        state.RankPoint = 100;
        state.RankBuffType = 4;
        state.RankPointDate = 0;

        Assert.True(zone.PostHolyStoneBattleRankReset());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var today = GameDate.Today();
        Assert.Equal(today, state.RankPointDate);
        Assert.Equal(0, state.RankPoint);
        Assert.Equal(0, state.RankBuffType);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void TransferringPlayer_DateAlreadyToday_LeavesEverythingUntouched()
    {
        var (zone, state, pipe) = SetUp();
        var today = GameDate.Today();
        state.IsMovingZone = true;
        state.RankPointDate = today;
        state.RankPoint = 77;
        state.RankBuffType = 2;

        Assert.True(zone.PostHolyStoneBattleRankReset());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(today, state.RankPointDate);
        Assert.Equal(77, state.RankPoint);
        Assert.Equal(2, state.RankBuffType);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void ServerWide_ResetsEveryConnectedPlayerOnTheZone_NotJustOne()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (sessionA, pipeA) = ZoneTestKit.CreateSession(10);
        var (sessionB, pipeB) = ZoneTestKit.CreateSession(11);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(sessionA, 1)));
        zone.Post(ZoneCommand.Enter(11, ZoneTestKit.EnterData(sessionB, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        Assert.True(zone.PostHolyStoneBattleRankReset());
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var today = GameDate.Today();
        Assert.True(zone.TryGetPlayer(10, out var stateA));
        Assert.True(zone.TryGetPlayer(11, out var stateB));
        Assert.Equal(today, stateA!.RankPointDate);
        Assert.Equal(today, stateB!.RankPointDate);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipeA));
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipeB));
    }
}
