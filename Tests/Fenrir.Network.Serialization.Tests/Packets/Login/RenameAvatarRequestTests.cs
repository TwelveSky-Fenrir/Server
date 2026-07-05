using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClChangeAvatarNameSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=34 (9-byte inbound header) -> 25-byte payload (int + char[13] + int + int).
        Assert.Equal(25, RenameAvatarRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[RenameAvatarRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 2);
        WriteFixedString(buffer.AsSpan(4, 13), "NewName");
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(17, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(21, 4), 37);

        Assert.True(RenameAvatarRequest.TryRead(buffer, out var packet));

        Assert.Equal(2, packet.AvatarPost);
        Assert.Equal("NewName", packet.ChangeAvatarName);
        Assert.Equal(1, packet.Page);
        Assert.Equal(37, packet.Index);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[RenameAvatarRequest.PayloadSize - 1];

        Assert.False(RenameAvatarRequest.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
