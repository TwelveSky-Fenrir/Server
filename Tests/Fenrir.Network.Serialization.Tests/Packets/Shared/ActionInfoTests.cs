using System.Buffers.Binary;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Shared;

public class ActionInfoTests
{
    [Fact]
    public void WireSize_MatchesContract()
    {
        Assert.Equal(104, ActionInfo.WireSize);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var v = new SequentialValueFactory();
        var action = new ActionInfo
        {
            Type = v.NextInt(),
            Sort = v.NextInt(),
            Frame = v.NextFloat(),
            Location = v.NextFloatArray(3),
            TargetLocation = v.NextFloatArray(3),
            Front = v.NextFloat(),
            TargetFront = v.NextFloat(),
            PetLocation = v.NextFloatArray(3),
            PetTargetLocation = v.NextFloatArray(3),
            PetFront = v.NextFloat(),
            PetSort = v.NextInt(),
            TargetObjectSort = v.NextInt(),
            TargetObjectIndex = v.NextInt(),
            TargetObjectUniqueNumber = v.NextInt(),
            SkillNumber = v.NextInt(),
            SkillGradeNum1 = v.NextInt(),
            SkillGradeNum2 = v.NextInt(),
            SkillValue = v.NextInt()
        };

        var buffer = new byte[ActionInfo.WireSize];
        var written = action.Write(buffer);
        Assert.Equal(ActionInfo.WireSize, written);

        Assert.True(ActionInfo.TryRead(buffer, out var roundTripped));
        StructuralAssert.DeepEqual(action, roundTripped);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var buffer = new byte[ActionInfo.WireSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 42);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(8, 4), 3.5f);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(28, 4), 9.25f);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(68, 4), 12.5f);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(100, 4), 777);

        var ok = ActionInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(42, packet.Type);
        Assert.Equal(3.5f, packet.Frame);
        Assert.Equal(9.25f, packet.TargetLocation[1]);
        Assert.Equal(12.5f, packet.PetFront);
        Assert.Equal(777, packet.SkillValue);
    }
}
