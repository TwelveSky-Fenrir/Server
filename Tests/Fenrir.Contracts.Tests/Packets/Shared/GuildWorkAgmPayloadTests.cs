using System.Buffers.Binary;
using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

/// <summary>GUILD_MAKE_AGM_CRECV (17 bytes, STRUCT.h:1182-1186, NO padding) -- CZ_GUILD_WORK_SEND tSort 9.</summary>
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
