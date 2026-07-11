using System.Buffers.Binary;
using System.IO.Compression;

namespace Fenrir.Tools.LegacyDataImport.Legacy;

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
