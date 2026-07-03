using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

/// <summary>
///     OBJECT_FOR_ITEM (84 bytes, STRUCT.h:941-955) — Index/Quantity/Value/SerialNumber/Location[3]/
///     Master[13]/PartyName[13]/(2-byte natural padding)/DropSort/CreateTime/PresentTime/CreateState/
///     SocketGem[3]. The golden encoder below is hand-built from the C++ layout, independent of the
///     generated <c>Write</c>, and explicitly asserts the 2 padding bytes at offset 54..55 are zero.
/// </summary>
public class ObjectForItemTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(84, ObjectForItem.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[ObjectForItem.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(ObjectForItem.WireSize, written);

        Assert.True(ObjectForItem.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[84];
        value.Write(actual);

        var expected = new byte[84];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Write_ZeroesPaddingAfterPartyName()
    {
        var value = CreatePopulated();

        var actual = new byte[84];
        value.Write(actual);

        Assert.Equal(0, actual[54]);
        Assert.Equal(0, actual[55]);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[84];
        EncodeGolden(golden, value);

        Assert.True(ObjectForItem.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ObjectForItem.TryRead(new byte[83], out _));
    }

    private static ObjectForItem CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ObjectForItem
        {
            Index = v.NextInt(),
            Quantity = v.NextInt(),
            Value = v.NextInt(),
            SerialNumber = v.NextInt(),
            Location = v.NextFloatArray(3),
            Master = v.NextString(13),
            PartyName = v.NextString(13),
            DropSort = v.NextInt(),
            CreateTime = v.NextUInt(),
            PresentTime = v.NextUInt(),
            CreateState = v.NextInt(),
            SocketGem = v.NextIntArray(3)
        };
    }

    private static void EncodeGolden(Span<byte> destination, ObjectForItem value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Index);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Quantity);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Value);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.SerialNumber);
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteSingleLittleEndian(destination[(16 + i * 4)..], value.Location[i]);
        Encoding.Latin1.GetBytes(value.Master, destination.Slice(28, 13));
        Encoding.Latin1.GetBytes(value.PartyName, destination.Slice(41, 13));
        destination.Slice(54, 2).Clear();
        BinaryPrimitives.WriteInt32LittleEndian(destination[56..], value.DropSort);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[60..], value.CreateTime);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[64..], value.PresentTime);
        BinaryPrimitives.WriteInt32LittleEndian(destination[68..], value.CreateState);
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(72 + i * 4)..], value.SocketGem[i]);
    }
}
