using System.Buffers;
using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace Fenrir.Network.Compression;

public static class Lz4Envelope
{
    public static byte[] Encode(byte opcode, ReadOnlySpan<byte> plainPayload)
    {
        var payloadSize = plainPayload.Length;
        var maxCompressedSize = LZ4Codec.MaximumOutputSize(payloadSize);
        var compressedRented = ArrayPool<byte>.Shared.Rent(maxCompressedSize);

        try
        {
            var compressedSpan = compressedRented.AsSpan(0, maxCompressedSize);
            var compressSize = LZ4Codec.Encode(plainPayload, compressedSpan);
            var isCompress = compressSize > 0;
            var bodySize = isCompress ? compressSize : payloadSize;

            var frame = new byte[13 + bodySize];
            frame[0] = opcode;
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(1, 4), isCompress ? 1 : 0);
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(5, 4), payloadSize);
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(9, 4), isCompress ? compressSize : 0);
            (isCompress ? compressedSpan[..compressSize] : plainPayload).CopyTo(frame.AsSpan(13));

            return frame;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressedRented);
        }
    }
}
