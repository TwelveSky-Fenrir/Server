using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_GUILD_WORK_SEND tSort 5.
public class GuildWorkNoticePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(204, GuildWorkNoticePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkNoticePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkNoticePayload.WireSize, written);

        Assert.True(GuildWorkNoticePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[204];
        value.Write(actual);

        var expected = new byte[204];
        for (var i = 0; i < 4; i++)
            Encoding.Latin1.GetBytes(value.Notices[i], expected.AsSpan(i * 51, 51));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkNoticePayload.TryRead(new byte[203], out _));
    }

    private static GuildWorkNoticePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkNoticePayload { Notices = v.NextStringArray(4, 51) };
    }
}
