using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

/// <summary>GUILD_KICK_CRECV (13 bytes, STRUCT.h:1158-1161) -- CZ_GUILD_WORK_SEND tSort 8.</summary>
public class GuildWorkKickPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(13, GuildWorkKickPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkKickPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkKickPayload.WireSize, written);

        Assert.True(GuildWorkKickPayload.TryRead(buffer, out var roundTripped));
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
        Assert.False(GuildWorkKickPayload.TryRead(new byte[12], out _));
    }

    private static GuildWorkKickPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkKickPayload { AvatarName = v.NextString(13) };
    }
}
