using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcExchangeItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, RerollItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.RerollItem, RerollItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[RerollItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static RerollItemResponse CreatePopulated()
    {
        return new RerollItemResponse { Result = 11, Cost = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, RerollItemResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Cost);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
