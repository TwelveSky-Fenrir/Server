using System.Buffers.Binary;
using System.Text;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_GUILD_WORK_SEND tSort 9.
public class GuildWorkAgmPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(17, GuildWorkAgmPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkAgmPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkAgmPayload.WireSize, written);

        Assert.True(GuildWorkAgmPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[17];
        value.Write(actual);

        var expected = new byte[17];
        Encoding.Latin1.GetBytes(value.AvatarName, expected.AsSpan(0, 13));
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(13), value.GuildRole);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkAgmPayload.TryRead(new byte[16], out _));
    }

    private static GuildWorkAgmPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkAgmPayload { AvatarName = v.NextString(13), GuildRole = v.NextInt() };
    }
}
