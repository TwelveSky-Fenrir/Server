using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

// CZ_GUILD_WORK_SEND tSort 14.
public class GuildWorkBuffPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(4, GuildWorkBuffPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkBuffPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkBuffPayload.WireSize, written);

        Assert.True(GuildWorkBuffPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, value.GuildBuffType);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkBuffPayload.TryRead(new byte[3], out _));
    }

    private static GuildWorkBuffPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkBuffPayload { GuildBuffType = v.NextInt() };
    }
}
