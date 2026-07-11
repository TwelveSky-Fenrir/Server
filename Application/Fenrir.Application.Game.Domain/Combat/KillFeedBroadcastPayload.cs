using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     One wire-facing top-3 slot: a killer's name/tribe/kill-total, or <see cref="Blank" /> when the
///     leaderboard has fewer than 3 tracked entries or the zone's war state is not currently active (source
///     contract: "the three top entries carry a single-space placeholder name and zero values instead of
///     real data").
/// </summary>
public readonly record struct KillFeedTopEntry(string Name, byte Tribe, int Kills)
{
    /// <summary>Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:365-370 (single-space name, zeroed tribe/kills).</summary>
    public static readonly KillFeedTopEntry Blank = new(" ", 0, 0);
}

/// <summary>
///     Everything the code-3000 kill-feed broadcast (<see cref="World.Zone.RecordEnemyKillForFeed" />) needs
///     to describe one qualifying enemy-tribe kill: killer/victim name+tribe, plus the leaderboard's top 3 at
///     the moment of this kill (already blank-padded to exactly 3 slots by the caller).
/// </summary>
/// <remarks>
///     Pure DTO -- carries no wire-layout knowledge itself. <see cref="KillFeedBroadcastEncoder" /> is the
///     only place that decides how these fields map onto the 130-byte <c>ZoneEventInfoResponse.Data</c> blob
///     (Sort=3000); see that class's own remarks for why its exact byte layout is a documented placeholder,
///     not a byte-exact-verified transcription.
/// </remarks>
public readonly record struct KillFeedBroadcastPayload(
    string KillerName,
    byte KillerTribe,
    string VictimName,
    byte VictimTribe,
    ImmutableArray<KillFeedTopEntry> TopThree)
{
    /// <summary>
    ///     Builds the payload from a killer/victim pair and the leaderboard's current top-3 snapshot,
    ///     blank-padding to exactly 3 slots regardless of how many the leaderboard actually returned (source
    ///     contract step 1: the feed's top-3 is unconditionally blanked first, then overwritten only up to
    ///     however many valid entries exist).
    /// </summary>
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
