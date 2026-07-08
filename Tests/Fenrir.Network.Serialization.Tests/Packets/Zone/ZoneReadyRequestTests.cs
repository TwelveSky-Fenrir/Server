using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzClientOkForZoneSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, ZoneReadyRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ZoneReady, ZoneReadyRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[ZoneReadyRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer[..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 111);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 222);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[12..], 1);

        var ok = ZoneReadyRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Tribe);
        Assert.Equal(111, packet.AutoTime);
        Assert.Equal(222, packet.AutoTime2);
        Assert.Equal(1, packet.AutoState);
    }
}
