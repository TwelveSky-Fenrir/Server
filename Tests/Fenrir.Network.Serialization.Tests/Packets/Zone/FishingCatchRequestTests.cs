using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFishingRewardSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, FishingCatchRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FishingCatch, FishingCatchRequest.Opcode);
    }
}
