using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGetZoneConnectUserSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, TribePopulationRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribePopulation, TribePopulationRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[TribePopulationRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 349);

        var ok = TribePopulationRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(349, packet.ZoneNumber);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribePopulationRequest.TryRead(new byte[3], out _));
    }
}
