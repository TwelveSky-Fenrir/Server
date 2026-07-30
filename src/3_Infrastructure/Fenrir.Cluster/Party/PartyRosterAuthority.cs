using System.Collections.Immutable;
using Fenrir.Protocol.Center;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Party;

public sealed class PartyRosterAuthority(IPartyRosterStore store, ILogger<PartyRosterAuthority> logger)
{
    private readonly Lock _gate = new();

    public PartyEventBroadcastPlan Apply(int sort, string partyName, string avatarName)
    {
        lock (_gate)
        {
            return sort switch
            {
                (int)PartyEventOperation.Join => ApplyJoin(partyName, avatarName),
                (int)PartyEventOperation.Leave or (int)PartyEventOperation.Kick => ApplyLeaveOrKick(sort, partyName,
                    avatarName),
                (int)PartyEventOperation.Info => ApplyInfo(partyName),
                (int)PartyEventOperation.Break => ApplyBreak(partyName),
                _ => ApplyUnknown(sort)
            };
        }
    }

    private PartyEventBroadcastPlan ApplyJoin(string partyName, string avatarName)
    {
        if (store.TryGet(partyName, out var roster))
        {
            if (roster.IsFull)
            {
                logger.LogWarning(
                    "op57 JOIN rejected: party {Party} already at cap {Cap}; member {Member} not added, no snapshot fanned out",
                    partyName, PartyEventProtocol.MaxMembers, avatarName);
                return PartyEventBroadcastPlan.None;
            }

            var joined = roster with { Members = roster.Members.Add(avatarName) };
            store.Put(joined);
            return JoinFanOut(joined, PartySnapshotDisposition.JoinedExisting);
        }

        var created = new PartyRoster(partyName, ImmutableArray.Create(partyName, avatarName));
        store.Put(created);
        return JoinFanOut(created, PartySnapshotDisposition.Created);
    }

    private PartyEventBroadcastPlan ApplyLeaveOrKick(int sort, string partyName, string avatarName)
    {
        if (store.TryGet(partyName, out var roster))
        {
            var index = roster.Members.IndexOf(avatarName);
            if (index >= 0)
            {
                var remaining = roster.Members.RemoveAt(index);
                if (remaining.Length == 0)
                {
                    store.Remove(partyName);
                    logger.LogDebug("op57 {Sort} emptied party {Party}; entry erased", sort, partyName);
                }
                else
                {
                    store.Put(roster with { Members = remaining });
                }
            }
        }

        return PartyEventBroadcastPlan.EchoOnly;
    }

    private PartyEventBroadcastPlan ApplyInfo(string partyName)
    {
        if (store.TryGet(partyName, out var roster))
            return new PartyEventBroadcastPlan(false,
                new PartyRosterSnapshot(roster.PartyName, roster.Members, PartySnapshotDisposition.Refresh), null);

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
            store.Remove(partyName);
            logger.LogDebug("op57 BREAK erased party {Party}", partyName);
        }

        return PartyEventBroadcastPlan.EchoOnly;
    }

    private PartyEventBroadcastPlan ApplyUnknown(int sort)
    {
        logger.LogDebug("op57 unknown sub-code {Sort}: raw-echoed then ignored, no roster change", sort);
        return PartyEventBroadcastPlan.EchoOnly;
    }

    private static PartyEventBroadcastPlan JoinFanOut(PartyRoster roster, PartySnapshotDisposition disposition)
    {
        return new PartyEventBroadcastPlan(true, new PartyRosterSnapshot(roster.PartyName, roster.Members, disposition),
            null);
    }
}
