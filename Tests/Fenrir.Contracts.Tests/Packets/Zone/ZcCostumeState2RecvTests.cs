using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcCostumeState2RecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, ZcCostumeState2Recv.PayloadSize);
        Assert.Equal(3 * 4, ZcCostumeState2Recv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CostumeState2Recv, ZcCostumeState2Recv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcCostumeState2Recv { Sort = 1, Sort2 = 0, Sort3 = 0 };

        var golden = new byte[ZcCostumeState2Recv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 0);

        Span<byte> buffer = new byte[ZcCostumeState2Recv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcCostumeState2Recv.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
