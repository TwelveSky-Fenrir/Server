namespace Fenrir.Domain.Login.Pins;

public static class MousePinFormat
{
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
