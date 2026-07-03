using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

/// <summary>
///     PROXY_STATE_INFO (STRUCT.h:1734-1740) — C++ <c>sizeof</c> is 52 bytes, but this wire type only
///     covers the 3 real fields (50 bytes): Location[3]/Name[13]/PshopName[25]. The trailing 2-byte
///     queue padding is NOT part of this type — it is the responsibility of the parent packet's
///     <c>[Reserved(2)]</c> attribute on the field that follows the embedded struct (documented on the
///     type itself). The golden encoder below is hand-built from the C++ layout, independent of the
///     generated <c>Write</c>.
/// </summary>
public class ProxyStateInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(50, ProxyStateInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[ProxyStateInfo.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(ProxyStateInfo.WireSize, written);

        Assert.True(ProxyStateInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[50];
        value.Write(actual);

        var expected = new byte[50];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[50];
        EncodeGolden(golden, value);

        Assert.True(ProxyStateInfo.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ProxyStateInfo.TryRead(new byte[49], out _));
    }

    private static ProxyStateInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ProxyStateInfo
        {
            Location = v.NextFloatArray(3),
            Name = v.NextString(13),
            PshopName = v.NextString(25)
        };
    }

    private static void EncodeGolden(Span<byte> destination, ProxyStateInfo value)
    {
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteSingleLittleEndian(destination[(i * 4)..], value.Location[i]);
        Encoding.Latin1.GetBytes(value.Name, destination.Slice(12, 13));
        Encoding.Latin1.GetBytes(value.PshopName, destination.Slice(25, 25));
    }
}
