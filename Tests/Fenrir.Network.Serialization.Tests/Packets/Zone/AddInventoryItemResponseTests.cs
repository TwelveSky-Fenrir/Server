using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_ADD_USER_INVENTORY_ITEM_RECV (ZONE.h:1326-1338, 48-byte payload).</summary>
public class ZcAddUserInventoryItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(48, AddInventoryItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AddInventoryItem, AddInventoryItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[AddInventoryItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[48];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static void AssertFieldsEqual(AddInventoryItemResponse expected, AddInventoryItemResponse actual)
    {
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(expected.ItemIndex, actual.ItemIndex);
        Assert.Equal(expected.Page, actual.Page);
        Assert.Equal(expected.Index, actual.Index);
        Assert.Equal(expected.Xy, actual.Xy);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.Serial, actual.Serial);
        Assert.True(expected.Socket.AsSpan().SequenceEqual(actual.Socket));
        Assert.Equal(expected.Expire, actual.Expire);
    }

    private static AddInventoryItemResponse CreatePopulated()
    {
        return new AddInventoryItemResponse
        {
            Result = 11,
            ItemIndex = 22,
            Page = 33,
            Index = 44,
            Xy = 55,
            Quantity = 66,
            Value = 77,
            Serial = 88,
            Socket = [91, 92, 93],
            Expire = 99
        };
    }

    private static void EncodeGolden(Span<byte> destination, AddInventoryItemResponse value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.ItemIndex);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Page);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Xy);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.Quantity);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], value.Value);
        BinaryPrimitives.WriteInt32LittleEndian(destination[28..], value.Serial);
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(32 + i * 4)..], value.Socket[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[44..], value.Expire);
    }
}
