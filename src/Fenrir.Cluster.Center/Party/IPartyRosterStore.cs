using System.Diagnostics.CodeAnalysis;

namespace Fenrir.Cluster.Center.Party;

public interface IPartyRosterStore
{
    public bool TryGet(string partyName, [NotNullWhen(true)] out PartyRoster? roster);

    public void Put(PartyRoster roster);

    public void Remove(string partyName);

    public PartyRoster? FindPartyContainingMember(string memberName);
}
