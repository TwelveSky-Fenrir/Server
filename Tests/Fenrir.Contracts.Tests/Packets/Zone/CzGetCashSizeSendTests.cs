using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGetCashSizeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzGetCashSizeSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GetCashSizeSend, CzGetCashSizeSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzGetCashSizeSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);

        var ok = CzGetCashSizeSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
    }
}
