using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

public readonly record struct KillFeedTopEntry(string Name, byte Tribe, int Kills)
{

        public static readonly KillFeedTopEntry Blank = new(" ", 0, 0);
}

public readonly record struct KillFeedBroadcastPayload(
    string KillerName,
    byte KillerTribe,
    string VictimName,
    byte VictimTribe,
    ImmutableArray<KillFeedTopEntry> TopThree)
{

        public static KillFeedBroadcastPayload Create(string killerName, byte killerTribe, string victimName,
        byte victimTribe, ImmutableArray<KillFeedRankedEntry> topThree)
    {
        var builder = ImmutableArray.CreateBuilder<KillFeedTopEntry>(3);
        for (var i = 0; i < 3; i++)
            builder.Add(i < topThree.Length ? topThree[i].ToTopEntry() : KillFeedTopEntry.Blank);

        return new KillFeedBroadcastPayload(killerName, killerTribe, victimName, victimTribe,
            builder.MoveToImmutable());
    }
}
