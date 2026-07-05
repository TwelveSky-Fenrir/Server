using System.Buffers.Binary;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class SkillUpgradeDataTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(4, SkillUpgradeData.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[SkillUpgradeData.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(SkillUpgradeData.WireSize, written);

        Assert.True(SkillUpgradeData.TryRead(buffer, out var roundTripped));
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

        Assert.True(SkillUpgradeData.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(SkillUpgradeData.TryRead(new byte[3], out _));
    }

    private static SkillUpgradeData CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new SkillUpgradeData { SkillIndex = v.NextInt() };
    }

    private static void EncodeGolden(Span<byte> destination, SkillUpgradeData value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.SkillIndex);
    }
}
