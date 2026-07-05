using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcCostumeState2RecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, CostumeVisibilityResponse.PayloadSize);
        Assert.Equal(3 * 4, CostumeVisibilityResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CostumeVisibility, CostumeVisibilityResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new CostumeVisibilityResponse { Sort = 1, Sort2 = 0, Sort3 = 0 };

        var golden = new byte[CostumeVisibilityResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 0);

        Span<byte> buffer = new byte[CostumeVisibilityResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(CostumeVisibilityResponse.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
