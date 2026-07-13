using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Party;

/// <summary>
///     The authoritative op57 party (group) state machine, Center-side. Applies exactly one of the five party
///     sub-codes (JOIN / LEAVE / KICK / INFO / BREAK) against the volatile <see cref="IPartyRosterStore" /> and
///     returns a <see cref="PartyEventBroadcastPlan" /> describing the resulting all-zones fan-out. Pure domain:
///     it touches no wire bytes and performs no I/O — the handler owns decoding, encoding, and the actual
///     broadcast.
///     <para>
///         DORMANT this lot: the Center maintains this authoritative roster, but shards still author their own
///         party state; the shard→Center cut-over is a later, atomically-gated lot. Nothing shard-side is removed.
///     </para>
///     <para>
///         Every mutation is serialised under <see cref="_gate" /> so a read-modify-write (find slot, compact,
///         erase-if-empty) is atomic even when two zone links deliver op57 concurrently. The store therefore
///         needs no lock of its own.
///     </para>
///     <para>
///         Corrected legacy behaviours applied (the shipped C++ was bugged here — see the contract's Edge cases):
///         <list type="number">
///             <item><b>Over-cap JOIN reject.</b> A JOIN to a party already at the cap of
///             <see cref="PartyEventProtocol.MaxMembers" /> adds nothing and fans nothing out (no echo, no
///             disposition=2 snapshot), rather than broadcasting a snapshot that misrepresents the member as
///             present. The requester infers failure from the absent snapshot — there is no per-sender ack
///             opcode in the legacy op57 protocol to signal it more directly (flagged for a wire/product decision).</item>
///             <item><b>Erase-on-empty.</b> When LEAVE/KICK removes the last member, the entry is erased
///             immediately, instead of lingering as an all-empty party until Center restart.</item>
///         </list>
///         Two further contract corrections are deliberately NOT applied here, with reasons:
///         <list type="bullet">
///             <item><b>No re-key on leader departure.</b> The party name is treated as the party's stable
///             lifetime identity and is never re-keyed, because the contract's own "Cross-zone survival"
///             behaviour (to be preserved) resolves a re-entering member's party via a 108 INFO query keyed by
///             that stable name; re-keying to the new slot-0 name would strand every member still caching the
///             original. A genuinely member-name-independent id cannot be reconstructed from the name-only wire.
///             Erase-on-empty already removes the concrete harm the contract flags (a stale empty key lingering
///             and colliding with a later same-named party).</item>
///             <item><b>No BREAK leadership succession.</b> The legacy succession path is dead code
///             (<c>USE_PART_V3</c>, never defined); BREAK destroys the party outright with no successor.</item>
///         </list>
///         The invitee level-gap (≤9) rule is intentionally absent: the Center holds names only, no level data,
///         so it is not — and per the contract cannot be — a Center-side rule.
///     </para>
/// </summary>
public sealed class PartyRosterAuthority(IPartyRosterStore store, ILogger<PartyRosterAuthority> logger)
{
    private readonly Lock _gate = new();

    /// <summary>
    ///     Applies one op57 packet and returns its fan-out plan. <paramref name="partyName" /> and
    ///     <paramref name="avatarName" /> are the two fixed-13-byte names already decoded from the inbound tData
    ///     region by the handler; <paramref name="sort" /> is the raw envelope sub-code.
    /// </summary>
    public PartyEventBroadcastPlan Apply(int sort, string partyName, string avatarName)
    {
        lock (_gate)
        {
            return sort switch
            {
                (int)PartyEventOperation.Join => ApplyJoin(partyName, avatarName),
                (int)PartyEventOperation.Leave or (int)PartyEventOperation.Kick => ApplyLeaveOrKick(sort, partyName, avatarName),
                (int)PartyEventOperation.Info => ApplyInfo(partyName),
                (int)PartyEventOperation.Break => ApplyBreak(partyName),
                _ => ApplyUnknown(sort),
            };
        }
    }

