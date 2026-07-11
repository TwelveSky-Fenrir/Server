using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.Avatars;

public class AvatarInfoFactoryTests
{
    [Fact]
    public void CreateForCharacter_PersistedWarPoint_IsReflectedOnTheWireProjection_NotZero()
    {
        const int PersistedWarPoint = 1234;

        var character = new CharacterWorldSnapshotDto(
            1, 1, 0, "Hero", 1, 0,
            1, 1, 10, 1, 0f, 0f, 0f, 0f,
            30, 30, 21, 21, 1, 0, 0,
            1, 1, 1, 1, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, false, [],
            0, 0, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0,
            WarPoint: PersistedWarPoint);

        var avatarInfo = AvatarInfoFactory.CreateForCharacter(character, []);

        Assert.Equal(PersistedWarPoint, avatarInfo.WarPoint);
    }

    [Fact]
    public void CreateForRuntimeState_LiveWarPoint_IsReflectedOnTheWireProjection_NotZero()
    {
        const int LiveWarPoint = 777;

        var state = new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 1,
            Gender = 0,
            HeadType = 2,
            FaceType = 3,
            Level = 42,
            WarPoint = LiveWarPoint
        };

        var avatarInfo = AvatarInfoFactory.CreateForRuntimeState(state, 2, 0f, 0f, 0f);

        Assert.Equal(LiveWarPoint, avatarInfo.WarPoint);
    }
}
