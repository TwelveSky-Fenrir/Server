using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_TRIBE_WORK_SEND tSort 2/3.
public class TribeWorkNamePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(13, TribeWorkNamePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[TribeWorkNamePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(TribeWorkNamePayload.WireSize, written);

        Assert.True(TribeWorkNamePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[13];
        value.Write(actual);

        var expected = new byte[13];
        Encoding.Latin1.GetBytes(value.AvatarName, expected);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribeWorkNamePayload.TryRead(new byte[12], out _));
    }

    private static TribeWorkNamePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new TribeWorkNamePayload { AvatarName = v.NextString(13) };
    }
}
