using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzBuyBloodMarkSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(36, BuyBloodMarkItemRequest.PayloadSize);
        Assert.Equal(4 + 4 + 4 + 24, BuyBloodMarkItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.BuyBloodMarkItem, BuyBloodMarkItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[BuyBloodMarkItemRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 2);
        var value = new int[6];
        for (var i = 0; i < value.Length; i++)
        {
            value[i] = (i + 1) * 11;
            BinaryPrimitives.WriteInt32LittleEndian(buffer[(12 + i * 4)..], value[i]);
        }

        var ok = BuyBloodMarkItemRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(4, packet.BloodIndex);
        Assert.Equal(1, packet.Page);
        Assert.Equal(2, packet.Index);
        Assert.True(value.AsSpan().SequenceEqual(packet.Value));
    }
}
