using System.Buffers.Binary;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class TribeWorkSkillPayloadTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(4, TribeWorkSkillPayload.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var value = CreatePopulated();

        var buffer = new byte[TribeWorkSkillPayload.WireSize];
        var written = value.Write(buffer);
        Assert.Equal(TribeWorkSkillPayload.WireSize, written);

        Assert.True(TribeWorkSkillPayload.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(value, roundTripped);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, value.TribeSkillSort);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribeWorkSkillPayload.TryRead(new byte[3], out _));
    }

    private static TribeWorkSkillPayload CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new TribeWorkSkillPayload { TribeSkillSort = v.NextInt() };
    }
}
