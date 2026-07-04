using System.Buffers.Binary;
using System.Text;

namespace Fenrir.Tools.LegacyDataImport.Legacy;

/// <summary>Cursor over a tightly-packed legacy C struct (natural alignment, no #pragma pack) -- callers must <see cref="Skip" /> compiler padding explicitly.</summary>
internal ref struct LegacySpanReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    public int Position { get; private set; }

    public int ReadInt32()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(Position, 4));
        Position += 4;
        return value;
    }

    public float ReadSingle()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(_data.Slice(Position, 4));
        Position += 4;
        return value;
    }

    public int[] ReadInt32Array(int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++) values[i] = ReadInt32();
        return values;
    }

    public float[] ReadSingleArray(int count)
    {
        var values = new float[count];
        for (var i = 0; i < count; i++) values[i] = ReadSingle();
        return values;
    }

    /// <summary>Reads a fixed-width buffer as Latin-1, trimmed at the first NUL (legacy zero-pads, not space-pads).</summary>
    public string ReadFixedString(int length)
    {
        var slice = _data.Slice(Position, length);
        Position += length;

        var nulIndex = slice.IndexOf((byte)0);
        var textSlice = nulIndex >= 0 ? slice[..nulIndex] : slice;
        return Encoding.Latin1.GetString(textSlice);
    }

    public void Skip(int byteCount)
    {
        Position += byteCount;
    }
}
