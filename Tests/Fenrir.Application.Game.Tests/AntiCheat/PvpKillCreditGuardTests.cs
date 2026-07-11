using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Tests.AntiCheat;

public class PvpKillCreditGuardTests
{
    private static PvpKillCreditRequest Request(
        bool killerReady = true,
        bool victimReady = true,
        string? killerIp = "203.0.113.10",
        string? victimIp = "203.0.113.11",
        int? killerAccount = 100,
        int? victimAccount = 200,
        int killerLevel = 50,
        int victimLevel = 50)
    {
        return new PvpKillCreditRequest(killerReady, victimReady, killerIp, victimIp, killerAccount,
            victimAccount, killerLevel, victimLevel);
    }

    [Fact]
    public void AllGuardsPass_CreditAllowed()
    {
        var result = PvpKillCreditGuard.Evaluate(Request());
        Assert.Equal(KillCreditDenial.None, result);
        Assert.True(PvpKillCreditGuard.IsCreditAllowed(Request()));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void EitherPartyNotReady_NoCredit(bool killerReady, bool victimReady)
    {
        Assert.Equal(KillCreditDenial.NotReady,
            PvpKillCreditGuard.Evaluate(Request(killerReady, victimReady)));
    }

    [Fact]
    public void SameSourceIp_NoCredit()
    {
        var result = PvpKillCreditGuard.Evaluate(Request(killerIp: "198.51.100.5", victimIp: "198.51.100.5",
            killerAccount: 100, victimAccount: 200));
        Assert.Equal(KillCreditDenial.SameOrigin, result);
    }

    [Fact]
    public void SameAccount_DifferentIp_NoCredit_HardeningOverLegacyIpOnly()
    {
        var result = PvpKillCreditGuard.Evaluate(Request(killerIp: "203.0.113.10", victimIp: "203.0.113.99",
            killerAccount: 777, victimAccount: 777));
        Assert.Equal(KillCreditDenial.SameOrigin, result);
    }

    [Fact]
    public void NullAccounts_DifferentIps_FallsBackToLegacyIpOnly_Allows()
    {
        var result = PvpKillCreditGuard.Evaluate(Request(killerIp: "203.0.113.10", victimIp: "203.0.113.99",
            killerAccount: null, victimAccount: null));
        Assert.Equal(KillCreditDenial.None, result);
    }

    [Fact]
    public void IsSameOrigin_UnknownIps_NoAccounts_IsFalse()
    {
        Assert.False(PvpKillCreditGuard.IsSameOrigin(null, null));
        Assert.False(PvpKillCreditGuard.IsSameOrigin("", ""));
    }

    [Theory]
    [InlineData(64, 50, KillCreditDenial.LevelGap)]
    [InlineData(63, 50, KillCreditDenial.None)]
    [InlineData(50, 50, KillCreditDenial.None)]
    [InlineData(30, 50, KillCreditDenial.None)]
    public void LevelGap_IsDirectionalAndBoundedAtThirteen(int killerLevel, int victimLevel,
        KillCreditDenial expected)
    {
        var result = PvpKillCreditGuard.Evaluate(Request(killerLevel: killerLevel, victimLevel: victimLevel));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExceedsLevelGap_MatchesConstant()
    {
        Assert.Equal(13, PvpKillCreditGuard.MaxKillerCombinedLevelAdvantage);
        Assert.True(PvpKillCreditGuard.ExceedsLevelGap(64, 50));
        Assert.False(PvpKillCreditGuard.ExceedsLevelGap(63, 50));
    }
}
