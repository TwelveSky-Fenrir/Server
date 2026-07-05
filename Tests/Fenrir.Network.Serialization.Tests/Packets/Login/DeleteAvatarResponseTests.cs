using System.Buffers.Binary;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcDeleteAvatarRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=5 (1-byte outbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, DeleteAvatarResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new DeleteAvatarResponse { Result = 1 };

        var buffer = new byte[DeleteAvatarResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(DeleteAvatarResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
