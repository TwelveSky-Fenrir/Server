using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// Golden encoder is hand-built from the C++ PSHOP_INFO layout, independent of the generated Write.
public class PshopInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(1232, PshopInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[PshopInfo.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(PshopInfo.WireSize, written);

        Assert.True(PshopInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[1232];
        value.Write(actual);

        var expected = new byte[1232];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Write_ZeroesPaddingAfterName()
    {
        var value = CreatePopulated();

        var actual = new byte[1232];
        value.Write(actual);

        Assert.Equal(0, actual[29]);
        Assert.Equal(0, actual[30]);
        Assert.Equal(0, actual[31]);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[1232];
        EncodeGolden(golden, value);

        Assert.True(PshopInfo.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(PshopInfo.TryRead(new byte[1231], out _));
    }

    private static PshopInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new PshopInfo
        {
            UniqueNumber = v.NextUInt(),
            Name = v.NextString(25),
            ItemInfo = v.NextIntArray(225),
            SocketInfo = v.NextIntArray(75)
        };
    }

    private static void EncodeGolden(Span<byte> destination, PshopInfo value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, value.UniqueNumber);
        Encoding.Latin1.GetBytes(value.Name, destination.Slice(4, 25));
        destination.Slice(29, 3).Clear();
        for (var i = 0; i < 225; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(32 + i * 4)..], value.ItemInfo[i]);
        for (var i = 0; i < 75; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(932 + i * 4)..], value.SocketInfo[i]);
    }
}
