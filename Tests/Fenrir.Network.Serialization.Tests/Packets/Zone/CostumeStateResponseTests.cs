using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcCostumeStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, CostumeStateResponse.PayloadSize);
        Assert.Equal(8 * 4, CostumeStateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CostumeState, CostumeStateResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new CostumeStateResponse
        {
            Result = 0,
            Sort = 5,
            Value = 3,
            Page = 2,
            PosX = 4,
            PosY = 6,
            ItemIndex = 1234,
            CostumeDate = 20260703
        };

        var golden = new byte[CostumeStateResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 5);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 3);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 2);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 4);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(20), 6);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(24), 1234);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(28), 20260703);

        Span<byte> buffer = new byte[CostumeStateResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(CostumeStateResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
