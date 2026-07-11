namespace Fenrir.Data.Security;

public static class MacAddressFormatter
{

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
