using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcHeroRankInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(684, HeroRankingPreviousResponse.PayloadSize);
        Assert.Equal(4 + HeroRank.WireSize, HeroRankingPreviousResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.HeroRankingPrevious, HeroRankingPreviousResponse.Opcode);
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

    private static HeroRankingPreviousResponse CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new HeroRankingPreviousResponse
        {
            Result = v.NextInt(),
            HeroInfo = new HeroRank
            {
                Name = v.NextStringArray(40, 13),
                Point = v.NextIntArray(40)
            }
        };
    }

    private static void EncodeGolden(Span<byte> destination, HeroRankingPreviousResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);

        var heroInfo = destination[4..];
        for (var i = 0; i < 40; i++)
            Encoding.Latin1.GetBytes(value.HeroInfo.Name[i], heroInfo.Slice(i * 13, 13));
        for (var i = 0; i < 40; i++)
            BinaryPrimitives.WriteInt32LittleEndian(heroInfo[(520 + i * 4)..], value.HeroInfo.Point[i]);
    }
}
