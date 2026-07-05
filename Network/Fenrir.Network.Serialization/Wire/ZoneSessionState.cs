namespace Fenrir.Network.Serialization.Wire;

public enum ZoneSessionState : byte
{
    Connected,
    TicketConsumed,
    Registering,
    InWorld
}
