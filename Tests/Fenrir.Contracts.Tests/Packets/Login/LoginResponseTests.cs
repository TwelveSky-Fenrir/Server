using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcLoginRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=693 (1-byte outbound header) -> 692-byte payload.
        Assert.Equal(692, LoginResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var giftInfo = Enumerable.Range(1, 50).ToArray();
        var packet = new LoginResponse
        {
            Result = 1,
            Id = "MG100234",
            UserSort = 2,
            GoodFellow = 3,
            LoginPlace = 4,
            LoginPremium = 5,
            SecondLoginSort = 6,
            MousePassword = "abcd",
            SecretCardIndex01 = 7,
            SecretCardIndex02 = 8,
            GiftInfo = giftInfo,
            ResultString = "Welcome back, traveler!"
        };

        var buffer = new byte[LoginResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LoginResponse.PayloadSize, written);

        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));

        // Id is obfuscated via ApplyUidXor, independent of the packet-wide XOR applied later at
        // the send layer (§3.3). ApplyUidXor is involutive as long as the content has no 0x10/0xFE
        // byte, so re-applying it to Write's output recovers the plaintext.
        var idBytes = buffer.AsSpan(4, 255).ToArray();
        WireXor.ApplyUidXor(idBytes);
        Assert.Equal(packet.Id, DecodeFixedString(idBytes));

        Assert.Equal(packet.UserSort, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(259, 4)));
        Assert.Equal(packet.GoodFellow, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(263, 4)));
        Assert.Equal(packet.LoginPlace, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(267, 4)));
        Assert.Equal(packet.LoginPremium, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(271, 4)));
        Assert.Equal(packet.SecondLoginSort, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(275, 4)));
        Assert.Equal(packet.MousePassword, DecodeFixedString(buffer.AsSpan(279, 5)));
        Assert.Equal(packet.SecretCardIndex01, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(284, 4)));
        Assert.Equal(packet.SecretCardIndex02, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(288, 4)));

        var decodedGiftInfo = new int[50];
        for (var i = 0; i < 50; i++)
            decodedGiftInfo[i] = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(292 + i * 4, 4));
        Assert.True(packet.GiftInfo.SequenceEqual(decodedGiftInfo));

        Assert.Equal(packet.ResultString, DecodeFixedString(buffer.AsSpan(492, 200)));
    }

    private static string DecodeFixedString(ReadOnlySpan<byte> span)
    {
        var nullIndex = span.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nullIndex < 0 ? span : span[..nullIndex]);
    }
}
