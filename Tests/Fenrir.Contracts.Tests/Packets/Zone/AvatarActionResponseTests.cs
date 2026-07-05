using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcAvatarActionRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(644, AvatarActionResponse.PayloadSize);
        Assert.Equal(4 + 4 + ObjectForAvatar.WireSize + 4, AvatarActionResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AvatarAction, AvatarActionResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var data = WireTestKit.CreatePopulated<ObjectForAvatar>(7);
        var packet = new AvatarActionResponse
        {
            ServerIndex = 12,
            UniqueNumber = 999_000_111u,
            Data = data,
            CheckChangeActionState = 3
        };

        Span<byte> buffer = new byte[AvatarActionResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(AvatarActionResponse.PayloadSize, written);
        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(999_000_111u, BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]));

        var ok = ObjectForAvatar.TryRead(buffer.Slice(8, ObjectForAvatar.WireSize), out var dataBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(data, dataBack);

        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(buffer[(8 + ObjectForAvatar.WireSize)..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var action = new ActionInfo
        {
            Type = 1,
            Sort = 2,
            Frame = 3.5f,
            Location = [10f, 0f, 20f],
            TargetLocation = [11f, 0f, 21f],
            Front = 0.1f,
            TargetFront = 0.2f,
            PetLocation = [12f, 0f, 22f],
            PetTargetLocation = [13f, 0f, 23f],
            PetFront = 0.3f,
            PetSort = 4,
            TargetObjectSort = 5,
            TargetObjectIndex = 6,
            TargetObjectUniqueNumber = 7,
            SkillNumber = 8,
            SkillGradeNum1 = 9,
            SkillGradeNum2 = 10,
            SkillValue = 11
        };
        var data = new ObjectForAvatar
        {
            VisibleState = 1,
            SpecialState = 0,
            KillOtherTribe = 5,
            GoodFellow = 2,
            GuildName = "Aesir",
            GuildRole = 3,
            CallName = "Thor",
            GuildMarkEffect = 1,
            Name = "Odin",
            Tribe = 1,
            PreviousTribe = 0,
            Gender = 1,
            HeadType = 2,
            FaceType = 3,
            Level1 = 60,
            Level2 = 0,
            EquipForView = Enumerable.Range(1, 26).ToArray(),
            AnimalNumber = 0,
            Title = 4,
            Halo = 1,
            RebirthNum = 2,
            BattleTeam = 0,
            Action = action,
            MaxLifeValue = 5000,
            LifeValue = 4800,
            MaxManaValue = 2000,
            ManaValue = 1900,
            EffectValueForView = Enumerable.Range(1, 35).ToArray(),
            PartyName = "Valhalla",
            DuelState = [0, 0, 0],
            PShopState = 0,
            PShopName = "OdinShop",
            CostumeNumber = 7,
            BufEffectTimeState = 0,
            BufSort = 0,
            AutoState = 1,
            FishingState = 0,
            FishingStep = 0,
            FishingPoint = [1f, 2f, 3f],
            RankPoint = 100,
            TargetState = 0,
            AnimalAbsorbState = 0,
            PetValid = 0,
            Unk1 = 0,
            PetLocation = [4f, 5f, 6f],
            PetFrame = 0.5f,
            Unk624 = 0,
            Unk625 = 0,
            UniqueSkillNumber = 12,
            UniqueSkillBuffTime = 0,
            CostumeState = 0,
            StellarCoreNumber = 0
        };
        var packet = new AvatarActionResponse
        {
            ServerIndex = 42,
            UniqueNumber = 0xDEADBEEFu,
            Data = data,
            CheckChangeActionState = 9
        };

        var golden = new byte[AvatarActionResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 42);
        BinaryPrimitives.WriteUInt32LittleEndian(golden.AsSpan(4), 0xDEADBEEFu);
        var dataWritten = WireTestKit.EncodeObjectForAvatar(golden.AsSpan(8, ObjectForAvatar.WireSize), data);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8 + ObjectForAvatar.WireSize), 9);

        Assert.Equal(ObjectForAvatar.WireSize, dataWritten);

        Span<byte> buffer = new byte[AvatarActionResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
