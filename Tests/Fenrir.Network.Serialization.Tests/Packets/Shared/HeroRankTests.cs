using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ HERO_RANK layout, independent of the generated Write.
public class HeroRankTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(680, HeroRank.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[HeroRank.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(HeroRank.WireSize, written);

        Assert.True(HeroRank.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[680];
        value.Write(actual);

        var expected = new byte[680];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[680];
        EncodeGolden(golden, value);

        Assert.True(HeroRank.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(HeroRank.TryRead(new byte[679], out _));
    }

    private static HeroRank CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new HeroRank
        {
            Name = v.NextStringArray(40, 13),
            Point = v.NextIntArray(40)
        };
    }

    private static void EncodeGolden(Span<byte> destination, HeroRank value)
    {
        for (var i = 0; i < 40; i++)
            Encoding.Latin1.GetBytes(value.Name[i], destination.Slice(i * 13, 13));

        for (var i = 0; i < 40; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(520 + i * 4)..], value.Point[i]);
    }
}
