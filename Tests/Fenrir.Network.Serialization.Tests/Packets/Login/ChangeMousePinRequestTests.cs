using System.Text;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClChangeMousePasswordSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(10, ChangeMousePinRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[ChangeMousePinRequest.PayloadSize];
        WriteFixedString(buffer.AsSpan(0, 5), "1234");
        WriteFixedString(buffer.AsSpan(5, 5), "5678");

        Assert.True(ChangeMousePinRequest.TryRead(buffer, out var packet));

        Assert.Equal("1234", packet.MousePassword);
        Assert.Equal("5678", packet.ChangeMousePassword);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[ChangeMousePinRequest.PayloadSize - 1];

        Assert.False(ChangeMousePinRequest.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
