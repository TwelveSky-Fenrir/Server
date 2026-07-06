namespace Fenrir.Application.Game.Domain.Social.Party;

/// <summary>
///     Resolves "this character's current party identity" -- the single character name that legacy's
///     <c>aPartyName</c>/<c>iPartyName</c> caches on every member's own record, always the party's original
///     creator/leader's own name, never a player-chosen string, and never reassigned to a different member
///     mid-life short of a full disband. <see cref="PartyRegistry.GetMembers" /> already returns exactly the
///     live, leader-first membership Fenrir needs to answer this; this resolver only adds the "turn the
///     leader's characterId into their current name" step shared by every consumer that needs the identity
///     itself rather than just the member list.
/// </summary>
/// <remarks>
///     Two independent consumers need this exact value, resolved the same way from the same authoritative
///     <see cref="PartyRegistry" /> state, not a snapshot taken earlier or a hardcoded absent value:
///     <see cref="Fenrir.Application.Game.Domain.World.Monsters.MonsterSpawnScheduler" />'s
///     <c>ResolvePartyDrop</c> (killer's identity at loot-drop-creation time, already wired) and ground-item
///     pickup claim eligibility (<see cref="Fenrir.Application.Game.Domain.World.Loot.GroundItemEntity.IsClaimableBy" />,
///     the claimant's identity at claim time -- today hardcoded to absent, the bug this resolver exists to fix).
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:9719-9726 (identity assigned once, to both members
///     simultaneously, as the inviter's own name, at invite-acceptance time) ; Server/ts25zone/S04_MyWork04.cpp:81-220
///     (cases 106/107 PARTY_LEAVE/KICK clear only the departing member's own copy; case 109 PARTY_BREAK clears
///     every remaining member's copy at once; no path reassigns an existing party's identity to a different
///     member) ; Server/Header/Protocol/DEFINE.h:101 (<c>USE_PARTY_V3</c> is commented out, confirming the
///     non-V3 branch cited above is the one that actually runs) ; ServerDocs/12_ts25zone/04_MyWork02_PartieA.md:382-390
///     ("the group's name is the leader's name").
/// </remarks>
public static class PartyIdentityResolver
{
    /// <summary>
    ///     Pure form: given an already-fetched member list (leader first, or empty if unpartied), the
    ///     requesting character's own id/name, and a way to look up another member's current name, returns
    ///     that character's current party identity, or <see cref="string.Empty" /> if they are not partied.
    /// </summary>
    /// <param name="partyMembers">
    ///     The party's live member list, leader first (<see cref="PartyRegistry.GetMembers" />'s own return
    ///     shape), or empty if the character is not currently partied.
    /// </param>
    /// <param name="characterId">The requesting character's own id.</param>
    /// <param name="ownName">The requesting character's own current name.</param>
    /// <param name="tryResolveMemberName">
    ///     Resolves another character's current name if locally known (e.g. same-zone player lookup), or
    ///     <see langword="null" />/empty if it cannot be resolved from here (e.g. the leader is on a different
    ///     zone shard at this exact instant). Never called when the requester is themselves the leader.
    /// </param>
    /// <returns>
    ///     The leader's current name if resolvable; otherwise <paramref name="ownName" /> as a non-empty
    ///     fallback (same posture as <c>MonsterSpawnScheduler.ResolvePartyDrop</c> -- still marks the
    ///     requester as partied for comparison purposes, just not byte-identical to the true leader name in
    ///     this rare cross-shard edge case); <see cref="string.Empty" /> if <paramref name="partyMembers" /> is
    ///     empty (not partied), which is also the correct fallback for any upstream resolution failure that
    ///     could not even determine membership.
    /// </returns>
    public static string ResolveCurrentPartyName(IReadOnlyList<int> partyMembers, int characterId, string ownName,
        Func<int, string?> tryResolveMemberName)
    {
        if (partyMembers is not { Count: > 0 })
            return string.Empty;

        var leaderId = partyMembers[0];
        if (leaderId == characterId)
            return ownName;

        return tryResolveMemberName(leaderId) is { Length: > 0 } leaderName ? leaderName : ownName;
    }

    /// <summary>Convenience overload that fetches the member list from <paramref name="partyRegistry" /> itself.</summary>
    public static string ResolveCurrentPartyName(PartyRegistry partyRegistry, int characterId, string ownName,
        Func<int, string?> tryResolveMemberName)
    {
        return ResolveCurrentPartyName(partyRegistry.GetMembers(characterId), characterId, ownName,
            tryResolveMemberName);
    }
}
