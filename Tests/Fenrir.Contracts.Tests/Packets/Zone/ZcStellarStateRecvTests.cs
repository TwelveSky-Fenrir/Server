using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcStellarStateRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(28, ZcStellarStateRecv.PayloadSize);
        Assert.Equal(7 * 4, ZcStellarStateRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.StellarStateRecv, ZcStellarStateRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcStellarStateRecv
        {
            Result = 0,
            Sort = 3,
            Value = 2,
            Page = 1,
            PosX = 5,
            PosY = 6,
            ItemIndex = 4321
        };

        var golden = new byte[ZcStellarStateRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 3);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 2);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 1);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 5);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(20), 6);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(24), 4321);

        Span<byte> buffer = new byte[ZcStellarStateRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcStellarStateRecv.PayloadSize, written);
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
