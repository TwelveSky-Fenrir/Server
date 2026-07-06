namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     Op18 CL_DELETE_AVATAR_SEND (delete branch)'s three read-only business-rule refusals: tribe leadership/
///     election role, guild membership, and pending proxy-shop state. Each predicate below answers exactly one
///     rule in isolation, given data its caller has already fetched; running them in the legacy-mandated order
///     (tribe, then guild, then shop) and stopping at the first failure is DeleteAvatarService's job, not this
///     gate's.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1237-1274 (the three sequential refusal checks; a fourth,
///     level-based block at lines 1249-1268 is fully block-commented dead code and deliberately not modeled
///     here) ; Server/Header/function.h:92-114 (ReturnTribeRole rank encoding: 1=master, 2=sub-master, 3=vote
///     candidate, 0=none) ; ServerDocs/12_ts25zone/04_MyWork02_PartieA.md:498-502 (corroborates rank 3 = an
///     active tribe-election candidate) ; Server/ts25login/S08_MyDB.cpp:1109-1175 (CheckProxyShop -- row-
///     absence, state/money, item-string-length, and per-slot item checks).
/// </remarks>
public static class AvatarDeletionGate
{
    /// <summary>
    ///     True if this character currently holds tribe master or sub-master rank (<paramref name="tribeRole" />
    ///     is non-zero, matching ReturnTribeRole's own 1/2 encoding directly), or is a currently-registered Force
    ///     Leader election candidate for its own tribe (present in <paramref name="ownTribeVotes" /> under its
    ///     own <paramref name="characterId" />) -- all three ranks collapse to the same refusal.
    /// </summary>
    /// <remarks>
    ///     Implementation-remap note: legacy identifies the vote-candidate rank (encoding 3) by name comparison
    ///     against a live shared-memory roster, whereas this identifies it by character id against a separately
    ///     fetched <paramref name="ownTribeVotes" /> list. Functionally equivalent as long as both ultimately
    ///     model the same candidate set for the tribe, but that equivalence has not been independently verified
    ///     against the vote-roster data model -- flagged here rather than assumed.
    /// </remarks>
    public static bool TribeRoleBlocksDeletion(byte tribeRole, int characterId,
        IReadOnlyList<TribeVoteDto> ownTribeVotes)
    {
        return tribeRole != 0 || ownTribeVotes.Any(vote => vote.CandidateCharacterId == characterId);
    }

    /// <summary>True if this character currently belongs to any guild.</summary>
    /// <remarks>
    ///     Intentional divergence from legacy, not an oversight: this reads whatever guild-membership snapshot
    ///     its caller fetched, which in Fenrir is a fresh, live lookup at delete-request time. Legacy instead
    ///     reads a per-session guild-name/guild-role snapshot populated once, at account login, and never
    ///     refreshed again during that session (Server/ts25login/S08_MyDB.cpp:563-671, within
    ///     <c>MyDB::Login</c>) -- so a character that joins a guild mid-session and is deleted later in that
    ///     same session would, under legacy, still be evaluated against its stale "no guild" snapshot and would
    ///     not be refused. Fenrir's live read is stricter than legacy in exactly that narrow scenario; this is a
    ///     deliberate, acknowledged deviation (a cross-process caching quirk, not a design worth reproducing),
    ///     not an assumption that the two are behaviorally identical.
    /// </remarks>
    public static bool GuildMembershipBlocksDeletion(CharacterGuildMembershipDto? membership)
    {
        return membership is not null;
    }

    /// <summary>
    ///     True if this character has a pending offline/proxy shop: no shop row at all is always allowed; a shop
    ///     row with a nonzero state or either money balance is always blocked, regardless of items; a shop row
    ///     with state and both balances zero is blocked only if it still has any item rows.
    /// </summary>
    /// <remarks>
    ///     The legacy check parses a fixed-length packed item-list string only when state/money are all zero, and
    ///     silently treats a wrong-length string as "nothing to check" (allowed) rather than blocking -- a
    ///     legacy-only artifact of that packed-string format. Fenrir's game.OfflineShopItems table already stores
    ///     one row per genuinely occupied slot (never zero-filled -- see that table's own doc comment), so there
    ///     is no packed string to mis-parse here: "no item rows" and "the string didn't parse" collapse into the
    ///     same allowed outcome by construction of the normalized schema, not by replicating the parse failure
    ///     itself.
    /// </remarks>
    public static bool ProxyShopBlocksDeletion(OfflineShopRowDto? shop, IReadOnlyList<OfflineShopItemRowDto> items)
    {
        if (shop is null)
            return false;

        if (shop.ShopState != 0 || shop.Money != 0 || shop.BigMoney != 0)
            return true;

        return items.Count > 0;
    }
}
