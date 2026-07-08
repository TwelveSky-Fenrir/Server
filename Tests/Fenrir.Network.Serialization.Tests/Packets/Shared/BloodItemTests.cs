using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// Field order matters: Price comes before Quantity here, unlike ProxyShopItem.
public class BloodItemTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(12, BloodItem.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[BloodItem.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(BloodItem.WireSize, written);

        Assert.True(BloodItem.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes_WithPriceBeforeQuantity()
    {
        var value = new BloodItem { ItemId = 111, Price = 222, Quantity = 333 };

        var actual = new byte[12];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 111);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 222);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 333);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[12];
        EncodeGolden(golden, value);

        Assert.True(BloodItem.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(BloodItem.TryRead(new byte[11], out _));
    }

    private static BloodItem CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new BloodItem
        {
            ItemId = v.NextInt(),
            Price = v.NextInt(),
            Quantity = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, BloodItem value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.ItemId);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Price);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Quantity);
    }
}
