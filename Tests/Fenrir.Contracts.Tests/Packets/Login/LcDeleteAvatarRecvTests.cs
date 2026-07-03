using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcDeleteAvatarRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=5 (1-byte outbound header) -> 4-byte payload (1 int).
        Assert.Equal(4, LcDeleteAvatarRecv.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new LcDeleteAvatarRecv { Result = 1 };

        var buffer = new byte[LcDeleteAvatarRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LcDeleteAvatarRecv.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
