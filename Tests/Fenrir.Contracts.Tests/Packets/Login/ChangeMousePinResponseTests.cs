using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcChangeMousePasswordRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=10 (1-byte header) -> 9-byte payload; same struct as op13 in LOGIN.h.
        Assert.Equal(9, ChangeMousePinResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        // Result=1 + "0000" mask is the legacy old-PIN-mismatch reply shape (S04_MyWork02.cpp).
        var packet = new ChangeMousePinResponse { Result = 1, MousePassword = "0000" };

        var buffer = new byte[ChangeMousePinResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ChangeMousePinResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
        Assert.Equal(packet.MousePassword, DecodeFixedString(buffer.AsSpan(4, 5)));
    }

    private static string DecodeFixedString(ReadOnlySpan<byte> span)
    {
        var nullIndex = span.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nullIndex < 0 ? span : span[..nullIndex]);
    }
}
