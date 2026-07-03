using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcMakePetRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, ZcMakePetRecv.PayloadSize);
        Assert.Equal(4 + 6 * 4, ZcMakePetRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MakePetRecv, ZcMakePetRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var value = new[] { 92291, 0, 0, 15, 0, 0 };
        var packet = new ZcMakePetRecv { Result = 10000, Value = value };

        var golden = new byte[ZcMakePetRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 10000);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4 + i * 4), value[i]);

        Span<byte> buffer = new byte[ZcMakePetRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcMakePetRecv.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
