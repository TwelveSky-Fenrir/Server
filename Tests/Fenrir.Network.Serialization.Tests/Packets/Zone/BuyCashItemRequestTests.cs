using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzBuyCashItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(40, BuyCashItemRequest.PayloadSize);
        Assert.Equal(4 + 4 + 4 + 24 + 4, BuyCashItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.BuyCashItem, BuyCashItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[BuyCashItemRequest.PayloadSize];
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

        var ok = BuyCashItemRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.CostInfoIndex);
        Assert.Equal(1, packet.Page);
        Assert.Equal(2, packet.Index);
        Assert.True(value.AsSpan().SequenceEqual(packet.Value));
        Assert.Equal(42, packet.Version);
    }
}
