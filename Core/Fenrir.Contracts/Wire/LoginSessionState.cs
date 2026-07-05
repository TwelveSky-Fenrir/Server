namespace Fenrir.Contracts.Wire;

/// <summary>LoginServer-side session states (adapted from the legacy flow, §8.1/§8.5).</summary>
public enum LoginSessionState : byte
{
    Connected,
    VersionOk,
    Authenticated,

    /// <summary>
    ///     Legacy <c>mSecondLoginSort == 1</c>: mouse-PIN gate still closed; only ops 12/13/14/15 legal, else the legacy
    ///     server Quit()s (S04_MyWork02.cpp).
    /// </summary>
    PinRequired,
    CharSelect,
    HandoverIssued
}
