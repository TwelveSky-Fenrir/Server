using System.Buffers.Binary;
using System.Text;

namespace Fenrir.Tools.LegacyDataImport.Legacy;

/// <summary>
///     Cursor-based reader over a tightly-packed legacy C struct record (no <c>#pragma pack</c> in effect --
///     natural alignment applies, so callers must <see cref="Skip" /> any compiler-inserted padding explicitly;
///     see Docs/legacy/ for the byte-offset maps this was built against).
/// </summary>
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

    /// <summary>
    ///     Reads a fixed-width legacy char buffer and decodes it as Latin-1, trimmed at the first NUL byte
    ///     (the legacy tool zero-pads unused tail bytes rather than filling them with spaces).
    /// </summary>
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
