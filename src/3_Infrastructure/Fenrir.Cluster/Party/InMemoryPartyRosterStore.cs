using System.Diagnostics.CodeAnalysis;

namespace Fenrir.Cluster.Party;

/// <summary>
///     Volatile in-memory <see cref="IPartyRosterStore" />: a plain party-name-keyed map. Not internally
///     locked — all access is serialised by <see cref="PartyRosterAuthority" /> (see the interface remarks).
///     Keys are compared with ordinal (case-sensitive) equality, matching the legacy fixed-byte name compare.
/// </summary>
public sealed class InMemoryPartyRosterStore : IPartyRosterStore
{
    private readonly Dictionary<string, PartyRoster> _parties = new(StringComparer.Ordinal);

    public bool TryGet(string partyName, [NotNullWhen(true)] out PartyRoster? roster) =>
        _parties.TryGetValue(partyName, out roster);

    public void Put(PartyRoster roster) => _parties[roster.PartyName] = roster;

    public void Remove(string partyName) => _parties.Remove(partyName);

    public PartyRoster? FindPartyContainingMember(string memberName)
    {
        foreach (var roster in _parties.Values)
            foreach (var member in roster.Members)
                if (string.Equals(member, memberName, StringComparison.Ordinal))
                    return roster;

        return null;
    }
}
