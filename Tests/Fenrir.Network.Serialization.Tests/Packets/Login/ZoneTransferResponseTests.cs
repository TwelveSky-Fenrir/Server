using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcDemandZoneServerInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        Assert.Equal(28, ZoneTransferResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        var packet = new ZoneTransferResponse
        {
            Result = 1,
            Ip = "127.0.0.1",
            Port = 9081,
            Zone = 3
        };

        var buffer = new byte[ZoneTransferResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZoneTransferResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
        Assert.Equal(packet.Ip, DecodeFixedString(buffer.AsSpan(4, 16)));
        Assert.Equal(packet.Port, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(20, 4)));
        Assert.Equal(packet.Zone, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(24, 4)));
    }

    private static string DecodeFixedString(ReadOnlySpan<byte> span)
    {
        var nullIndex = span.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nullIndex < 0 ? span : span[..nullIndex]);
    }
}
