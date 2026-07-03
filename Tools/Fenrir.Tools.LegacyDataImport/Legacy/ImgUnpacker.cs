using System.Buffers.Binary;
using System.IO.Compression;

namespace Fenrir.Tools.LegacyDataImport.Legacy;

/// <summary>
///     Decodes the legacy <c>ts25sharemem</c> "005_0000N.IMG" container format (reverse-engineered from
///     <c>Header/Scope/ZlibScope.h</c>'s <c>Unpack005Copy</c>/<c>GetZData</c>): a 4-byte decompressed size,
///     a 4-byte compressed size, then a zlib (RFC 1950) stream. The first 4 bytes of the inflated payload are
///     a record count XOR'd with a per-dataset key (used here as an integrity check, matching the legacy
///     loader's own validation); the actual record array starts at <paramref name="recordArrayOffset" /> and
///     is never itself obfuscated.
/// </summary>
internal static class ImgUnpacker
{
    public static byte[] UnpackRecordArray(string imgFilePath, int xorKey, int recordArrayOffset,
        int expectedRecordCount, int recordSize)
    {
        using var file = File.OpenRead(imgFilePath);
        Span<byte> header = stackalloc byte[8];
        file.ReadExactly(header);

        var originalSize = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
        var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);

        var compressed = new byte[compressedSize];
        file.ReadExactly(compressed);

        var inflated = new byte[originalSize];
        using (var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress))
        {
            zlib.ReadExactly(inflated);
        }

        var storedCount = BinaryPrimitives.ReadInt32LittleEndian(inflated.AsSpan(0, 4)) ^ xorKey;
        if (storedCount != expectedRecordCount)
            throw new InvalidDataException(
                $"'{Path.GetFileName(imgFilePath)}': decoded record count {storedCount} does not match the " +
                $"expected {expectedRecordCount} -- XOR key or offset is wrong for this dataset.");

        var recordBytesLength = expectedRecordCount * recordSize;
        var recordBytes = new byte[recordBytesLength];
        Array.Copy(inflated, recordArrayOffset, recordBytes, 0, recordBytesLength);
        return recordBytes;
    }
}
