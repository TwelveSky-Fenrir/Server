using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

public class NpcSkillLearnDataTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(8, NpcSkillLearnData.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[NpcSkillLearnData.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(NpcSkillLearnData.WireSize, written);

        Assert.True(NpcSkillLearnData.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[8];
        value.Write(actual);

        var expected = new byte[8];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var value = CreatePopulated();
        var golden = new byte[8];
        EncodeGolden(golden, value);

        Assert.True(NpcSkillLearnData.TryRead(golden, out var decoded));
        StructuralAssert.DeepEqual(value, decoded);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(NpcSkillLearnData.TryRead(new byte[7], out _));
    }

    private static NpcSkillLearnData CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new NpcSkillLearnData { NpcId = v.NextInt(), SkillId = v.NextInt() };
    }

    private static void EncodeGolden(Span<byte> destination, NpcSkillLearnData value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.NpcId);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.SkillId);
    }
}
