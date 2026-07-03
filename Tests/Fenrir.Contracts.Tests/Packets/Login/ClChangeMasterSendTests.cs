using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class ClChangeMasterSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=62 (9-byte inbound header) -> 53-byte payload (int + char[49]). Dead opcode, but the
        // size MUST be registered or a legacy client sending it desynchronizes the frame decoder (report §7.2).
        Assert.Equal(53, ClChangeMasterSend.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ClChangeMasterSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1);
        WriteFixedString(buffer.AsSpan(4, 49), "SomeMasterName");

        Assert.True(ClChangeMasterSend.TryRead(buffer, out var packet));

        Assert.Equal(1, packet.AvatarPost);
        Assert.Equal("SomeMasterName", packet.MasterId);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ClChangeMasterSend.PayloadSize - 1];

        Assert.False(ClChangeMasterSend.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
