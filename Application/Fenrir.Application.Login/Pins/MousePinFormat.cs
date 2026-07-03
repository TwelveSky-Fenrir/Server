namespace Fenrir.Application.Login.Pins;

/// <summary>
///     Byte-exact port of the legacy <c>CheckMousePassword</c> (function.h l.56-71): a mouse PIN is valid iff its
///     length is exactly <c>MAX_MOUSE_PASSWORD_LENGTH - 1 = 4</c> and every character is an ASCII digit
///     ('0'..'9', 0x30-0x39). Pure — shared by the three PIN handlers (ops 13/14/15), which all Quit() on an
///     invalid format in the legacy server.
/// </summary>
public static class MousePinFormat
{
    /// <summary>MAX_MOUSE_PASSWORD_LENGTH - 1 (the buffer is 5 bytes: 4 digits + NUL).</summary>
    public const int PinLength = 4;

    public static bool IsValid(string pin)
    {
        if (pin.Length != PinLength)
            return false;

        foreach (var c in pin)
            if (c is < '0' or > '9')
                return false;

        return true;
    }
}
