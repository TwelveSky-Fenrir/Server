using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     Covers <see cref="AutoHuntConfigValidator" /> -- the server-side validation of the client-supplied 112-byte
///     AUTO_HUNT blob (the security-hardening target: legacy stored it verbatim with no field validation).
/// </summary>
public class AutoHuntConfigValidatorTests
{
    private static AutoHunt Valid()
    {
        return new AutoHunt
        {
            BuffType = 0, BuffStore = new int[16], HuntType = 0, AttackType = new int[4],
            MonNum = 0, ItemType = 0, InvenCmd = 0, DeathCmd = 0, AnimalPreyCmd = 0, AnimalFoodCmd = 0
        };
    }

    [Fact]
    public void AllZeroBlob_IsValid()
    {
        var result = AutoHuntConfigValidator.Validate(Valid());

        Assert.True(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.None, result.Rejection);
    }

    [Fact]
    public void WellFormedPopulatedBlob_IsValid()
    {
        var config = Valid() with
        {
            BuffType = 3, HuntType = 1, MonNum = 5, ItemType = 2,
            BuffStore = [82, 10, 83, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            AttackType = [15, 6, 0, 0],
            InvenCmd = 1, DeathCmd = 0, AnimalPreyCmd = 1, AnimalFoodCmd = 1
        };

        Assert.True(AutoHuntConfigValidator.Validate(config).IsValid);
    }

    [Fact]
    public void NegativeBuffStoreSkillId_Rejected()
    {
        var config = Valid() with { BuffStore = [-1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.NegativeSkillId, result.Rejection);
    }

    [Fact]
    public void NegativeBuffStoreGrade_Rejected()
    {
        var config = Valid() with { BuffStore = [82, -5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.NegativeGrade, result.Rejection);
    }

    [Fact]
    public void NegativeAttackSkillId_Rejected()
    {
        var config = Valid() with { AttackType = [-9, 0, 0, 0] };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.NegativeSkillId, result.Rejection);
    }

    [Fact]
    public void NegativeAttackGrade_Rejected()
    {
        var config = Valid() with { AttackType = [15, -1, 0, 0] };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.NegativeGrade, result.Rejection);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void NegativeSelectorOrCount_Rejected(int buffType, int huntType, int itemType, int monNum)
    {
        var config = Valid() with { BuffType = buffType, HuntType = huntType, ItemType = itemType, MonNum = monNum };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.NegativeSelectorOrCount, result.Rejection);
    }

    [Theory]
    [InlineData(2, 0, 0, 0)]
    [InlineData(0, 2, 0, 0)]
    [InlineData(0, 0, 2, 0)]
    [InlineData(0, 0, 0, 2)]
    [InlineData(-1, 0, 0, 0)]
    public void CommandFlagOutsideBooleanDomain_Rejected(int inven, int death, int prey, int food)
    {
        var config = Valid() with
        {
            InvenCmd = inven, DeathCmd = death, AnimalPreyCmd = prey, AnimalFoodCmd = food
        };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.FlagOutOfDomain, result.Rejection);
    }

    [Fact]
    public void WrongLengthBuffStore_RejectedAsMalformedShape()
    {
        var config = Valid() with { BuffStore = new int[15] };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.MalformedShape, result.Rejection);
    }

    [Fact]
    public void WrongLengthAttackType_RejectedAsMalformedShape()
    {
        var config = Valid() with { AttackType = new int[3] };

        var result = AutoHuntConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Equal(AutoHuntConfigValidator.Rejection.MalformedShape, result.Rejection);
    }
}
