using System.Text;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

/// <summary>GUILD_TRANSFER_LEADER_CRECV (26 bytes, STRUCT.h:1203-1207) -- CZ_GUILD_WORK_SEND tSort 17.</summary>
public class GuildWorkTransferPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(26, GuildWorkTransferPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GuildWorkTransferPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GuildWorkTransferPayload.WireSize, written);

        Assert.True(GuildWorkTransferPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[26];
        value.Write(actual);

        var expected = new byte[26];
        Encoding.Latin1.GetBytes(value.NewMasterName, expected.AsSpan(0, 13));
        Encoding.Latin1.GetBytes(value.OldMasterName, expected.AsSpan(13, 13));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildWorkTransferPayload.TryRead(new byte[25], out _));
    }

    private static GuildWorkTransferPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GuildWorkTransferPayload { NewMasterName = v.NextString(13), OldMasterName = v.NextString(13) };
    }
}
