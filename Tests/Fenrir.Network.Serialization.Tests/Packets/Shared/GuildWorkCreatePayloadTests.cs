using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class GuildWorkCreatePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(13, GuildWorkCreatePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkCreatePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkCreatePayload.WireSize, written);

        Assert.True(GuildWorkCreatePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[13];
        value.Write(actual);

        var expected = new byte[13];
        Encoding.Latin1.GetBytes(value.GuildName, expected);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkCreatePayload.TryRead(new byte[12], out _));
    }

    private static GuildWorkCreatePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkCreatePayload { GuildName = v.NextString(13) };
    }
}
