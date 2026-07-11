using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class GmTargetNamePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(13, GmTargetNamePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GmTargetNamePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GmTargetNamePayload.WireSize, written);

        Assert.True(GmTargetNamePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[13];
        value.Write(actual);

        var expected = new byte[13];
        Encoding.Latin1.GetBytes(value.TargetName, expected);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmTargetNamePayload.TryRead(new byte[12], out _));
    }

    [Fact]
    public void TryRead_DecodesFromFirst13BytesOfLargerBuffer()
    {
        var data = new byte[130];
        Encoding.Latin1.GetBytes("Griefer", data.AsSpan(0, 13));

        Assert.True(GmTargetNamePayload.TryRead(data, out var payload));
        Assert.Equal("Griefer", payload.TargetName);
    }

    private static GmTargetNamePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmTargetNamePayload { TargetName = v.NextString(13) };
    }
}
