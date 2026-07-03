using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzBuyCashItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(40, CzBuyCashItemSend.PayloadSize);
        Assert.Equal(4 + 4 + 4 + 24 + 4, CzBuyCashItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.BuyCashItemSend, CzBuyCashItemSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzBuyCashItemSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 2);
        var value = new int[6];
        for (var i = 0; i < value.Length; i++)
        {
            value[i] = (i + 1) * 10;
            BinaryPrimitives.WriteInt32LittleEndian(buffer[(12 + i * 4)..], value[i]);
        }

        BinaryPrimitives.WriteInt32LittleEndian(buffer[36..], 42);

        var ok = CzBuyCashItemSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.CostInfoIndex);
        Assert.Equal(1, packet.Page);
        Assert.Equal(2, packet.Index);
        Assert.True(value.AsSpan().SequenceEqual(packet.Value));
        Assert.Equal(42, packet.Version);
    }
}
