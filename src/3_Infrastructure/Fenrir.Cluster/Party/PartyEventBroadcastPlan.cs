using System.Collections.Immutable;

namespace Fenrir.Cluster.Party;

/// <summary>
///     The fan-out plan <see cref="PartyRosterAuthority" /> returns for one op57 packet. Every op57 output is a
///     broadcast to all zones (never a per-sender reply). The handler emits, in this order: the raw echo (if
///     <see cref="EchoInbound" />), then the snapshot (if <see cref="Snapshot" /> is set), then the break (if
///     <see cref="Break" /> is set). At most two ever fire together — JOIN produces echo + snapshot; every other
///     case produces exactly one broadcast or (an over-cap JOIN reject) none.
/// </summary>
public readonly record struct PartyEventBroadcastPlan(
    bool EchoInbound,
    PartyRosterSnapshot? Snapshot,
    PartyBreakNotice? Break)
{
    /// <summary>Nothing to fan out (an over-cap JOIN reject — the requester infers failure from the absent snapshot).</summary>
    public static readonly PartyEventBroadcastPlan None = new(false, null, null);

    /// <summary>Raw echo only (LEAVE / KICK / BREAK / unknown non-108 sub-code).</summary>
    public static readonly PartyEventBroadcastPlan EchoOnly = new(true, null, null);
}

/// <summary>
///     A 108 snapshot to fan out: the full authoritative roster plus a disposition (<c>pSort</c>). <see cref="Members" />
///     is slot-ordered and 1..5 long; the handler pads the remaining wire slots with empty names.
/// </summary>
public readonly record struct PartyRosterSnapshot(
    string PartyName,
    ImmutableArray<string> Members,
    PartySnapshotDisposition Disposition);

/// <summary>
///     A 109 break to fan out on the INFO not-found path. <see cref="QueryName" /> is the looked-up name
///     (goes into the partyName field); <see cref="ResolvedName" /> is the found party's leader name, or the
///     empty string when the member was found nowhere (goes into the avatarName01 field).
/// </summary>
public readonly record struct PartyBreakNotice(string QueryName, string ResolvedName);
