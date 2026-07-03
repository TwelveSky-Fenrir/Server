using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class MissionDateTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(16, MissionDate.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var missionDate = new MissionDate
        {
            JoinWar = v.NextInt(),
            KillOtherTribe = v.NextInt(),
            KillMonster = v.NextInt(),
            PlayTime = v.NextInt()
        };

        var buffer = new byte[MissionDate.WireSize];
        var written = missionDate.Write(buffer);
        Assert.Equal(MissionDate.WireSize, written);

        Assert.True(MissionDate.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(missionDate, roundTripped);
    }
}
