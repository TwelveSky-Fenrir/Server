using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

// CZ_TRIBE_WORK_SEND tSort 6.
public class TribeWorkTitlePayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(8, TribeWorkTitlePayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[TribeWorkTitlePayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(TribeWorkTitlePayload.WireSize, written);

        Assert.True(TribeWorkTitlePayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[8];
        value.Write(actual);

        var expected = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(expected, value.TitleSort);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), value.TitleLv);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribeWorkTitlePayload.TryRead(new byte[7], out _));
    }

    private static TribeWorkTitlePayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new TribeWorkTitlePayload { TitleSort = v.NextInt(), TitleLv = v.NextInt() };
    }
}
