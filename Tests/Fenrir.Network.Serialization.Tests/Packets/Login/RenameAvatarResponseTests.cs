using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcChangeAvatarNameRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(4, RenameAvatarResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new RenameAvatarResponse { Result = 102 };

        var buffer = new byte[RenameAvatarResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(RenameAvatarResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
