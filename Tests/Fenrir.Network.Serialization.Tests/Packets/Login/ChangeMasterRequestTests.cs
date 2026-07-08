using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClChangeMasterSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // Dead opcode; PayloadSize=53 (62-byte frame - 9-byte header) must stay registered or legacy clients desync.
        Assert.Equal(53, ChangeMasterRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ChangeMasterRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1);
        WriteFixedString(buffer.AsSpan(4, 49), "SomeMasterName");

        Assert.True(ChangeMasterRequest.TryRead(buffer, out var packet));

        Assert.Equal(1, packet.AvatarPost);
        Assert.Equal("SomeMasterName", packet.MasterId);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ChangeMasterRequest.PayloadSize - 1];

        Assert.False(ChangeMasterRequest.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
