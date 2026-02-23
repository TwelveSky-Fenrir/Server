namespace Fenrir.Framework.Extensions;

public static class StreamExtensions
{
    public static int Remaining(this Stream stream)
    {
        return (int)(stream.Length - stream.Position);
    }

    public static unsafe bool TryRead<T>(this Stream stream, Func<T> func, out T value)
        where T : unmanaged
    {
        value = default;

        var size = sizeof(T);

        if (stream.Remaining() < size)
            return false;

        value = func();
        return true;
    }
}