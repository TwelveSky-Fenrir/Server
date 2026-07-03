using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTribeWorkSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(104, CzTribeWorkSend.PayloadSize);
        Assert.Equal(4 + 100, CzTribeWorkSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeWorkSend, CzTribeWorkSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzTribeWorkSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 5);
        var data = new byte[100];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 3);
        data.AsSpan().CopyTo(buffer[4..]);

        var ok = CzTribeWorkSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(5, packet.Sort);
        Assert.True(data.AsSpan().SequenceEqual(packet.Data));
    }
}
