using Fenrir.Application.Game.Skills;

namespace Fenrir.Application.Game.Tests.Skills;

public class AutoBuffActivationResolverTests
{
    private const int Today = 20260704;
    private const int Yesterday = 20260703;

    [Fact]
    public void Sort1_ValidPreconditions_ActivatesAndReducesManaByNinetyPercent()
    {
        var ctx = new AutoBuffActivationResolver.Context(Today, 1, 100);

        var result = AutoBuffActivationResolver.Resolve(1, in ctx, Today);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Activate, result.Kind);
        Assert.Equal(10, result.ManaAfterActivation);
    }

    [Fact]
    public void Sort1_AutoBuffTimeExpired_NoReply()
    {
        var ctx = new AutoBuffActivationResolver.Context(Yesterday, 1, 100);

        var result = AutoBuffActivationResolver.Resolve(1, in ctx, Today);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Sort1_NotIdle_NoReply()
    {
        var ctx = new AutoBuffActivationResolver.Context(Today, 30, 100);

        var result = AutoBuffActivationResolver.Resolve(1, in ctx, Today);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.NoReply, result.Kind);
    }

    [Fact]
    public void Sort1_NegativeMana_DisconnectsPerLegacyQuirk()
    {
        var ctx = new AutoBuffActivationResolver.Context(Today, 1, -10);

        var result = AutoBuffActivationResolver.Resolve(1, in ctx, Today);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void Sort2_IsAlwaysATick()
    {
        var ctx = new AutoBuffActivationResolver.Context(Yesterday, 0, 0);

        var result = AutoBuffActivationResolver.Resolve(2, in ctx, Today);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Tick, result.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void UnsupportedSort_Disconnects(int sort)
    {
        var ctx = new AutoBuffActivationResolver.Context(Today, 1, 100);

        var result = AutoBuffActivationResolver.Resolve(sort, in ctx, Today);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Disconnect, result.Kind);
    }
}
