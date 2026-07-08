using System.Buffers.Binary;
using Fenrir.Network.Serialization.Login.Packets.Login;

namespace Fenrir.Network.Serialization.Tests.Packets.Login;

public class LcRecommandWorldRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=13 (1-byte header) -> 12-byte payload (3 int), LOGIN.h.
        Assert.Equal(12, WorldRecommendationResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        // Non-zero values just pin field order; the legacy always sends three zeros.
        var packet = new WorldRecommendationResponse
            { AddKillOtherTribe0 = 1, AddKillOtherTribe1 = 2, AddKillOtherTribe2 = 3 };

        var buffer = new byte[WorldRecommendationResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(WorldRecommendationResponse.PayloadSize, written);
        Assert.Equal(packet.AddKillOtherTribe0, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
        Assert.Equal(packet.AddKillOtherTribe1, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4, 4)));
        Assert.Equal(packet.AddKillOtherTribe2, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(8, 4)));
    }
}
