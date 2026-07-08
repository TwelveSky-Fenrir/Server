using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcWantGiftRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=5 (1-byte outbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, ClaimGiftResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new ClaimGiftResponse { Result = 1 };

        var buffer = new byte[ClaimGiftResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ClaimGiftResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
