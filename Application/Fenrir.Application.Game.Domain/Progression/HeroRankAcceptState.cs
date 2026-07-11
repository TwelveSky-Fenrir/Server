namespace Fenrir.Application.Game.Domain.Progression;

public enum HeroRankAcceptState : byte
{

        Unclaimed = 0,

        ClaimedPendingSettlement = 1,

        Settled = 2
}

public static class HeroRankAcceptStateRules
{

        public static bool IsClaimable(HeroRankAcceptState state)
    {
        return state == HeroRankAcceptState.Unclaimed;
    }

        public static bool IsClaimed(HeroRankAcceptState state)
    {
        return state is HeroRankAcceptState.ClaimedPendingSettlement or HeroRankAcceptState.Settled;
    }

        public static HeroRankAcceptState PromoteToSettled(HeroRankAcceptState state)
    {
        return state == HeroRankAcceptState.ClaimedPendingSettlement ? HeroRankAcceptState.Settled : state;
    }

        public static HeroRankAcceptState FromClaimedFlag(bool? rewardClaimed)
    {
        return rewardClaimed == true ? HeroRankAcceptState.Settled : HeroRankAcceptState.Unclaimed;
    }

        public static bool ToClaimedFlag(HeroRankAcceptState state)
    {
        return IsClaimed(state);
    }
}
