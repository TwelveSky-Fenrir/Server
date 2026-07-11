namespace Fenrir.Data.Abstractions.Runtime;

public interface IPartyResyncRelayQueue
{
    public bool Enqueue(PartyResyncRelayEntry entry);
}
