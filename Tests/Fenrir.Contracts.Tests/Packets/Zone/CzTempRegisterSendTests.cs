using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTempRegisterSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(263, CzTempRegisterSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TempRegisterSend, CzTempRegisterSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = new byte[CzTempRegisterSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..255], "Freya");
        BinaryPrimitives.WriteInt32LittleEndian(buffer[255..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[259..], 7);

        var ok = CzTempRegisterSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Freya", packet.Id);
        Assert.Equal(2, packet.Tribe);
        Assert.Equal(7, packet.UserSort);
    }
}
