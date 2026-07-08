using Fenrir.Application.Login.Domain;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Tests;

// Legacy SEND_LOGIN train (S04_MyWork02.cpp l.42-67): every CL_LOGIN_SEND, success or failure, must produce
// exactly LC_LOGIN_RECV + 3x LC_USER_AVATAR_RECV2 + LC_RECOMMAND_WORLD_RECV + LC_RECOMMAND_WORLD2_RECV,
// byte-for-byte, in that order.
public class LoginTrainTests
{
    [Fact]
    public async Task Send_PutsExactlySixPacketsOnTheWireInLegacyOrder()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        var loginRecv = LoginTrain.BuildLoginRecv(0, "MG1", 1, "");
        var avatarSlots = LoginTrain.BuildAvatarSlots(Array.Empty<CharacterSummaryDto>());

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
        var emptySlot = LoginTrain.BuildAvatarSlots(Array.Empty<CharacterSummaryDto>())[0];
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
        CharacterSummaryDto[] characters =
        [
            new(1, 0, "Hero", 2, 1, 3, 4, 12),
            new(2, 2, "Second", 1, 0, 1, 2, 5)
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

    // Major audit gap fix: GuildName must reflect actual live guild membership, resolved by the caller
    // (LoginService, via IGuildRepository) and handed in as a CharacterId-keyed lookup -- Domain itself stays
    // I/O-free, see LoginTrain.BuildAvatarSlots' own remarks.
    [Fact]
    public void BuildAvatarSlots_GuildLookupProvided_SetsGuildNameOnlyForMembersPresentInIt()
    {
        CharacterSummaryDto[] characters =
        [
            new(1, 0, "Hero", 2, 1, 3, 4, 12),
            new(2, 1, "Guildless", 1, 0, 1, 2, 5)
        ];
        var guildNamesByCharacterId = new Dictionary<int, string> { [1] = "TestGuild" };

        var slots = LoginTrain.BuildAvatarSlots(characters, guildNamesByCharacterId);

        Assert.Equal("TestGuild", slots[0].GuildName);
        Assert.Equal("", slots[1].GuildName);
        // Empty slot (no character in it at all) is unaffected.
        Assert.Equal("", slots[2].GuildName);
    }

    // No lookup at all (every pre-existing caller/test) must be indistinguishable from "nobody has a guild" --
    // proves the new optional parameter is backward compatible, not just defaulted away silently.
    [Fact]
    public void BuildAvatarSlots_NoGuildLookupProvided_GuildNameStaysEmptyForEveryOccupiedSlot()
    {
        CharacterSummaryDto[] characters = [new(1, 0, "Hero", 2, 1, 3, 4, 12)];

        var slots = LoginTrain.BuildAvatarSlots(characters);

        Assert.Equal("", slots[0].GuildName);
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
}
