using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

// C++ sizeof is 52, but WireSize=50: the trailing 2-byte padding belongs to the parent packet's [Reserved(2)], not this type.
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