    private PartyEventBroadcastPlan ApplyJoin(string partyName, string avatarName)
    {
        if (store.TryGet(partyName, out var roster))
        {
            if (roster.IsFull)
            {
                // Corrected behaviour #1: reject an over-cap JOIN; do not misrepresent the member as present.
                logger.LogWarning(
                    "op57 JOIN rejected: party {Party} already at cap {Cap}; member {Member} not added, no snapshot fanned out",
                    partyName, PartyEventProtocol.MaxMembers, avatarName);
                return PartyEventBroadcastPlan.None;
            }

            // Existing party: place the joiner in the first empty slot (= append, since slots stay contiguous).
            var joined = roster with { Members = roster.Members.Add(avatarName) };
            store.Put(joined);
            return JoinFanOut(joined, PartySnapshotDisposition.JoinedExisting);
        }

        // New party: slot 0 = partyName (leader/key), slot 1 = the joiner.
        var created = new PartyRoster(partyName, ImmutableArray.Create(partyName, avatarName));
        store.Put(created);
        return JoinFanOut(created, PartySnapshotDisposition.Created);
    }

    private PartyEventBroadcastPlan ApplyLeaveOrKick(int sort, string partyName, string avatarName)
    {
        // LEAVE (106) and KICK (107) are indistinguishable at the Center: identical remove-and-compact. Only the
        // raw echo carries the distinct sub-code through to zones for client display.
        if (store.TryGet(partyName, out var roster))
        {
            var index = roster.Members.IndexOf(avatarName);
            if (index >= 0)
            {
                var remaining = roster.Members.RemoveAt(index); // shift-down compaction is implicit in a contiguous list
                if (remaining.Length == 0)
                {
                    // Corrected behaviour #2: erase as soon as the roster empties.
                    store.Remove(partyName);
                    logger.LogDebug("op57 {Sort} emptied party {Party}; entry erased", sort, partyName);
                }
                else
                {
                    store.Put(roster with { Members = remaining });
                }
            }
        }

        // No snapshot on LEAVE/KICK — zones reconcile from the raw echo alone.
        return PartyEventBroadcastPlan.EchoOnly;
    }

    private PartyEventBroadcastPlan ApplyInfo(string partyName)
    {
        // 108 INFO is never raw-echoed.
        if (store.TryGet(partyName, out var roster))
            return new PartyEventBroadcastPlan(false,
                new PartyRosterSnapshot(roster.PartyName, roster.Members, PartySnapshotDisposition.Refresh), null);

        // Key gone: tell the requester the party is gone via a 109 break. Carry the containing party's leader name
        // if the queried name is still a member somewhere, else an empty avatar name.
        var found = store.FindPartyContainingMember(partyName);
        var resolved = found?.Leader ?? string.Empty;
        logger.LogDebug("op57 INFO for absent party {Party}: emitting break (resolved leader '{Resolved}')",
            partyName, resolved);
        return new PartyEventBroadcastPlan(false, null, new PartyBreakNotice(partyName, resolved));
    }

    private PartyEventBroadcastPlan ApplyBreak(string partyName)
    {
        if (store.TryGet(partyName, out _))
        {
            // No USE_PART_V3 succession in the shipped build: BREAK erases the whole entry outright.
            store.Remove(partyName);
            logger.LogDebug("op57 BREAK erased party {Party}", partyName);
        }

        return PartyEventBroadcastPlan.EchoOnly;
    }

    private PartyEventBroadcastPlan ApplyUnknown(int sort)
    {
        // Unknown sub-code (never 108, which is a known case): legacy still raw-echoes it, then no case matches.
        logger.LogDebug("op57 unknown sub-code {Sort}: raw-echoed then ignored, no roster change", sort);
        return PartyEventBroadcastPlan.EchoOnly;
    }

    private static PartyEventBroadcastPlan JoinFanOut(PartyRoster roster, PartySnapshotDisposition disposition)
        // JOIN fans out twice: the raw echo first, then the authoritative snapshot.
        => new(true, new PartyRosterSnapshot(roster.PartyName, roster.Members, disposition), null);
}
