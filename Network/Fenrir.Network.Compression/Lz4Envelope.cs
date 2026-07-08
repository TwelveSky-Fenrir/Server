using System.Buffers;
using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace Fenrir.Network.Compression;

/// <summary>
///     ZC_ZPACKET_OK_SEND envelope (§3.5): tProtocol(1) + isCompress(4) + originalSize(4) + compressSize(4) +
///     payload. Falls back to uncompressed (isCompress=0, payload=originalSize bytes) if
///     <see cref="LZ4Codec.Encode(System.ReadOnlySpan{byte},System.Span{byte},LZ4Level)" /> returns &lt;= 0.
///     S→C only, never received by the server -- no symmetric decode exists here.
/// </summary>
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
            var compressSize = LZ4Codec.Encode(plainPayload, compressedSpan, LZ4Level.L00_FAST);
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
