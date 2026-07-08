using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzChangeAutoInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, AutoPotionThresholdRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.AutoPotionThreshold, AutoPotionThresholdRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[AutoPotionThresholdRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 4);

        var ok = AutoPotionThresholdRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Value01);
        Assert.Equal(4, packet.Value02);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(AutoPotionThresholdRequest.TryRead(new byte[7], out _));
    }
}
