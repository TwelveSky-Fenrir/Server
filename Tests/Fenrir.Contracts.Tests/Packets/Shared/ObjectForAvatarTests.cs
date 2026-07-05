using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class ObjectForAvatarTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(632, ObjectForAvatar.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var action = new ActionInfo
        {
            Type = v.NextInt(),
            Sort = v.NextInt(),
            Frame = v.NextFloat(),
            Location = v.NextFloatArray(3),
            TargetLocation = v.NextFloatArray(3),
            Front = v.NextFloat(),
            TargetFront = v.NextFloat(),
            PetLocation = v.NextFloatArray(3),
            PetTargetLocation = v.NextFloatArray(3),
            PetFront = v.NextFloat(),
            PetSort = v.NextInt(),
            TargetObjectSort = v.NextInt(),
            TargetObjectIndex = v.NextInt(),
            TargetObjectUniqueNumber = v.NextInt(),
            SkillNumber = v.NextInt(),
            SkillGradeNum1 = v.NextInt(),
            SkillGradeNum2 = v.NextInt(),
            SkillValue = v.NextInt()
        };

        var data = new ObjectForAvatar
        {
            VisibleState = v.NextInt(),
            SpecialState = v.NextInt(),
            KillOtherTribe = v.NextInt(),
            GoodFellow = v.NextInt(),
            GuildName = v.NextString(13),
            GuildRole = v.NextInt(),
            CallName = v.NextString(5),
            GuildMarkEffect = v.NextInt(),
            Name = v.NextString(13),
            Tribe = v.NextInt(),
            PreviousTribe = v.NextInt(),
            Gender = v.NextInt(),
            HeadType = v.NextInt(),
            FaceType = v.NextInt(),
            Level1 = v.NextInt(),
            Level2 = v.NextInt(),
            EquipForView = v.NextIntArray(26),
            AnimalNumber = v.NextInt(),
            Title = v.NextInt(),
            Halo = v.NextInt(),
            RebirthNum = v.NextInt(),
            BattleTeam = v.NextInt(),
            Action = action,
            MaxLifeValue = v.NextInt(),
            LifeValue = v.NextInt(),
            MaxManaValue = v.NextInt(),
            ManaValue = v.NextInt(),
            EffectValueForView = v.NextIntArray(35),
            PartyName = v.NextString(13),
            DuelState = v.NextIntArray(3),
            PShopState = v.NextInt(),
            PShopName = v.NextString(25),
            CostumeNumber = v.NextInt(),
            BufEffectTimeState = v.NextInt(),
            BufSort = v.NextInt(),
            AutoState = v.NextInt(),
            FishingState = v.NextInt(),
            FishingStep = v.NextInt(),
            FishingPoint = v.NextFloatArray(3),
            RankPoint = v.NextInt(),
            TargetState = v.NextInt(),
            AnimalAbsorbState = v.NextInt(),
            PetValid = v.NextInt(),
            Unk1 = v.NextInt(),
            PetLocation = v.NextFloatArray(3),
            PetFrame = v.NextFloat(),
            Unk624 = v.NextInt(),
            Unk625 = v.NextInt(),
            UniqueSkillNumber = v.NextInt(),
            UniqueSkillBuffTime = v.NextInt(),
            CostumeState = v.NextInt(),
            StellarCoreNumber = v.NextInt()
        };

        var buffer = new byte[ObjectForAvatar.WireSize];
        var written = data.Write(buffer);
        Assert.Equal(ObjectForAvatar.WireSize, written);

        Assert.True(ObjectForAvatar.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(data, roundTripped);
    }

    // Name (offset 48, char[13]) is followed by a 3-byte compiler pad before Tribe at offset 64;
    // Action is a nested ACTION_INFO at offset 216.
    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[ObjectForAvatar.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 10);
        Encoding.Latin1.GetBytes("Hero1", buffer.AsSpan(48, 13));
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(64, 4), 20);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(216 + 100, 4), 30);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(628, 4), 40);

        var ok = ObjectForAvatar.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(10, packet.VisibleState);
        Assert.Equal("Hero1", packet.Name);
        Assert.Equal(20, packet.Tribe);
        Assert.Equal(30, packet.Action.SkillValue);
        Assert.Equal(40, packet.StellarCoreNumber);
    }
}
