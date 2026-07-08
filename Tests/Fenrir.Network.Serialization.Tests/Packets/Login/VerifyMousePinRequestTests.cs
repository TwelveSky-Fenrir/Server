using System.Text;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class ClLoginMousePasswordSendTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=14 (9-byte inbound header) -> 5-byte payload (char[5]).
        Assert.Equal(5, VerifyMousePinRequest.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var buffer = new byte[VerifyMousePinRequest.PayloadSize];
        WriteFixedString(buffer.AsSpan(0, 5), "0042");

        Assert.True(VerifyMousePinRequest.TryRead(buffer, out var packet));

        Assert.Equal("0042", packet.MousePasswordInput);
    }

    [Fact]
    public void TryRead_BufferTooShort_Fails()
    {
        var buffer = new byte[VerifyMousePinRequest.PayloadSize - 1];

        Assert.False(VerifyMousePinRequest.TryRead(buffer, out _));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.Latin1.GetBytes(value, destination);
    }
}
