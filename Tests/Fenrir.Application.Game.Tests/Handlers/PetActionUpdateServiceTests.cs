using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.BuffsMountsCosmetics;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Handlers;

public class PetActionUpdateServiceTests
{
    private static ActionInfo PetAction(int petSort, float petFront, float[] petLocation, float[] petTargetLocation)
    {
        return new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [0f, 0f, 0f],
            TargetLocation = [0f, 0f, 0f],
            Front = 0f,
            TargetFront = 0f,
            PetLocation = petLocation,
            PetTargetLocation = petTargetLocation,
            PetFront = petFront,
            PetSort = petSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe) Setup(Zone zone, int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        return (session, pipe);
    }

    [Fact]
    public void ValidUpdate_MirrorsPetSubFieldsOnly_NoReply()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = Setup(zone, 10);
        var service = new PetActionUpdateService();

        var action = PetAction(3, 1.5f, [1f, 2f, 3f], [4f, 5f, 6f]);
        service.Apply(zone, session.CharacterId!.Value, in action);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(3, player!.PetActionSort);
        Assert.Equal(1.5f, player.PetActionFront);
        Assert.Equal(1f, player.PetActionLocationX);
        Assert.Equal(2f, player.PetActionLocationY);
        Assert.Equal(3f, player.PetActionLocationZ);
        Assert.Equal(4f, player.PetActionTargetLocationX);
        Assert.Equal(5f, player.PetActionTargetLocationY);
        Assert.Equal(6f, player.PetActionTargetLocationZ);

        // no reply and no position change of any kind -- matches the legacy handler exactly
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
        Assert.Equal(0, player.ActionSort);
    }

    [Fact]
    public void UnknownCharacter_IsIgnored()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.Post(ZoneCommand.PetAction(999, PetAction(1, 0f, [0f, 0f, 0f], [0f, 0f, 0f])));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
