using Fenrir.Application.Game.Domain.Crafting;

namespace Fenrir.Application.Game.Tests.Crafting;

public class RuneStoneStatCodecTests
{
    [Fact]
    public void EncodeThenDecode_RoundTrips_PositiveComponents()
    {
        var packed = RuneStoneStatCodec.Encode(10, 20, 30, 5);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(packed);

        Assert.Equal(10, str);
        Assert.Equal(20, dex);
        Assert.Equal(30, vit);
        Assert.Equal(5, intel);
    }

    [Fact]
    public void EncodeThenDecode_RoundTrips_NegativeComponents()
    {
        var packed = RuneStoneStatCodec.Encode(-5, 0, -1, 127);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(packed);

        Assert.Equal(-5, str);
        Assert.Equal(0, dex);
        Assert.Equal(-1, vit);
        Assert.Equal(127, intel);
    }

    [Fact]
    public void Encode_ByteOrder_IsStrDexVitInt_LittleEndian()
    {
        var packed = RuneStoneStatCodec.Encode(1, 2, 3, 4);

        Assert.Equal(1, packed & 0xFF);
        Assert.Equal(2, (packed >> 8) & 0xFF);
        Assert.Equal(3, (packed >> 16) & 0xFF);
        Assert.Equal(4, (packed >> 24) & 0xFF);
    }

    [Fact]
    public void Decode_OfZero_YieldsAllZeroComponents()
    {
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(0);

        Assert.Equal(0, str);
        Assert.Equal(0, dex);
        Assert.Equal(0, vit);
        Assert.Equal(0, intel);
    }
}
