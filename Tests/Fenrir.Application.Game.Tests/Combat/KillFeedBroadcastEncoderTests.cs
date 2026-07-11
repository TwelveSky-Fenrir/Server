using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class KillFeedBroadcastEncoderTests
{
    [Fact]
    public void Encode_ProducesExactlyThePayloadSize()
    {
        var payload = new KillFeedBroadcastPayload("Killer", 1, "Victim", 2,
            [KillFeedTopEntry.Blank, KillFeedTopEntry.Blank, KillFeedTopEntry.Blank]);

        var data = KillFeedBroadcastEncoder.Encode(payload);

        Assert.Equal(KillFeedBroadcastEncoder.PayloadSize, data.Length);
    }

    [Fact]
    public void Encode_WritesKillerAndVictimNamesAndTribes()
    {
        var payload = new KillFeedBroadcastPayload("Killer", 3, "Victim", 4,
            [KillFeedTopEntry.Blank, KillFeedTopEntry.Blank, KillFeedTopEntry.Blank]);

        var data = KillFeedBroadcastEncoder.Encode(payload);

        var killerName = ReadFixedAsciiName(data, 0);
        Assert.Equal("Killer", killerName);
        Assert.Equal(3, data[KillFeedBroadcastEncoder.NameFieldSize]);

        var victimOffset = KillFeedBroadcastEncoder.NameFieldSize + 1;
        var victimName = ReadFixedAsciiName(data, victimOffset);
        Assert.Equal("Victim", victimName);
        Assert.Equal(4, data[victimOffset + KillFeedBroadcastEncoder.NameFieldSize]);
    }

    [Fact]
    public void Encode_WritesTopThreeEntries_NameTribeKills()
    {
        ImmutableArray<KillFeedTopEntry> top3 =
        [
            new KillFeedTopEntry("First", 0, 9),
            new KillFeedTopEntry("Second", 1, 5),
            new KillFeedTopEntry("Third", 2, 1)
        ];

        var payload = new KillFeedBroadcastPayload("Killer", 0, "Victim", 1, top3);
        var data = KillFeedBroadcastEncoder.Encode(payload);

        const int topThreeOffset = 2 * (13 + 1);
        const int entrySize = 13 + 1 + 4;

        for (var i = 0; i < 3; i++)
        {
            var entryOffset = topThreeOffset + i * entrySize;
            var name = ReadFixedAsciiName(data, entryOffset);
            Assert.Equal(top3[i].Name, name);
            Assert.Equal(top3[i].Tribe, data[entryOffset + 13]);

            var kills = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(entryOffset + 14, 4));
            Assert.Equal(top3[i].Kills, kills);
        }
    }

    [Fact]
    public void Encode_FewerThanThreeTopEntries_BlankPadsTheRest()
    {
        ImmutableArray<KillFeedTopEntry> top3 = [new KillFeedTopEntry("Only", 0, 2)];
        var payload = new KillFeedBroadcastPayload("Killer", 0, "Victim", 1, top3);

        var data = KillFeedBroadcastEncoder.Encode(payload);

        const int topThreeOffset = 2 * (13 + 1);
        const int entrySize = 13 + 1 + 4;

        var secondOffset = topThreeOffset + entrySize;
        var secondName = ReadFixedAsciiName(data, secondOffset);
        Assert.Equal(" ", secondName);
        Assert.Equal(0, data[secondOffset + 13]);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(secondOffset + 14, 4)));
    }

    [Fact]
    public void Encode_NameExactlyThirteenBytes_NoTerminatorReserved_NoTruncationException()
    {
        var thirteenCharName = new string('A', 13);
        var payload = new KillFeedBroadcastPayload(thirteenCharName, 0, "Victim", 1,
            [KillFeedTopEntry.Blank, KillFeedTopEntry.Blank, KillFeedTopEntry.Blank]);

        var data = KillFeedBroadcastEncoder.Encode(payload);

        var readBack = Encoding.ASCII.GetString(data, 0, KillFeedBroadcastEncoder.NameFieldSize);
        Assert.Equal(thirteenCharName, readBack);
    }

    [Fact]
    public void Encode_NameLongerThanThirteenBytes_Truncated()
    {
        var longName = new string('B', 20);
        var payload = new KillFeedBroadcastPayload(longName, 0, "Victim", 1,
            [KillFeedTopEntry.Blank, KillFeedTopEntry.Blank, KillFeedTopEntry.Blank]);

        var data = KillFeedBroadcastEncoder.Encode(payload);

        var readBack = Encoding.ASCII.GetString(data, 0, KillFeedBroadcastEncoder.NameFieldSize);
        Assert.Equal(new string('B', 13), readBack);
    }

    private static string ReadFixedAsciiName(byte[] data, int offset)
    {
        var raw = Encoding.ASCII.GetString(data, offset, KillFeedBroadcastEncoder.NameFieldSize);
        var nullIndex = raw.IndexOf('\0');
        return nullIndex >= 0 ? raw[..nullIndex] : raw;
    }
}
