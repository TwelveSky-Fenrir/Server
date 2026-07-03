using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzMissionCompleteSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzMissionCompleteSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.MissionCompleteSend, CzMissionCompleteSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzMissionCompleteSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);

        var ok = CzMissionCompleteSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzMissionCompleteSend.TryRead(new byte[3], out _));
    }
}
