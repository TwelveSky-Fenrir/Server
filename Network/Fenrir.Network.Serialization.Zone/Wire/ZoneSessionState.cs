namespace Fenrir.Network.Serialization.Zone.Wire;

public enum ZoneSessionState : byte
{
    Connected,
    TicketConsumed,
    Registering,
    InWorld
}
