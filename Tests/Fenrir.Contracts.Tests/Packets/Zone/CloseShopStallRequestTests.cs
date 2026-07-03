using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzEndPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CloseShopStallRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.CloseShopStall, CloseShopStallRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CloseShopStallRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);

        var ok = CloseShopStallRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
    }
}
