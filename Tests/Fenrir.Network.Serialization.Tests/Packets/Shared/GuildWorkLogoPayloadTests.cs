using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_GUILD_WORK_SEND tSort 1001.
public class GuildWorkLogoPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(4, GuildWorkLogoPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkLogoPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkLogoPayload.WireSize, written);

        Assert.True(GuildWorkLogoPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, value.Value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkLogoPayload.TryRead(new byte[3], out _));
    }

    private static GuildWorkLogoPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkLogoPayload { Value = v.NextInt() };
    }
}
