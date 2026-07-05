using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Login;

namespace Fenrir.Contracts.Tests.Packets.Login;

public class LcRecommandWorld2RecvTests
{
    [Fact]
    public void PayloadSize_MatchesContractConstant()
    {
        // ExpectedSize=13 (1-byte outbound header) -> 12-byte payload (3 int), same struct as op 24 in LOGIN.h.
        Assert.Equal(12, WorldRecommendationFinalResponse.PayloadSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields_NoObfuscation()
    {
        var packet = new WorldRecommendationFinalResponse
            { AddKillOtherTribe0 = 4, AddKillOtherTribe1 = 5, AddKillOtherTribe2 = 6 };

        var buffer = new byte[WorldRecommendationFinalResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(WorldRecommendationFinalResponse.PayloadSize, written);
        Assert.Equal(packet.AddKillOtherTribe0, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0, 4)));
        Assert.Equal(packet.AddKillOtherTribe1, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4, 4)));
        Assert.Equal(packet.AddKillOtherTribe2, BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(8, 4)));
    }
}
