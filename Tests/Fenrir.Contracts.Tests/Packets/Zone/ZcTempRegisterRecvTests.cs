using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTempRegisterRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcTempRegisterRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TempRegisterRecv, ZcTempRegisterRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZcTempRegisterRecv { Result = 1 };

        Span<byte> buffer = stackalloc byte[ZcTempRegisterRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcTempRegisterRecv.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
