using System.Collections.Immutable;

namespace Fenrir.Cluster.Party;

/// <summary>
///     An authoritative party roster: names-only, ordered, slot 0 = the party's leader/key name. The legacy
///     <c>PARTY_LIST</c> (STRUCT.h:1040-1043) carries no levels, classes, ids, or zone numbers — only names —
///     so neither does this. Immutable: <see cref="PartyRosterAuthority" /> rebuilds and re-stores it on every
///     mutation rather than editing in place, which keeps a fanned-out snapshot a consistent point-in-time copy.
/// </summary>
/// <param name="PartyName">
///     The party key, seeded from the creating leader's avatar name at JOIN-create and stable for the party's
///     whole lifetime (see <see cref="PartyRosterAuthority" /> for why it is not re-keyed on leader departure).
/// </param>
/// <param name="Members">Occupied member names in slot order, slot 0 first; length 1..<see cref="PartyEventProtocol.MaxMembers" />.</param>
public sealed record PartyRoster(string PartyName, ImmutableArray<string> Members)
{
    /// <summary>True when the roster is at the legacy cap of <see cref="PartyEventProtocol.MaxMembers" /> and cannot take another member.</summary>
    public bool IsFull => Members.Length >= PartyEventProtocol.MaxMembers;

    /// <summary>The leader / party-name holder (slot 0), or the empty string for a degenerate empty roster.</summary>
    public string Leader => Members.Length > 0 ? Members[0] : string.Empty;
}
