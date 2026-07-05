using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTribeBankSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, TribeBankRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeBank, TribeBankRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[TribeBankRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 17);

        var ok = TribeBankRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
        Assert.Equal(17, packet.Value);
    }
}
