using System.Buffers.Binary;
using System.Text;
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

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[TribeInfo.WireSize];
        Encoding.Latin1.GetBytes("Vote1", buffer.AsSpan(0, 13));
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(520 + 5 * 4, 4), 42);
        Encoding.Latin1.GetBytes("Chief2", buffer.AsSpan(1000 + 2 * 13, 13));
        Encoding.Latin1.GetBytes("Last19", buffer.AsSpan(2196 + 19 * 13, 13));

        var ok = TribeInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Vote1", packet.TribeVoteName[0]);
        Assert.Equal(42, packet.TribeVoteLevel[5]);
        Assert.Equal("Chief2", packet.TribeMaster[2]);
        Assert.Equal("Last19", packet.HoisundoName3[19]);
    }
}
