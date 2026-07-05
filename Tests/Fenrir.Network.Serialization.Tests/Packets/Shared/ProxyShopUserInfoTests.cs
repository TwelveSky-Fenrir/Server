using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ PROXY_SHOP_USER_INFO layout, independent of the generated Write.
public class ProxyShopUserInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(824, ProxyShopUserInfo.WireSize);
        Assert.Equal(20, ProxyShopItem.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[ProxyShopUserInfo.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(ProxyShopUserInfo.WireSize, written);

        Assert.True(ProxyShopUserInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ProxyShopUserInfo.WireSize];
        value.Write(actual);

        var expected = new byte[824];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[824];
        EncodeGolden(golden, value);

        Assert.True(ProxyShopUserInfo.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ProxyShopUserInfo.TryRead(new byte[823], out _));
    }

    private static ProxyShopUserInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        var items = new ProxyShopItem[25];
        for (var i = 0; i < items.Length; i++)
            items[i] = new ProxyShopItem
            {
                Id = v.NextInt(),
                Quantity = v.NextInt(),
                Value = v.NextInt(),
                Serial = v.NextInt(),
                Price = v.NextInt()
            };

        return new ProxyShopUserInfo
        {
            AvatarName = v.NextString(13),
            Items = items,
            Sockets = v.NextIntArray(75),
            Money = v.NextInt(),
            BigMoney = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, ProxyShopUserInfo value)
    {
        Encoding.Latin1.GetBytes(value.AvatarName, destination[..13]);

        for (var i = 0; i < 25; i++)
        {
            var itemOffset = 16 + i * 20;
            BinaryPrimitives.WriteInt32LittleEndian(destination[itemOffset..], value.Items[i].Id);
            BinaryPrimitives.WriteInt32LittleEndian(destination[(itemOffset + 4)..], value.Items[i].Quantity);
            BinaryPrimitives.WriteInt32LittleEndian(destination[(itemOffset + 8)..], value.Items[i].Value);
            BinaryPrimitives.WriteInt32LittleEndian(destination[(itemOffset + 12)..], value.Items[i].Serial);
            BinaryPrimitives.WriteInt32LittleEndian(destination[(itemOffset + 16)..], value.Items[i].Price);
        }

        for (var i = 0; i < 75; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(516 + i * 4)..], value.Sockets[i]);

        BinaryPrimitives.WriteInt32LittleEndian(destination[816..], value.Money);
        BinaryPrimitives.WriteInt32LittleEndian(destination[820..], value.BigMoney);
    }
}
