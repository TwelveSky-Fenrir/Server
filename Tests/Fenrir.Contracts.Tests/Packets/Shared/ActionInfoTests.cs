using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Shared;

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
}
