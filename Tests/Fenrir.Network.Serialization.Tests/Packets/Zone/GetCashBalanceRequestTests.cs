using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGetCashSizeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, GetCashBalanceRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GetCashBalance, GetCashBalanceRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[GetCashBalanceRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);

        var ok = GetCashBalanceRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
    }
}
