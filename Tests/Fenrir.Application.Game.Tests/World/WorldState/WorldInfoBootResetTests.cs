using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class WorldInfoBootResetTests
{
    private static WorldInfo Poisoned()
    {
        return WorldStateTemplates.ZeroedWorldInfo with
        {
            TribeVoteState = [7, 7, 7, 7],
            CloseVoteState = [7, 7, 7, 7],
            PossibleAllianceInfo = [7, 7, 7, 7, 7, 7, 7, 7],
            AllianceState = [7, 7, 7, 7],
            Zone038DTMValue = [7, 7, 7, 7],
            TribeNokSanStone = [7, 7, 7, 7],
            NokSanStoneState = [7, 7, 7, 7, 7, 7, 7, 7, 7],
            PopUpTypeState = [7, 7, 7, 7, 7],
            PopUpKillAvt = [7, 7, 7, 7, 7],
            PopUpKillMonster = [7, 7, 7, 7, 7],

            GuildBattle = 99,
            TribeSymbol = [1, 2, 3, 4],
            Zone049TypeState = [9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9],
            Zone241TypeState =
            [
                9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9
            ],
            TribeGeneralExperienceUpRatioInfo = [1.5f, 2.5f, 3.5f, 4.5f]
        };
    }

    [Fact]
    public void Apply_ZeroesVotes()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal([0, 0, 0, 0], result.TribeVoteState);
    }

    [Fact]
    public void Apply_ZeroesCloseVote()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal([0, 0, 0, 0], result.CloseVoteState);
    }

    [Fact]
    public void Apply_ZeroesAlliancePossibilityProposalSide()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal(new int[8], result.PossibleAllianceInfo);
    }

    [Fact]
    public void Apply_ZeroesDtm()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal([0, 0, 0, 0], result.Zone038DTMValue);
    }

    [Fact]
    public void Apply_ZeroesNokSan()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal([0, 0, 0, 0], result.TribeNokSanStone);
        Assert.Equal(new int[9], result.NokSanStoneState);
    }

    [Fact]
    public void Apply_ZeroesPopup()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal(new int[5], result.PopUpTypeState);
        Assert.Equal(new int[5], result.PopUpKillAvt);
        Assert.Equal(new int[5], result.PopUpKillMonster);
    }

    [Fact]
    public void Apply_LeavesAllianceStateUntouched_WhenNoSentinelProvided()
    {
        var result = WorldInfoBootReset.Apply(Poisoned());

        Assert.Equal([7, 7, 7, 7], result.AllianceState);
    }

    [Fact]
    public void Apply_FillsAllianceState_WhenSentinelExplicitlyProvided()
    {
        var result = WorldInfoBootReset.Apply(Poisoned(), -1);

        Assert.Equal([-1, -1, -1, -1], result.AllianceState);
    }

    [Fact]
    public void Apply_LeavesEveryUnrelatedFieldUntouched()
    {
        var poisoned = Poisoned();

        var result = WorldInfoBootReset.Apply(poisoned);

        Assert.Equal(99, result.GuildBattle);
        Assert.Equal(poisoned.TribeSymbol, result.TribeSymbol);
        Assert.Equal(poisoned.Zone049TypeState, result.Zone049TypeState);
        Assert.Equal(poisoned.Zone241TypeState, result.Zone241TypeState);
        Assert.Equal(poisoned.TribeGeneralExperienceUpRatioInfo, result.TribeGeneralExperienceUpRatioInfo);
    }

    [Fact]
    public void ZeroedTemplate_MatchesApplyOfZeroedWorldInfo()
    {
        var expected = WorldInfoBootReset.Apply(WorldStateTemplates.ZeroedWorldInfo);

        Assert.Equal(expected.TribeVoteState, WorldInfoBootReset.ZeroedTemplate.TribeVoteState);
        Assert.Equal(expected.CloseVoteState, WorldInfoBootReset.ZeroedTemplate.CloseVoteState);
        Assert.Equal(expected.PossibleAllianceInfo, WorldInfoBootReset.ZeroedTemplate.PossibleAllianceInfo);
        Assert.Equal(expected.AllianceState, WorldInfoBootReset.ZeroedTemplate.AllianceState);
        Assert.Equal(expected.Zone038DTMValue, WorldInfoBootReset.ZeroedTemplate.Zone038DTMValue);
        Assert.Equal(expected.TribeNokSanStone, WorldInfoBootReset.ZeroedTemplate.TribeNokSanStone);
        Assert.Equal(expected.NokSanStoneState, WorldInfoBootReset.ZeroedTemplate.NokSanStoneState);
        Assert.Equal(expected.PopUpTypeState, WorldInfoBootReset.ZeroedTemplate.PopUpTypeState);
        Assert.Equal(expected.PopUpKillAvt, WorldInfoBootReset.ZeroedTemplate.PopUpKillAvt);
        Assert.Equal(expected.PopUpKillMonster, WorldInfoBootReset.ZeroedTemplate.PopUpKillMonster);
    }

    [Fact]
    public void ZeroedTemplate_IsAllZeroForEveryTargetedGroup()
    {
        var template = WorldInfoBootReset.ZeroedTemplate;

        Assert.Equal([0, 0, 0, 0], template.TribeVoteState);
        Assert.Equal([0, 0, 0, 0], template.CloseVoteState);
        Assert.Equal(new int[8], template.PossibleAllianceInfo);
        Assert.Equal([0, 0, 0, 0], template.Zone038DTMValue);
        Assert.Equal([0, 0, 0, 0], template.TribeNokSanStone);
        Assert.Equal(new int[9], template.NokSanStoneState);
        Assert.Equal(new int[5], template.PopUpTypeState);
        Assert.Equal(new int[5], template.PopUpKillAvt);
        Assert.Equal(new int[5], template.PopUpKillMonster);
    }
}
