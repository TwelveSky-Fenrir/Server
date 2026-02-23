namespace Fenrir.Network.Infrastructure;

internal static class UuidGenerator
{
    private static readonly char[] Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

    private static long _lastId = DateTime.UtcNow.Ticks;

    public static string NewGuid()
    {
        // TODO: Why not just use existing GUID generators?
        return string.Create(13, Interlocked.Increment(ref _lastId), (buffer, value) =>
        {
            for (var i = 0; i < buffer.Length; i++) buffer[buffer.Length - 1 - i] = Chars[(value >> (5 * i)) & 31];
        });
    }
}