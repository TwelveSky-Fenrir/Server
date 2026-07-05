using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTribeWorkSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, TribeActionRequest.PayloadSize);
        Assert.Equal(4 + 100, TribeActionRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeAction, TribeActionRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[TribeActionRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 5);
        var data = new byte[100];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 3);
        data.AsSpan().CopyTo(buffer[4..]);

        var ok = TribeActionRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(5, packet.Sort);
        Assert.True(data.AsSpan().SequenceEqual(packet.Data));
    }
}
