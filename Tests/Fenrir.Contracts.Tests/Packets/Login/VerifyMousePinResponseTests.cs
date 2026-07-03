using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcLoginMousePasswordRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=5 (1-byte outbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, VerifyMousePinResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new VerifyMousePinResponse { Result = 1 };

        var buffer = new byte[VerifyMousePinResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(VerifyMousePinResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
