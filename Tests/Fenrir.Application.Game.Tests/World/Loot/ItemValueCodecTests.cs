using Fenrir.Application.Game.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class ItemValueCodecTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(40, 0, 0, 0)]
    [InlineData(1, 2, 3, 4)]
    [InlineData(255, 255, 255, 255)]
    [InlineData(0, 0, 0, 255)]
    public void EncodeThenDecode_RoundTrips(byte enchant, byte combine, byte refine, byte socket)
    {
        var value = ItemValueCodec.Encode(enchant, combine, refine, socket);
        var (decodedEnchant, decodedCombine, decodedRefine, decodedSocket) = ItemValueCodec.Decode(value);

        Assert.Equal(enchant, decodedEnchant);
        Assert.Equal(combine, decodedCombine);
        Assert.Equal(refine, decodedRefine);
        Assert.Equal(socket, decodedSocket);
    }

    [Fact]
    public void Encode_MatchesLittleEndianByteLayout()
    {
        // Server/Header/function.h's SetISIUIMValue: byte0=IS/Enchant, byte1=IU/Combine, byte2=IM/Refine, byte3=IZ/Socket.
        var value = ItemValueCodec.Encode(1, 2, 3, 4);

        Assert.Equal(1 | (2 << 8) | (3 << 16) | (4 << 24), value);
    }

    [Fact]
    public void Decode_Zero_IsAllZeroBytes()
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(0);

        Assert.Equal(0, enchant);
        Assert.Equal(0, combine);
        Assert.Equal(0, refine);
        Assert.Equal(0, socket);
    }
}
