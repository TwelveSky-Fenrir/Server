using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;

namespace Fenrir.Application.Game.Domain.World.Geometry;

public static class ZoneGeometryReader
{
    private const int HeaderSize = 8;
    private const int TriangleSize = 156;
    private const int QuadtreeNodeMinimumSize = 48;

    public static ZoneGeometry Load(string wmFilePath)
    {
        using var file = File.OpenRead(wmFilePath);
        Span<byte> header = stackalloc byte[HeaderSize];
        file.ReadExactly(header);

        var originalSize = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
        var compressedSize = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        if (originalSize < 12 || compressedSize <= 0 || file.Length != HeaderSize + compressedSize)
            throw new InvalidDataException("The WM header does not match the file length.");

        var compressed = new byte[compressedSize];
        file.ReadExactly(compressed);

        var inflated = new byte[originalSize];
        using (var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress))
        {
            zlib.ReadExactly(inflated);
        }

        var position = 0;

        var triangleCount = ReadInt32(inflated, ref position);
        if (triangleCount < 0 || triangleCount > (inflated.Length - position - 8) / TriangleSize)
            throw new InvalidDataException("The WM triangle count exceeds the inflated payload.");

        var triangles = new WorldTriangle[triangleCount];
        for (var i = 0; i < triangleCount; i++) triangles[i] = ReadTriangle(inflated, ref position);

        var quadtreeNodeCount = ReadInt32(inflated, ref position);
        var maxQuadtreeLeafCount = ReadInt32(inflated, ref position);
        if (quadtreeNodeCount <= 0 || maxQuadtreeLeafCount < 0 ||
            quadtreeNodeCount > (inflated.Length - position) / QuadtreeNodeMinimumSize)
            throw new InvalidDataException("The WM quadtree header is invalid.");

        var quadtree = new QuadtreeNode[quadtreeNodeCount];
        for (var i = 0; i < quadtreeNodeCount; i++)
            quadtree[i] = ReadQuadtreeNode(inflated, ref position, triangleCount, quadtreeNodeCount);

        if (position != inflated.Length)
            throw new InvalidDataException("The WM payload contains trailing bytes.");

        return new ZoneGeometry(triangles, quadtree);
    }

    private static WorldTriangle ReadTriangle(byte[] data, ref int position)
    {
        _ = ReadInt32(data, ref position);
        var vertex0 = ReadVertexPosition(data, ref position);
        var vertex1 = ReadVertexPosition(data, ref position);
        var vertex2 = ReadVertexPosition(data, ref position);
        var planeInfo = ReadVector4(data, ref position);
        position += 16;
        return new WorldTriangle(vertex0, vertex1, vertex2, planeInfo);
    }

    private static Vector3 ReadVertexPosition(byte[] data, ref int position)
    {
        var position3D = ReadVector3(data, ref position);
        position += 12 + 8 + 8;
        return position3D;
    }

    private static QuadtreeNode ReadQuadtreeNode(byte[] data, ref int position, int totalTriangleCount,
        int quadtreeNodeCount)
    {
        var boxMin = ReadVector3(data, ref position);
        var boxMax = ReadVector3(data, ref position);
        var nodeTriangleCount = ReadInt32(data, ref position);
        var hasTriangleIndex = ReadInt32(data, ref position) != 0;

        var triangleIndex = Array.Empty<int>();
        if (hasTriangleIndex)
        {
            if (nodeTriangleCount < 0 || nodeTriangleCount > (data.Length - position - 16) / sizeof(int))
                throw new InvalidDataException("The WM quadtree triangle list exceeds the payload.");

            triangleIndex = new int[nodeTriangleCount];
            for (var i = 0; i < nodeTriangleCount; i++)
            {
                var value = ReadInt32(data, ref position);
                if (value < 0 || value >= totalTriangleCount)
                    throw new InvalidDataException("The WM quadtree references an invalid triangle.");

                triangleIndex[i] = value;
            }
        }
        var childNodeIndex = new int[4];
        for (var i = 0; i < 4; i++) childNodeIndex[i] = ReadInt32(data, ref position);

        if (childNodeIndex[0] == -1)
        {
            if (childNodeIndex.Any(static child => child != -1))
                throw new InvalidDataException("The WM quadtree leaf has child references.");
        }
        else if (childNodeIndex.Any(child => child < 0 || child >= quadtreeNodeCount))
            throw new InvalidDataException("The WM quadtree references an invalid child node.");

        return new QuadtreeNode(boxMin, boxMax, triangleIndex, childNodeIndex);
    }

    private static int ReadInt32(byte[] data, ref int position)
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position, 4));
        position += 4;
        return value;
    }

    private static float ReadSingle(byte[] data, ref int position)
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(position, 4));
        position += 4;
        return value;
    }

    private static Vector3 ReadVector3(byte[] data, ref int position)
    {
        var x = ReadSingle(data, ref position);
        var y = ReadSingle(data, ref position);
        var z = ReadSingle(data, ref position);
        return new Vector3(x, y, z);
    }

    private static Vector4 ReadVector4(byte[] data, ref int position)
    {
        var x = ReadSingle(data, ref position);
        var y = ReadSingle(data, ref position);
        var z = ReadSingle(data, ref position);
        var w = ReadSingle(data, ref position);
        return new Vector4(x, y, z, w);
    }
}
