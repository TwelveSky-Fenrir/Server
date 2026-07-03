using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

/// <summary>
///     PSHOP_INFO (1232 bytes, STRUCT.h:703-709) — UniqueNumber/Name[25]/(3-byte natural padding)/
///     ItemInfo[225]/SocketInfo[75]. The golden encoder below is hand-built from the C++ layout
///     (UniqueNumber 0..3, Name 4..28, padding 29..31, ItemInfo 32..931, SocketInfo 932..1231),
///     independent of the generated <c>Write</c>, and explicitly asserts the 3 padding bytes after
///     <c>Name</c> are zero.
/// </summary>
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
