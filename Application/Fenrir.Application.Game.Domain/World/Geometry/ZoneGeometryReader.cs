using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;

namespace Fenrir.Application.Game.Domain.World.Geometry;

/// <summary>
///     Decodes legacy <c>Z0NN.WM</c> (<c>ts25zone/S09_MyWorld.cpp</c> <c>WORLD_FOR_GXD::LoadWM</c>):
///     <c>[int32 originalSize][int32 compressedSize][zlib stream]</c>, then triangle records, then quadtree
///     node records. Node records are variable-length, so must be read sequentially, not by index.
/// </summary>
public static class ZoneGeometryReader
{
    public static ZoneGeometry Load(string wmFilePath)
    {
        using var file = File.OpenRead(wmFilePath);
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

        var position = 0;

        var triangleCount = ReadInt32(inflated, ref position);
        var triangles = new WorldTriangle[triangleCount];
        for (var i = 0; i < triangleCount; i++) triangles[i] = ReadTriangle(inflated, ref position);

        var quadtreeNodeCount = ReadInt32(inflated, ref position);
        _ = ReadInt32(inflated, ref position); // mMaxQuadtreeNodeLeafNum: allocation hint only, unused here

        var quadtree = new QuadtreeNode[quadtreeNodeCount];
        for (var i = 0; i < quadtreeNodeCount; i++) quadtree[i] = ReadQuadtreeNode(inflated, ref position);

        return new ZoneGeometry(triangles, quadtree);
    }

    private static WorldTriangle ReadTriangle(byte[] data, ref int position)
    {
        _ = ReadInt32(data, ref position); // mTextureIndex: rendering-only, discarded
        var vertex0 = ReadVertexPosition(data, ref position);
        var vertex1 = ReadVertexPosition(data, ref position);
        var vertex2 = ReadVertexPosition(data, ref position);
        var planeInfo = ReadVector4(data, ref position);
        position += 16; // mSphereInfo: rendering-only, discarded
        return new WorldTriangle(vertex0, vertex1, vertex2, planeInfo);
    }

    private static Vector3 ReadVertexPosition(byte[] data, ref int position)
    {
        var position3D = ReadVector3(data, ref position);
        position += 12 + 8 + 8; // mN (normal), mT1, mT2 (UVs): rendering-only, discarded
        return position3D;
    }

    private static QuadtreeNode ReadQuadtreeNode(byte[] data, ref int position)
    {
        var boxMin = ReadVector3(data, ref position);
        var boxMax = ReadVector3(data, ref position);
        var triangleCount = ReadInt32(data, ref position);
        var hasTriangleIndex = ReadInt32(data, ref position) != 0;

        var triangleIndex = Array.Empty<int>();
        if (hasTriangleIndex)
        {
            triangleIndex = new int[triangleCount];
            for (var i = 0; i < triangleCount; i++) triangleIndex[i] = ReadInt32(data, ref position);
        }

        var childNodeIndex = new int[4];
        for (var i = 0; i < 4; i++) childNodeIndex[i] = ReadInt32(data, ref position);

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
