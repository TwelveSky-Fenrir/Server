using System.Text;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class ClCreateMousePasswordSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=14 (9-byte inbound header) -> 5-byte payload (char[5], MAX_MOUSE_PASSWORD_LENGTH).
        Assert.Equal(5, ClCreateMousePasswordSend.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ClCreateMousePasswordSend.PayloadSize];
        WriteFixedString(buffer.AsSpan(0, 5), "1234");

        Assert.True(ClCreateMousePasswordSend.TryRead(buffer, out var packet));

        Assert.Equal("1234", packet.MousePassword);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ClCreateMousePasswordSend.PayloadSize - 1];

        Assert.False(ClCreateMousePasswordSend.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
