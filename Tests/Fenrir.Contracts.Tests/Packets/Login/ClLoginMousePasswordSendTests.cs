using System.Text;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class ClLoginMousePasswordSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=14 (9-byte inbound header) -> 5-byte payload (char[5]).
        Assert.Equal(5, ClLoginMousePasswordSend.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ClLoginMousePasswordSend.PayloadSize];
        WriteFixedString(buffer.AsSpan(0, 5), "0042");

        Assert.True(ClLoginMousePasswordSend.TryRead(buffer, out var packet));

        Assert.Equal("0042", packet.MousePasswordInput);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ClLoginMousePasswordSend.PayloadSize - 1];

        Assert.False(ClLoginMousePasswordSend.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
