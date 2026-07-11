using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Application.Login.Tests;

public class LoginTrainTests
{
    [Fact]
    public async Task Send_PutsExactlySixPacketsOnTheWireInLegacyOrder()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        var loginRecv = LoginTrain.BuildLoginRecv(0, "MG1", 1, "");
        var avatarSlots = LoginTrain.BuildAvatarSlots([]);

        LoginTrain.Send(session, loginRecv, avatarSlots);

        var expected = Concat(
            FrameOf(loginRecv),
            FrameOf(avatarSlots[0]),
            FrameOf(avatarSlots[1]),
            FrameOf(avatarSlots[2]),
            FrameOf(new WorldRecommendationResponse
                { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 }),
            FrameOf(new WorldRecommendationFinalResponse
                { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 }));

        var actual = await PacketAssert.ReadSentBytesAsync(pipe);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task SendFailure_EchoesRequestIdWithZeroedSortsAndFailurePinMask()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);

        LoginTrain.SendFailure(session, 7, "baduser");

        var actual = await PacketAssert.ReadSentBytesAsync(pipe);

        var expectedLoginRecv = LoginTrain.BuildLoginRecv(7, "baduser", 0, "0000");
        var emptySlot = LoginTrain.BuildAvatarSlots([])[0];
        var expected = Concat(
            FrameOf(expectedLoginRecv),
            FrameOf(emptySlot), FrameOf(emptySlot), FrameOf(emptySlot),
            FrameOf(new WorldRecommendationResponse
                { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 }),
            FrameOf(new WorldRecommendationFinalResponse
                { AddKillOtherTribe0 = 0, AddKillOtherTribe1 = 0, AddKillOtherTribe2 = 0 }));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildLoginRecv_PinsEveryLegacyAlwaysZeroFieldToNothingToReport()
    {
        var recv = LoginTrain.BuildLoginRecv(0, "MG7", 1, "****");

        Assert.Equal(0, recv.UserSort);
        Assert.Equal(0, recv.GoodFellow);
        Assert.Equal(0, recv.LoginPlace);
        Assert.Equal(0, recv.LoginPremium);
        Assert.Equal(0, recv.SecretCardIndex01);
        Assert.Equal(0, recv.SecretCardIndex02);
        Assert.Equal(50, recv.GiftInfo.Length);
        Assert.All(recv.GiftInfo, value => Assert.Equal(0, value));
        Assert.Equal("", recv.ResultString);
        Assert.Equal(1, recv.SecondLoginSort);
        Assert.Equal("****", recv.MousePassword);
    }

    [Fact]
    public void BuildAvatarSlots_AlwaysReturnsExactlyThreeSlots_EmptyOnesZeroed()
    {
        AvatarRosterEntry[] characters =
        [
            EntryFor(new CharacterRosterDtoBuilder(1, 0, "Hero", 2, 1, 3, 4, 12).Build()),
            EntryFor(new CharacterRosterDtoBuilder(2, 2, "Second", 1, 0, 1, 2, 5).Build())
        ];

        var slots = LoginTrain.BuildAvatarSlots(characters);

        Assert.Equal(3, slots.Length);

        Assert.Equal("Hero", slots[0].Name);
        Assert.Equal(2, slots[0].Tribe);
        Assert.Equal(1, slots[0].Gender);
        Assert.Equal(3, slots[0].HeadType);
        Assert.Equal(4, slots[0].FaceType);
        Assert.Equal(12, slots[0].Level1);

        Assert.Equal("", slots[1].Name);
        Assert.Equal(0, slots[1].Tribe);
        Assert.Equal(0, slots[1].Level1);

        Assert.Equal("Second", slots[2].Name);
        Assert.Equal(1, slots[2].Tribe);
        Assert.Equal(5, slots[2].Level1);
    }

    [Fact]
    public void BuildAvatarSlots_EntryCarriesAGuildName_OverlaysItOntoTheWireField()
    {
        AvatarRosterEntry[] characters =
        [
            EntryFor(new CharacterRosterDtoBuilder(1, 0, "Hero", 2, 1, 3, 4, 12).Build(), "TestGuild"),
            EntryFor(new CharacterRosterDtoBuilder(2, 1, "Guildless", 1, 0, 1, 2, 5).Build())
        ];

        var slots = LoginTrain.BuildAvatarSlots(characters);

        Assert.Equal("TestGuild", slots[0].GuildName);
        Assert.Equal("", slots[1].GuildName);
        Assert.Equal("", slots[2].GuildName);
    }

    [Fact]
    public void BuildAvatarSlots_OccupiedSlot_OverlaysEquipmentAndProgressionScalars()
    {
        var character = new CharacterRosterDtoBuilder(1, 0, "Hero", 2, 1, 3, 4, 12)
        {
            PreviousTribe = 1,
            Level2 = 9,
            Halo = 3,
            RebirthCount = 2,
            ContributionPoints = 77,
            SkillPoints = 4,
            EatLifePotion = 1,
            EatManaPotion = 2,
            EatStrPotion = 3,
            EatDexPotion = 4,
            EatElePotion = 5,
            PetGrowth = 60,
            PetActivity = 80
        }.Build();

        CharacterRosterItemDto[] items =
        [
            new(character.CharacterId, 2, 7, 9001, 1, 0, 0,
                0, 0, 0, 0, 0, 0, 1),
            new(character.CharacterId, 0, 5, 500, 3, 0, 0,
                0, 0, 0, 0, 0, 0, 2),
            new(character.CharacterId, 3, 2, 700, 1, 0, 0,
                0, 0, 0, 0, 0, 0, 3)
        ];

        var entry = new AvatarRosterEntry(character, items, "", new Dictionary<byte, string>(), "", "");

        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(1, slots[0].PreviousTribe);
        Assert.Equal(9, slots[0].Level2);
        Assert.Equal(3, slots[0].Halo);
        Assert.Equal(2, slots[0].RebirthNum);
        Assert.Equal(77, slots[0].KillOtherTribe);
        Assert.Equal(4, slots[0].SkillPoint);
        Assert.Equal(1, slots[0].EatLifePotion);
        Assert.Equal(2, slots[0].EatManaPotion);
        Assert.Equal(3, slots[0].EatStrPotion);
        Assert.Equal(4, slots[0].EatDexPotion);
        Assert.Equal(5, slots[0].EatElePotion);

        Assert.Equal(9001, slots[0].Equip[7 * 4]);
        Assert.Equal(500, slots[0].Inventory[5 * 6]);
        Assert.Equal(3, slots[0].Inventory[5 * 6 + 3]);
        Assert.Equal(700, slots[0].StoreItem[2 * 4]);
    }

    [Fact]
    public void BuildAvatarSlots_OccupiedSlot_PetEquipSlotPacksActivityAndGrowthInsteadOfExpireDateAndUpgradeByte()
    {
        var character = new CharacterRosterDtoBuilder(1, 0, "Hero", 2, 1, 3, 4, 12)
        {
            PetGrowth = 60,
            PetActivity = 80
        }.Build();

        CharacterRosterItemDto[] items =
        [
            new(character.CharacterId, 2, 8, 4200, 1, 0, 0,
                0, 0, 0, 0, 0, 0, 1)
        ];

        var entry = new AvatarRosterEntry(character, items, "", new Dictionary<byte, string>(), "", "");
        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(4200, slots[0].Equip[8 * 4]);
        Assert.Equal(80, slots[0].Equip[8 * 4 + 1]);
        Assert.Equal(60, slots[0].Equip[8 * 4 + 2]);
    }

    [Fact]
    public void BuildAvatarSlots_OccupiedSlot_OverlaysFriendTeacherAndStudentNames()
    {
        var character = new CharacterRosterDtoBuilder(1, 0, "Hero", 2, 1, 3, 4, 12).Build();
        var friendNameBySlot = new Dictionary<byte, string> { [0] = "Ally", [3] = "Buddy" };

        var entry = new AvatarRosterEntry(character, [], "", friendNameBySlot, "Mentor", "Apprentice");
        var slots = LoginTrain.BuildAvatarSlots([entry]);

        Assert.Equal(10, slots[0].Friend.Length);
        Assert.Equal("Ally", slots[0].Friend[0]);
        Assert.Equal("Buddy", slots[0].Friend[3]);
        Assert.Equal("", slots[0].Friend[1]);
        Assert.Equal("Mentor", slots[0].Teacher);
        Assert.Equal("Apprentice", slots[0].Student);
    }

    [Fact]
    public void BuildAvatarSlots_NoCharacters_EveryFieldStaysAtTheZeroTemplateForAllThreeSlots()
    {
        var slots = LoginTrain.BuildAvatarSlots([]);

        Assert.Equal(3, slots.Length);
        Assert.All(slots, slot =>
        {
            Assert.Equal("", slot.GuildName);
            Assert.Equal("", slot.Teacher);
            Assert.Equal("", slot.Student);
            Assert.All(slot.Friend, name => Assert.Equal("", name));
        });
    }

    private static AvatarRosterEntry EntryFor(CharacterRosterDto character, string guildName = "")
    {
        return new AvatarRosterEntry(character, [], guildName, new Dictionary<byte, string>(), "", "");
    }

    private static byte[] FrameOf<TPacket>(TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var buffer = new byte[FrameWriter.FrameSizeOf<TPacket>()];
        FrameWriter.WriteFrame(in packet, buffer);
        return buffer;
    }

    private static byte[] Concat(params byte[][] frames)
    {
        var total = frames.Sum(f => f.Length);
        var result = new byte[total];
        var offset = 0;
        foreach (var frame in frames)
        {
            frame.CopyTo(result, offset);
            offset += frame.Length;
        }

        return result;
    }

        private sealed class CharacterRosterDtoBuilder(
        int characterId,
        byte slot,
        string name,
        byte tribe,
        byte gender,
        byte headType,
        byte faceType,
        short level)
    {
        public byte PreviousTribe { get; init; }
        public short Level2 { get; init; }
        public int Halo { get; init; }
        public int RebirthCount { get; init; }
        public int ContributionPoints { get; init; }
        public int SkillPoints { get; init; }
        public int EatLifePotion { get; init; }
        public int EatManaPotion { get; init; }
        public int EatStrPotion { get; init; }
        public int EatDexPotion { get; init; }
        public int EatElePotion { get; init; }
        public int PetGrowth { get; init; }
        public byte PetActivity { get; init; }
        public short MapId { get; init; }
        public float PosX { get; init; }
        public float PosY { get; init; }
        public float PosZ { get; init; }
        public int Life { get; init; }
        public int Mana { get; init; }

        public CharacterRosterDto Build()
        {
            return new CharacterRosterDto(characterId, slot, name, tribe, PreviousTribe, gender, headType, faceType,
                level, Level2, Halo, RebirthCount, ContributionPoints, SkillPoints, EatLifePotion, EatManaPotion,
                EatStrPotion, EatDexPotion, EatElePotion, PetGrowth, PetActivity, MapId, PosX, PosY, PosZ, Life,
                Mana);
        }
    }
}
