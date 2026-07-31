namespace Fenrir.Protocol.Login;

public enum LoginSessionState : byte
{
    Connected = 0,

    VersionOk = 1,

    Authenticated = 2,

    PinRequired = 3,

    CharSelect = 4,

    HandoverIssued = 5
}
