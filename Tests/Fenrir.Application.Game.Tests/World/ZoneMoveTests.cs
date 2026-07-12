using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneMoveTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    private static ActionInfo MoveTo(float x, float z)
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = 2,
            Frame = 0,
            Location = [x, 0f, z],
            TargetLocation = [x, 0f, z],
            Front = 0f,
            TargetFront = 0f,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
    }

    [Fact]
    public void Move_Plausible_UpdatesPositionAndBroadcastsToNeighborsAndSelfEcho()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);
        var (neighbor, neighborPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(neighbor, 1, posX: 12f, posZ: 12f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);
        ZoneTestKit.DrainOutbound(neighborPipe);

        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10.5f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);

        var moverInbox = ZoneTestKit.DrainOutbound(moverPipe);
        Assert.Equal(OneFrame, moverInbox.Length);

        var neighborInbox = ZoneTestKit.DrainOutbound(neighborPipe);
        Assert.Equal(OneFrame, neighborInbox.Length);
    }

    [Fact]
    public void MoveResume_Plausible_UpdatesPositionSilently_NoSelfEcho_NoNeighborBroadcast()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);
        var (neighbor, neighborPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(neighbor, 1, posX: 12f, posZ: 12f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);
        ZoneTestKit.DrainOutbound(neighborPipe);

        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10.5f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);

        Assert.Empty(ZoneTestKit.DrainOutbound(moverPipe));
        Assert.Empty(ZoneTestKit.DrainOutbound(neighborPipe));
    }

    [Fact]
    public void MoveResume_SkillGradeExceedsServerCap_DropsPacketSilently_NoMutation_NoDisconnect()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        var action = MoveTo(10.5f, 10f) with { Sort = 2, SkillNumber = 50, SkillGradeNum1 = 1 };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);
        Assert.Equal(0, mover10.ActionSkillNumber);

        Assert.Empty(ZoneTestKit.DrainOutbound(moverPipe));
        Assert.Null(mover.DisconnectReason);
    }

    [Fact]
    public void MoveResume_PartyBuffSkillGradeMismatch_IsExemptFromGradeCap_StillAppliesMutation()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        var action = MoveTo(10.5f, 10f) with { Sort = 64, SkillNumber = 76, SkillGradeNum1 = 99 };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10.5f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);
        Assert.Equal(76, mover10.ActionSkillNumber);

        Assert.Empty(ZoneTestKit.DrainOutbound(moverPipe));
        Assert.Null(mover.DisconnectReason);
    }

    [Fact]
    public void MoveResume_ImplausibleDistance_StillAppliesMutationSilently_NoCorrectionSent()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        zone.Post(ZoneCommand.Move(10, MoveTo(999_999f, 999_999f), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(999_999f, mover10!.PosX);
        Assert.Equal(999_999f, mover10.PosZ);

        Assert.Empty(ZoneTestKit.DrainOutbound(moverPipe));
        Assert.Null(mover.DisconnectReason);
    }

    [Fact]
    public void MoveResume_Accepted_RecordsDefenseHackPreviousPositionFromPriorState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        zone.Post(ZoneCommand.Move(10, MoveTo(50f, 60f), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10f, mover10!.DefenseHackPreviousPosX);
        Assert.Equal(20f, mover10.DefenseHackPreviousPosZ);
        Assert.Equal(50f, mover10.PosX);
        Assert.Equal(60f, mover10.PosZ);
    }

    [Fact]
    public void MoveResume_SkillGradeExceedsServerCap_StillRecordsDefenseHackPreviousPosition()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 20f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        var action = MoveTo(50f, 60f) with { Sort = 2, SkillNumber = 50, SkillGradeNum1 = 1 };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10f, mover10!.DefenseHackPreviousPosX);
        Assert.Equal(20f, mover10.DefenseHackPreviousPosZ);
        Assert.Equal(10f, mover10.PosX);
        Assert.Equal(20f, mover10.PosZ);
    }

    [Fact]
    public void MoveResume_StunActionSort_ResetsAfkTick()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        Assert.True(zone.TryGetPlayer(10, out var moverState));
        moverState!.AfkTick = 42;

        var action = MoveTo(10f, 10f) with { Sort = 11 };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, moverState.AfkTick);
    }

    [Theory]
    [InlineData(94)]
    [InlineData(95)]
    public void MoveResume_FishingResultActionSort_ClearsFishingProgressFlag(int sort)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        Assert.True(zone.TryGetPlayer(10, out var moverState));
        moverState!.FishingState = 1;

        var action = MoveTo(10f, 10f) with { Sort = sort };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, moverState.FishingState);
    }

    [Fact]
    public void MoveResume_FishingBiteActionSort93_DoesNotClearFishingProgressFlag()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        Assert.True(zone.TryGetPlayer(10, out var moverState));
        moverState!.FishingState = 1;

        var action = MoveTo(10f, 10f) with { Sort = 93 };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, moverState.FishingState);
    }

    [Fact]
    public void MoveResume_TypeOutsideLegacyByteRange_IsStillAcceptedNotDisconnected()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        var action = MoveTo(15f, 15f) with { Sort = 2, Type = 8 };
        zone.Post(ZoneCommand.Move(10, action, true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(15f, mover10!.PosX);
        Assert.Equal(15f, mover10.PosZ);
        Assert.Null(mover.DisconnectReason);
    }

    [Fact]
    public void Move_Implausible_CommitsUnconditionally_NoRejectionNoResync()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        zone.Post(ZoneCommand.Move(10, MoveTo(999_999f, 999_999f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(999_999f, mover10!.PosX);
        Assert.Equal(999_999f, mover10.PosZ);

        var moverInbox = ZoneTestKit.DrainOutbound(moverPipe);
        Assert.Equal(OneFrame, moverInbox.Length);
        Assert.Null(mover.DisconnectReason);
    }

    [Fact]
    public void Move_UnknownCharacter_IsIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.Post(ZoneCommand.Move(999, MoveTo(1f, 1f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }

    [Fact]
    public void Move_CasterHiding_SuppressesNeighborBroadcast_ButStillSelfEchoes()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);
        var (neighbor, neighborPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(neighbor, 1, posX: 12f, posZ: 12f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);
        ZoneTestKit.DrainOutbound(neighborPipe);

        Assert.True(zone.TryGetPlayer(10, out var moverState));
        moverState!.VisibleState = 0;

        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var moverInbox = ZoneTestKit.DrainOutbound(moverPipe);
        Assert.Equal(OneFrame, moverInbox.Length);

        Assert.Empty(ZoneTestKit.DrainOutbound(neighborPipe));
    }

    [Fact]
    public void Move_CasterInDifferentDungeonInstanceThanNeighbor_ExcludesNeighborFromBroadcast()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);
        var (neighbor, neighborPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(neighbor, 1, posX: 12f, posZ: 12f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);
        ZoneTestKit.DrainOutbound(neighborPipe);

        Assert.True(zone.TryGetPlayer(10, out var moverState));
        Assert.True(zone.TryGetPlayer(20, out var neighborState));
        moverState!.DungeonInstanceId = 1;
        neighborState!.DungeonInstanceId = 2;

        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var moverInbox = ZoneTestKit.DrainOutbound(moverPipe);
        Assert.Equal(OneFrame, moverInbox.Length);

        Assert.Empty(ZoneTestKit.DrainOutbound(neighborPipe));
    }

    [Fact]
    public void Move_CasterAndNeighborInSameDungeonInstance_StillBroadcasts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);
        var (neighbor, neighborPipe) = ZoneTestKit.CreateSession(2);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(neighbor, 1, posX: 12f, posZ: 12f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);
        ZoneTestKit.DrainOutbound(neighborPipe);

        Assert.True(zone.TryGetPlayer(10, out var moverState));
        Assert.True(zone.TryGetPlayer(20, out var neighborState));
        moverState!.DungeonInstanceId = 7;
        neighborState!.DungeonInstanceId = 7;

        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var neighborInbox = ZoneTestKit.DrainOutbound(neighborPipe);
        Assert.Equal(OneFrame, neighborInbox.Length);
    }
}
