using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_DESTROY_ITEM_RECV (ZONE.h:991-996, 32-byte payload): Result/Money/Value[6].</summary>
public class ZcDestroyItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, DestroyItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DestroyItem, DestroyItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[DestroyItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static DestroyItemResponse CreatePopulated()
    {
        return new DestroyItemResponse { Result = 11, Money = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, DestroyItemResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Money);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
