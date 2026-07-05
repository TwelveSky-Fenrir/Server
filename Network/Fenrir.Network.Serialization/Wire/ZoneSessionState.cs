namespace Fenrir.Network.Serialization.Wire;

/// <summary>GameServer/Zone-side session states (adapted from the legacy flow, §8.1/§8.5).</summary>
public enum ZoneSessionState : byte
{
    Connected,
    TicketConsumed,
    Registering,
    InWorld
}
