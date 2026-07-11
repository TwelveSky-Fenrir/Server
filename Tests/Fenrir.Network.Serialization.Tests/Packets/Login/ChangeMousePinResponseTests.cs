using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcChangeMousePasswordRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(9, ChangeMousePinResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
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
