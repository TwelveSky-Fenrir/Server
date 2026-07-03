using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcRecommandWorldRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=13 (1-byte outbound header) -> 12-byte payload (3 int), LOGIN.h l.216-222.
        Assert.Equal(12, LcRecommandWorldRecv.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        // Non-zero values only to pin the field ORDER; the legacy always sends three zeros (report §5.24).
        var packet = new LcRecommandWorldRecv
            { AddKillOtherTribe0 = 1, AddKillOtherTribe1 = 2, AddKillOtherTribe2 = 3 };

        var buffer = new byte[LcRecommandWorldRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LcRecommandWorldRecv.PayloadSize, written);
        Assert.Equal(packet.AddKillOtherTribe0, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
        Assert.Equal(packet.AddKillOtherTribe1, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4, 4)));
        Assert.Equal(packet.AddKillOtherTribe2, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(8, 4)));
    }
}
