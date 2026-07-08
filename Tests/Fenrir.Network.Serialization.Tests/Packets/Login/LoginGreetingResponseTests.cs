using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcLoginConnectOkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=37 (1-byte header + 20-byte padding + 3 int) -> 36-byte payload.
        Assert.Equal(36, LoginGreetingResponse.PayloadSize);
    }

    [Fact]
    public void Write_ReservedPadding_IsZeroed()
    {
        var packet = new LoginGreetingResponse
        {
            RandomNumber = 0x11223344,
            MaxPlayerNum = 500,
            GagePlayerNum = 42,
            PresentPlayerNum = 7
        };

        var buffer = new byte[LoginGreetingResponse.PayloadSize];
        packet.Write(buffer);

        Assert.All(buffer.AsSpan(0, 20).ToArray(), b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var packet = new LoginGreetingResponse
        {
            RandomNumber = 0x11223344,
            MaxPlayerNum = 500,
            GagePlayerNum = 42,
            PresentPlayerNum = 7
        };

        var buffer = new byte[LoginGreetingResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LoginGreetingResponse.PayloadSize, written);

        var randomNumber = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(20, 4));
        var maxPlayerNum = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(24, 4));
        var gagePlayerNum = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(28, 4));
        var presentPlayerNum = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(32, 4));

        Assert.Equal(packet.RandomNumber, randomNumber);
        Assert.Equal(packet.MaxPlayerNum, maxPlayerNum);
        Assert.Equal(packet.GagePlayerNum, gagePlayerNum);
        Assert.Equal(packet.PresentPlayerNum, presentPlayerNum);
    }
}
