using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzFishingRewardSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, FishingCatchRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FishingCatch, FishingCatchRequest.Opcode);
    }
}
