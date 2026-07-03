namespace Fenrir.Contracts.Wire;

/// <summary>LoginServer-side session states (adapted from the legacy flow, §8.1/§8.5).</summary>
public enum LoginSessionState : byte
{
    Connected,
    VersionOk,
    Authenticated,

    /// <summary>
    ///     Legacy <c>mSecondLoginSort == 1</c> (P2ndPassword=1 in BuildEU33/ServerInfo.ini): credentials accepted but
    ///     the mouse-PIN gate is still closed. Only ops 12 (keepalive) and 13/14/15 (create/change/verify PIN) are
    ///     legal here — the legacy server Quit()s any 17/18/19/21/22/23/25 received before <c>IsSecondLogin()</c>
    ///     turns true (S04_MyWork02.cpp, second-login preconditions on every handler).
    /// </summary>
    PinRequired,
    CharSelect,
    HandoverIssued
}
