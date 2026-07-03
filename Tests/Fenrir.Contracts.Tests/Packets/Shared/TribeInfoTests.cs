using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class TribeInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(2456, TribeInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var tribeInfo = new TribeInfo
        {
            TribeVoteName = v.NextStringArray(40, 13),
            TribeVoteLevel = v.NextIntArray(40),
            TribeVoteKillOtherTribe = v.NextIntArray(40),
            TribeVotePoint = v.NextIntArray(40),
            TribeMaster = v.NextStringArray(4, 13),
            TribeSubMaster = v.NextStringArray(48, 13),
            HoisundoName1 = v.NextStringArray(20, 13),
            HoisundoName2 = v.NextStringArray(20, 13),
            HoisundoName3 = v.NextStringArray(20, 13)
        };

        var buffer = new byte[TribeInfo.WireSize];
        var written = tribeInfo.Write(buffer);
        Assert.Equal(TribeInfo.WireSize, written);

        Assert.True(TribeInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(tribeInfo, roundTripped);
    }
}
