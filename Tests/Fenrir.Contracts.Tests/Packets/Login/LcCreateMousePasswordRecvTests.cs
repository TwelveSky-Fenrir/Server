using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcCreateMousePasswordRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=10 (1-byte outbound header) -> 9-byte payload (int + char[5]).
        Assert.Equal(9, LcCreateMousePasswordRecv.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        var packet = new LcCreateMousePasswordRecv { Result = 0, MousePassword = "1234" };

        var buffer = new byte[LcCreateMousePasswordRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LcCreateMousePasswordRecv.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
        Assert.Equal(packet.MousePassword, DecodeFixedString(buffer.AsSpan(4, 5)));
    }

    private static string DecodeFixedString(ReadOnlySpan<byte> span)
    {
        var nullIndex = span.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nullIndex < 0 ? span : span[..nullIndex]);
    }
}
