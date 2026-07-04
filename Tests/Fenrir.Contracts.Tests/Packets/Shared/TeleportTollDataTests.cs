using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class TeleportTollDataTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(4, TeleportTollData.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[TeleportTollData.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(TeleportTollData.WireSize, written);

        Assert.True(TeleportTollData.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[4];
        EncodeGolden(golden, value);

        Assert.True(TeleportTollData.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TeleportTollData.TryRead(new byte[3], out _));
    }

    private static TeleportTollData CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new TeleportTollData { Money = v.NextInt() };
    }

    private static void EncodeGolden(Span<byte> destination, TeleportTollData value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Money);
    }
}
