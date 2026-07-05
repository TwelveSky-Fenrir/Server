namespace Fenrir.Data.Security;

/// <summary>
///     Renders a wire adapter's raw MAC bytes (<c>LoginAdapterInfo.PhysicalAddress</c>/<c>PhysicalAddressLength</c>)
///     into the same "AA-BB-CC-..." uppercase-hex, hyphen-joined form the legacy client/server used
///     (<c>Server/ts25login/S08_MyDB.cpp:401-414</c>), so it matches whatever an operator seeded into
///     admin.MacRestrictions.MacAddress.
/// </summary>
public static class MacAddressFormatter
{
    /// <summary>Empty when <paramref name="length" /> is 0 (no adapter reported -- the integration bots send this).</summary>
    public static string Format(ReadOnlySpan<byte> address, uint length)
    {
        var count = (int)Math.Min(length, (uint)address.Length);
        if (count <= 0)
            return "";

        return string.Join('-', ToHexBytes(address[..count]));
    }

    private static IEnumerable<string> ToHexBytes(ReadOnlySpan<byte> bytes)
    {
        var result = new string[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
            result[i] = bytes[i].ToString("X2");

        return result;
    }
}
