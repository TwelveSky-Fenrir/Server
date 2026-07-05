namespace Fenrir.Network.Serialization.Wire;

public enum LoginSessionState : byte
{
    Connected,
    VersionOk,
    Authenticated,
    PinRequired,
    CharSelect,
    HandoverIssued
}
