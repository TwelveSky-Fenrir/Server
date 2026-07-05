using System.Collections.Immutable;
using Fenrir.Application.Game.Skills;

namespace Fenrir.Application.Game.Tests.Skills;

public class AutoBuffSkillResolverTests
{
    private static int[] Flat(params (int SkillId, int Grade)[] slots)
    {
        var flat = new int[16];
        for (var i = 0; i < slots.Length; i++)
        {
            flat[i * 2] = slots[i].SkillId;
            flat[i * 2 + 1] = slots[i].Grade;
        }

        return flat;
    }

    [Fact]
    public void RequestedGradeAboveLearnedGrade_ClampsToLearnedGrade()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(100, 3));

        var result = AutoBuffSkillResolver.ResolveRegistration(Flat((100, 10)), learned);

        Assert.Equal((100, 3), result[0]);
    }

    [Fact]
    public void RequestedGradeAtOrBelowLearnedGrade_KeptAsRequested()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(100, 5));

        var result = AutoBuffSkillResolver.ResolveRegistration(Flat((100, 2)), learned);

        Assert.Equal((100, 2), result[0]);
    }

    [Fact]
    public void UnlearnedSkill_ClampsToNegativeOne()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty;

        var result = AutoBuffSkillResolver.ResolveRegistration(Flat((200, 5)), learned);

        Assert.Equal((200, -1), result[0]);
    }

    [Fact]
    public void AllEightSlots_AreIndependentlyResolved()
    {
        var learned = ImmutableDictionary<byte, LearnedSkill>.Empty
            .Add(0, new LearnedSkill(1, 1))
            .Add(1, new LearnedSkill(2, 9));

        var result = AutoBuffSkillResolver.ResolveRegistration(Flat((1, 5), (2, 5)), learned);

        Assert.Equal(AutoBuffSkillResolver.SlotCount, result.Length);
        Assert.Equal((1, 1), result[0]);
        Assert.Equal((2, 5), result[1]);
        for (var i = 2; i < AutoBuffSkillResolver.SlotCount; i++)
            Assert.Equal((0, -1), result[i]);
    }
}
