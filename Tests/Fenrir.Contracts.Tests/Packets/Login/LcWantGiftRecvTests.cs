using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcWantGiftRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=5 (1-byte outbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, LcWantGiftRecv.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new LcWantGiftRecv { Result = 1 };

        var buffer = new byte[LcWantGiftRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LcWantGiftRecv.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
