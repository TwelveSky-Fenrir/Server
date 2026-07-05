using System.Buffers.Binary;
using System.IO.Compression;

namespace Fenrir.Tools.LegacyDataImport.Legacy;

/// <summary>
///     Decodes the legacy <c>ts25sharemem</c> "005_0000N.IMG" container (reverse-engineered from
///     ZlibScope.h's Unpack005Copy/GetZData): 4-byte size + 4-byte compressed size + zlib stream. The
///     inflated payload's first 4 bytes are a record count XOR'd with a per-dataset key (integrity check);
///     the record array at <paramref name="recordArrayOffset" /> is never itself obfuscated.
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
