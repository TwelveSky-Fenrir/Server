using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcChangeAvatarNameRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=5 (1-byte outbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, RenameAvatarResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        // 102 is the legacy "update failure" result code.
        var packet = new RenameAvatarResponse { Result = 102 };

        var buffer = new byte[RenameAvatarResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(RenameAvatarResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
