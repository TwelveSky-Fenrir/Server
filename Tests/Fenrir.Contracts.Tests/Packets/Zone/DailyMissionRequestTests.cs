using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzMissionCompleteSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, DailyMissionRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DailyMission, DailyMissionRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[DailyMissionRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);

        var ok = DailyMissionRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(DailyMissionRequest.TryRead(new byte[3], out _));
    }
}
