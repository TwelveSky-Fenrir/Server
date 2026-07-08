using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_GUILD_WORK_SEND tSort 10.
public class GuildWorkTitlePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(18, GuildWorkTitlePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkTitlePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkTitlePayload.WireSize, written);

        Assert.True(GuildWorkTitlePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[18];
        value.Write(actual);

        var expected = new byte[18];
        Encoding.Latin1.GetBytes(value.AvatarName, expected.AsSpan(0, 13));
        Encoding.Latin1.GetBytes(value.CallName, expected.AsSpan(13, 5));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkTitlePayload.TryRead(new byte[17], out _));
    }

    private static GuildWorkTitlePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkTitlePayload { AvatarName = v.NextString(13), CallName = v.NextString(5) };
    }
}
