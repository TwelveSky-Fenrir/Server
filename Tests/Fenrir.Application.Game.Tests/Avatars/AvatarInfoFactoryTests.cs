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
            CharacterId: 1, AccountId: 1, Slot: 0, Name: "Hero", Tribe: 1, Gender: 0,
            HeadType: 1, FaceType: 1, Level: 10, MapId: 1, PosX: 0f, PosY: 0f, PosZ: 0f, Heading: 0f,
            Life: 30, MaxLife: 30, Mana: 21, MaxMana: 21, FlushSequence: 1, Experience: 0, Level2: 0,
            StatVit: 1, StatStr: 1, StatInt: 1, StatDex: 1, StatPoints: 0, SkillPoints: 0, Money: 0,
            BigMoney: 0, StoreMoney: 0, BigStoreMoney: 0, RebirthCount: 0, Title: 0, Halo: 0,
            ContributionPoints: 0, EatLifePotion: 0, EatManaPotion: 0, EatStrPotion: 0, EatDexPotion: 0,
            EatElePotion: 0, ProtectForDeath: 0, ProtectForDestroy: 0, DoubleExpTime1: 0, DoubleExpTime2: 0,
            DropItemTime: 0, InventoryDate: 0, StoreDate: 0, QuestStepPermanent: 0, QuestActiveId: 0,
            QuestSort: 0, QuestTargetPhase: 0, QuestKillCounter: 0, JoinWar: 0, MissionKillOtherTribe: 0,
            MissionKillMonster: 0, MissionPlayTime: 0, AutoHuntEnabled: false, AutoHuntConfig: [],
            AutoLifeRatio: 0, AutoManaRatio: 0, PetGrowth: 0, PetActivity: 0, TeacherPoint: 0,
            AutoBuffTime: 0, PremiumExpireUtc: 0, Exp2: 0, PreviousTribe: 1, MountItemId: 0,
            MountExpActivity: 0, MountPower: 0, MountSlotIndex: 0, MountTime: 0,
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
