using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ BLOOD_SHOP layout, independent of the generated Write.
public class BloodShopTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(604, BloodShop.WireSize);
        Assert.Equal(12, BloodItem.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[BloodShop.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(BloodShop.WireSize, written);

        Assert.True(BloodShop.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[604];
        value.Write(actual);

        var expected = new byte[604];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[604];
        EncodeGolden(golden, value);

        Assert.True(BloodShop.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(BloodShop.TryRead(new byte[603], out _));
    }

    private static BloodShop CreatePopulated()
    {
        var v = new SequentialValueFactory();
        var items = new BloodItem[50];
        for (var i = 0; i < items.Length; i++)
            items[i] = new BloodItem
            {
                ItemId = v.NextInt(),
                Price = v.NextInt(),
                Quantity = v.NextInt()
            };

        return new BloodShop
        {
            BloodNum = v.NextInt(),
            Data = items
        };
    }

    private static void EncodeGolden(Span<byte> destination, BloodShop value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.BloodNum);
        for (var i = 0; i < 50; i++)
        {
            var itemOffset = 4 + i * 12;
            BinaryPrimitives.WriteInt32LittleEndian(destination[itemOffset..], value.Data[i].ItemId);
            BinaryPrimitives.WriteInt32LittleEndian(destination[(itemOffset + 4)..], value.Data[i].Price);
            BinaryPrimitives.WriteInt32LittleEndian(destination[(itemOffset + 8)..], value.Data[i].Quantity);
        }
    }
}
