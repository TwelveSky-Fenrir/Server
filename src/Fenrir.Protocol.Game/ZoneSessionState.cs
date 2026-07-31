namespace Fenrir.Protocol.Game;

public enum ZoneSessionState : byte
{
    Connected = 0,

    TicketConsumed = 1,

    Registering = 2,

    InWorld = 3,

    Leaving = 4
}
