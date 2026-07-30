using System.Diagnostics.CodeAnalysis;

namespace Fenrir.Cluster.Party;

public sealed class InMemoryPartyRosterStore : IPartyRosterStore
{
    private readonly Dictionary<string, PartyRoster> _parties = new(StringComparer.Ordinal);

    public bool TryGet(string partyName, [NotNullWhen(true)] out PartyRoster? roster)
    {
        return _parties.TryGetValue(partyName, out roster);
    }

    public void Put(PartyRoster roster)
    {
        _parties[roster.PartyName] = roster;
    }

    public void Remove(string partyName)
    {
        _parties.Remove(partyName);
    }

    public PartyRoster? FindPartyContainingMember(string memberName)
    {
        foreach (var roster in _parties.Values)
        foreach (var member in roster.Members)
            if (string.Equals(member, memberName, StringComparison.Ordinal))
                return roster;

        return null;
    }
}
