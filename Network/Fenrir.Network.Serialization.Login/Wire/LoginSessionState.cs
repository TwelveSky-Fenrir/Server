namespace Fenrir.Network.Serialization.Login.Wire;

public enum LoginSessionState : byte
{
    Connected,
    VersionOk,
    Authenticated,
    PinRequired,
    CharSelect,
    HandoverIssued
}
