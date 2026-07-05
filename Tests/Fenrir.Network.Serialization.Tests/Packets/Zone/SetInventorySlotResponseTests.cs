using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcSetInventoryItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, SetInventorySlotResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.SetInventorySlot, SetInventorySlotResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[SetInventorySlotResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static SetInventorySlotResponse CreatePopulated()
    {
        return new SetInventorySlotResponse { Page = 11, Index = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, SetInventorySlotResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Page);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Index);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
