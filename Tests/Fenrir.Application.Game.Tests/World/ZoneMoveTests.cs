using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone" />'s Move command path: position update, <c>MovementRules</c> plausibility gate, AOI
///     tracking, and broadcast/resync split.
/// </summary>
public class ZoneMoveTests
{
    private static readonly int OneFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    private static ActionInfo MoveTo(float x, float z)
    {
        return new ActionInfo
        {
            Type = 0,
            // Sort 2 ("plain movement"), NOT 0 -- Sort 0 is RestActionSort, and an accepted Sort-0 action now
            // legitimately triggers Zone.PlayerLifecycle.cs's ApplyRestActionProtectionAndHeal (legacy-parity
            // "rest"/stand-up side effect: an unconditional HP heal + a self-only AvatarStatUpdateResponse),
            // which would make this suite's "self excluded from a plain move" assertions fail for the wrong
            // reason. Sort 2 is legal for every Type (CharacterMotionWhitelist) and carries no such side effect.
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

        // MovementRules measures DateTime.UtcNow, not the zone's simulated clock -- SlackUnits=1 covers this tiny step regardless
        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10.5f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);

        // op15 (CZ_AVATAR_ACTION_SEND, isResumeAction: false) accepted-action tail echoes the just-accepted
        // action back to the mover itself, in addition to the neighbor broadcast -- Server/ts25zone/
        // S04_MyWork02.cpp:1770-1777's unconditional self-send, previously missing here (see
        // MoveResume_Plausible_BroadcastsToNeighbors_NoSelfEcho below for the op16 contrast, which has no
        // self-send at all).
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

        // op16 (CZ_UPDATE_AVATAR_ACTION, isResumeAction: true) has no broadcast call anywhere in its legacy
        // body (Server/ts25zone/S04_MyWork02.cpp:1789-1922 -- verified directly: the handler only calls
        // SetSpaceIndex()/UPDATE_LOGOUT_INFO, never mTRANSFER.B_AVATAR_ACTION_RECV) -- neither the acting
        // client nor its neighbors ever see this update over the wire; the state change is applied
        // server-side only, matching legacy's documented server-knows/clients-don't-know staleness gap for
        // this opcode.
        zone.Post(ZoneCommand.Move(10, MoveTo(10.5f, 10f), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10.5f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);

        Assert.Empty(ZoneTestKit.DrainOutbound(moverPipe));
        Assert.Empty(ZoneTestKit.DrainOutbound(neighborPipe));
    }

    [Fact]
    public void Move_Implausible_RejectsUpdate_AndResyncsMoverToLastKnownGoodState()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (mover, moverPipe) = ZoneTestKit.CreateSession(1);

        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(mover, 1, posX: 10f, posZ: 10f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(moverPipe);

        zone.Post(ZoneCommand.Move(10, MoveTo(999_999f, 999_999f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var mover10));
        Assert.Equal(10f, mover10!.PosX);
        Assert.Equal(10f, mover10.PosZ);

        // only the mover gets a direct resync frame with its last-known-good position
        var moverInbox = ZoneTestKit.DrainOutbound(moverPipe);
        Assert.Equal(OneFrame, moverInbox.Length);
    }

    [Fact]
    public void Move_UnknownCharacter_IsIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.Post(ZoneCommand.Move(999, MoveTo(1f, 1f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
