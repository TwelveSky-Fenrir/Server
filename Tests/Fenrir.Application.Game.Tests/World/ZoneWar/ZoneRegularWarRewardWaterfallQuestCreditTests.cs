using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class ZoneRegularWarRewardWaterfallQuestCreditTests
{
    private const short MapId = 49;

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) SetUp(int questKillCounter = 0)
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var enterData = ZoneTestKit.EnterData(session, MapId);
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var state));
        state!.QuestActiveFlag = 1;
        state.QuestSort = 8;
        state.QuestTargetPhase = MapId;
        state.QuestKillCounter = questKillCounter;
        ZoneTestKit.DrainOutbound(pipe);

        return (zone, state, pipe);
    }

    private static RegularWarRewardGrant Grant()
    {
        return new RegularWarRewardGrant(1, true, 0, 0, 0, 0, 0, false);
    }

    [Fact]
    public void QuestHolder_WithMatchingZoneTarget_AdvancesKillCounter_AndPushesMarkerNine()
    {
        var (zone, state, pipe) = SetUp();

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant()));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);

        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(9, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public void QuestHolder_AlreadyAtCap_DoesNotDoubleCreditOrPushAgain()
    {
        var (zone, state, pipe) = SetUp(questKillCounter: 1);

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant()));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void HidingCharacter_IsNotCredited()
    {
        var (zone, state, pipe) = SetUp();
        state.VisibleState = 0;

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant()));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void MidZoneTransferCharacter_IsNotCredited()
    {
        var (zone, state, pipe) = SetUp();
        state.IsMovingZone = true;

        zone.Post(ZoneCommand.ApplyRegularWarReward(Grant()));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
