using System.Collections.Immutable;
using Fenrir.Protocol.Center;

namespace Fenrir.Cluster.Party;

public sealed record PartyRoster(string PartyName, ImmutableArray<string> Members)
{
    public bool IsFull => Members.Length >= PartyEventProtocol.MaxMembers;

    public string Leader => Members.Length > 0 ? Members[0] : string.Empty;
}
