using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_LASTHERORANK_INFO_RECV — same layout as ZC 148 (Result @0, HeroRank @4), distinct opcode.
/// </summary>
public class ZcLastHeroRankInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(684, ZcLastHeroRankInfoRecv.PayloadSize);
        Assert.Equal(4 + HeroRank.WireSize, ZcLastHeroRankInfoRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.LastHeroRankInfoRecv, ZcLastHeroRankInfoRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[684];
        value.Write(actual);

        var expected = new byte[684];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcLastHeroRankInfoRecv CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ZcLastHeroRankInfoRecv
        {
            Result = v.NextInt(),
            HeroInfo = new HeroRank
            {
                Name = v.NextStringArray(40, 13),
                Point = v.NextIntArray(40)
            }
        };
    }

    private static void EncodeGolden(Span<byte> destination, ZcLastHeroRankInfoRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);

        var heroInfo = destination[4..];
        for (var i = 0; i < 40; i++)
            Encoding.Latin1.GetBytes(value.HeroInfo.Name[i], heroInfo.Slice(i * 13, 13));
        for (var i = 0; i < 40; i++)
            BinaryPrimitives.WriteInt32LittleEndian(heroInfo[(520 + i * 4)..], value.HeroInfo.Point[i]);
    }
}
