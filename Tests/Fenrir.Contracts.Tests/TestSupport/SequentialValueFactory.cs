namespace Fenrir.Contracts.Tests.TestSupport;

// Deterministic increasing values (never repeating) so an offset bug reading/writing the wrong field is detectable.
internal sealed class SequentialValueFactory
{
    private int _counter;

    public int NextInt()
    {
        return ++_counter;
    }

    public uint NextUInt()
    {
        return (uint)NextInt();
    }

    public long NextLong()
    {
        return NextInt() * 1_000_000_007L;
    }

    public float NextFloat()
    {
        return NextInt() + 0.25f;
    }

    public byte NextByte()
    {
        return (byte)(NextInt() & 0xFF);
    }

    // Truncated to fixedLength-1 so there's always room for a trailing zero byte, even as the counter grows.
    public string NextString(int fixedLength)
    {
        var value = "S" + NextInt();
        var maxLength = fixedLength - 1;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    public int[] NextIntArray(int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
            values[i] = NextInt();
        return values;
    }

    public float[] NextFloatArray(int count)
    {
        var values = new float[count];
        for (var i = 0; i < count; i++)
            values[i] = NextFloat();
        return values;
    }

    public byte[] NextByteArray(int count)
    {
        var values = new byte[count];
        for (var i = 0; i < count; i++)
            values[i] = NextByte();
        return values;
    }

    public string[] NextStringArray(int count, int rowLength)
    {
        var values = new string[count];
        for (var i = 0; i < count; i++)
            values[i] = NextString(rowLength);
        return values;
    }
}
