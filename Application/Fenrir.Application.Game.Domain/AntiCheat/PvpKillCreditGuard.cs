namespace Fenrir.Application.Game.Domain.AntiCheat;

public enum KillCreditDenial : byte
{
    None = 0,
    NotReady = 1,
    SameOrigin = 2,
    LevelGap = 3
}

public static class PvpKillCreditGuard
{

        public const int MaxKillerCombinedLevelAdvantage = 13;

        public static bool AreBothReady(bool killerReady, bool victimReady)
    {
        return killerReady && victimReady;
    }

        public static bool IsSameOrigin(string? killerIp, string? victimIp, int? killerAccountId = null,
        int? victimAccountId = null)
    {
        if (killerAccountId is { } killer && victimAccountId is { } victim && killer == victim)
            return true;

        return SessionSourceIp.AreSameHost(killerIp, victimIp);
    }

        public static bool ExceedsLevelGap(int killerCombinedLevel, int victimCombinedLevel)
    {
        return killerCombinedLevel - victimCombinedLevel > MaxKillerCombinedLevelAdvantage;
    }

        public static KillCreditDenial Evaluate(in PvpKillCreditRequest request)
    {
        if (!AreBothReady(request.KillerReady, request.VictimReady))
            return KillCreditDenial.NotReady;

        if (IsSameOrigin(request.KillerSourceIp, request.VictimSourceIp, request.KillerAccountId,
                request.VictimAccountId))
            return KillCreditDenial.SameOrigin;

        if (ExceedsLevelGap(request.KillerCombinedLevel, request.VictimCombinedLevel))
            return KillCreditDenial.LevelGap;

        return KillCreditDenial.None;
    }

        public static bool IsCreditAllowed(in PvpKillCreditRequest request)
    {
        return Evaluate(request) == KillCreditDenial.None;
    }
}

public readonly record struct PvpKillCreditRequest(
    bool KillerReady,
    bool VictimReady,
    string? KillerSourceIp,
    string? VictimSourceIp,
    int? KillerAccountId,
    int? VictimAccountId,
    int KillerCombinedLevel,
    int VictimCombinedLevel);
