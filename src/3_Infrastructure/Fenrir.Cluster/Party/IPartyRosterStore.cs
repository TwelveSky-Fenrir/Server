using System.Diagnostics.CodeAnalysis;

namespace Fenrir.Cluster.Party;

/// <summary>
///     Center-process-local, in-memory registry of live party rosters keyed by party name. Volatile by design
///     — no database read or write anywhere in the op57 handler, so a Center restart loses every party
///     (a legacy design limitation; persisting parties would be a Fenrir product decision, not legacy parity).
///     <para>
///         Intentionally NOT independently thread-safe: its sole consumer, <see cref="PartyRosterAuthority" />,
///         serialises every read-modify-write under its own lock so the store can stay a plain map. Do not add a
///         second consumer without moving the synchronisation here.
///     </para>
/// </summary>
public interface IPartyRosterStore
{
    /// <summary>Looks up a party by its key name.</summary>
    bool TryGet(string partyName, [NotNullWhen(true)] out PartyRoster? roster);

    /// <summary>Inserts or replaces the roster keyed by <see cref="PartyRoster.PartyName" />.</summary>
    void Put(PartyRoster roster);

    /// <summary>Removes the party under the given key, if present.</summary>
    void Remove(string partyName);

    /// <summary>
    ///     Scans every live party for one containing a member with the given name (case-sensitive, ordinal),
    ///     for the 108-INFO not-found reconciliation path. Returns the first match or null.
    /// </summary>
    PartyRoster? FindPartyContainingMember(string memberName);
}
