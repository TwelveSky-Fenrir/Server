using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     Covers <see cref="AvatarActionService" />'s <c>mProtect_ReviveHack</c> companion check
///     (S04_MyWork02.cpp:1313-1320): a stand-up-from-death action request (action-sort 30) while still
///     flagged from an unresolved death kicks the session outright instead of being silently denied/forwarded.
/// </summary>
public class AvatarActionServiceTests
{
    private const int CharacterId = 10;

    private static ActionInfo Action(int sort)
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = sort,
            Frame = 0,
            Location = new float[3],
            TargetLocation = new float[3],
            Front = 0,
            TargetFront = 0,
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

    private static (Zone Zone, ZoneClientSession Session) SetUp(bool reviveHackFlag)
    {
        var zone = ZoneTestKit.CreateZone(999);
        var (session, _) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, CharacterId);
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, 999)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        if (reviveHackFlag)
        {
            Assert.True(zone.TryGetPlayer(CharacterId, out var state));
            state!.ReviveHackFlag = true;
        }

        return (zone, session);
    }

    [Fact]
    public void StandUpAction_WhileFlagged_KicksTheSession_AndDoesNotPostTheMove()
    {
        var (zone, session) = SetUp(true);
        var service = new AvatarActionService(NullLogger<AvatarActionService>.Instance);

        var action = Action(30);
        service.PostAction(zone, CharacterId, in action);

        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public void StandUpAction_WhileNotFlagged_IsForwardedNormally()
    {
        var (zone, session) = SetUp(false);
        var service = new AvatarActionService(NullLogger<AvatarActionService>.Instance);

        var action = Action(30);
        service.PostAction(zone, CharacterId, in action);

        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public void OrdinaryMoveAction_WhileFlagged_IsNotKicked_OnlyStandUpSortIsGated()
    {
        var (zone, session) = SetUp(true);
        var service = new AvatarActionService(NullLogger<AvatarActionService>.Instance);

        var action = Action(0); // ordinary movement, not the stand-up sort
        service.PostAction(zone, CharacterId, in action);

        Assert.Null(session.DisconnectReason);
    }
}
