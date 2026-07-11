using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClCreateAvatarSend2Tests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(41, CreateAvatarRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[CreateAvatarRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), 17);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8, 4), 29);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(12, 4), 41);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16, 4), 53);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(20, 4), 67);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24, 4), 79);
        WriteFixedString(buffer.AsSpan(28, 13), "NewHero");

        Assert.True(CreateAvatarRequest.TryRead(buffer, out var packet));

        Assert.Equal(3, packet.AvatarPost);
        Assert.Equal(17, packet.Tribe);
        Assert.Equal(29, packet.PreviousTribe);
        Assert.Equal(41, packet.Gender);
        Assert.Equal(53, packet.Head);
        Assert.Equal(67, packet.Face);
        Assert.Equal(79, packet.Weapon);
        Assert.Equal("NewHero", packet.AvatarName);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[CreateAvatarRequest.PayloadSize - 1];

        Assert.False(CreateAvatarRequest.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
