using System.Collections.Immutable;
using Fenrir.Protocol.Center;

namespace Fenrir.Cluster.Party;

public readonly record struct PartyEventBroadcastPlan(
    bool EchoInbound,
    PartyRosterSnapshot? Snapshot,
    PartyBreakNotice? Break)
{
    public static readonly PartyEventBroadcastPlan None = new(false, null, null);

    public static readonly PartyEventBroadcastPlan EchoOnly = new(true, null, null);
}

public readonly record struct PartyRosterSnapshot(
    string PartyName,
    ImmutableArray<string> Members,
    PartySnapshotDisposition Disposition);

public readonly record struct PartyBreakNotice(string QueryName, string ResolvedName);
