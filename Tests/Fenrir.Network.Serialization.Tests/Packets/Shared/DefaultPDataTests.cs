using System.Buffers.Binary;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ DEFAULT_PDATA_RECV layout, independent of the generated Write.
public class DefaultPDataTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(28, DefaultPData.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[DefaultPData.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(DefaultPData.WireSize, written);

        Assert.True(DefaultPData.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[28];
        value.Write(actual);

        var expected = new byte[28];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[28];
        EncodeGolden(golden, value);

        Assert.True(DefaultPData.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(DefaultPData.TryRead(new byte[27], out _));
    }

    private static DefaultPData CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new DefaultPData
        {
            Page1 = v.NextInt(),
            Index1 = v.NextInt(),
            Quantity1 = v.NextInt(),
            Page2 = v.NextInt(),
            Index2 = v.NextInt(),
            XPost2 = v.NextInt(),
            YPost2 = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, DefaultPData value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Page1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Index1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], value.Quantity1);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], value.Page2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.Index2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[20..], value.XPost2);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], value.YPost2);
    }
}
