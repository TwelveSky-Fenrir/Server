using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcLoginMousePasswordRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
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
