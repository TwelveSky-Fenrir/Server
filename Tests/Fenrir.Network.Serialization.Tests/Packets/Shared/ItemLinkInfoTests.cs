using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class ItemLinkInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(24, ItemLinkInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[ItemLinkInfo.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(ItemLinkInfo.WireSize, written);

        Assert.True(ItemLinkInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[24];
        value.Write(actual);

        var expected = new byte[24];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[24];
        EncodeGolden(golden, value);

        Assert.True(ItemLinkInfo.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ItemLinkInfo.TryRead(new byte[23], out _));
    }

    private static ItemLinkInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ItemLinkInfo
        {
            Index = v.NextInt(),
            Activity = v.NextInt(),
            Value = v.NextInt(),
            Socket = v.NextIntArray(3)
        };
    }

    private static void EncodeGolden(Span<byte> destination, ItemLinkInfo value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Activity);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Value);
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(12 + i * 4)..], value.Socket[i]);
    }
}
