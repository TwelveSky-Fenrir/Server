using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class GmMoveCoordinatePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(12, GmMoveCoordinatePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[GmMoveCoordinatePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(GmMoveCoordinatePayload.WireSize, written);

        Assert.True(GmMoveCoordinatePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[GmMoveCoordinatePayload.WireSize];
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(0, 4), 100.5f);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(4, 4), 0f);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(8, 4), -50.25f);

        Assert.True(GmMoveCoordinatePayload.TryRead(buffer, out var payload));

        Assert.Equal(100.5f, payload.Location[0]);
        Assert.Equal(0f, payload.Location[1]);
        Assert.Equal(-50.25f, payload.Location[2]);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmMoveCoordinatePayload.TryRead(new byte[11], out _));
    }

    [Fact]
    public void TryRead_DecodesFromFirst12BytesOfLargerBuffer()
    {
        var data = new byte[130];
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0, 4), 1f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(4, 4), 2f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(8, 4), 3f);

        Assert.True(GmMoveCoordinatePayload.TryRead(data, out var payload));
        Assert.Equal([1f, 2f, 3f], payload.Location);
    }

    private static GmMoveCoordinatePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new GmMoveCoordinatePayload { Location = v.NextFloatArray(3) };
    }
}
