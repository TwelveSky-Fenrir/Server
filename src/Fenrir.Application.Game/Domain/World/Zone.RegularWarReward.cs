using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int RegularWarRewardItemWhiteFeather = 695;

    private const int RegularWarRewardItemCostumeStone30Percent = 8102;

    private const int RegularWarExperienceChangeSort = 1;

    private const int RegularWarContributionPointChangeSort = 3;

    private const int RegularWarMoneyChangeSort = 23;

    private const int RegularWarHeroRankMinimumCombinedLevel = 113;

    private void HandleApplyRegularWarReward(in RegularWarRewardGrant grant)
    {
        if (!_players.TryGetValue(grant.CharacterId, out var state))
            return;

        if (state.IsMovingZone)
            return;

        if (grant.MoneyAmount > 0)
        {
            QueueMoneyGrant(state.CharacterId, grant.MoneyAmount);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = RegularWarMoneyChangeSort,
                Value = (int)Math.Min(grant.MoneyAmount, int.MaxValue),
                Value2 = 0
            });
        }

        if (grant.ExperienceAmount > 0)
        {
            ApplyCharacterExperienceGain(state, grant.ExperienceAmount);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = RegularWarExperienceChangeSort, Value = grant.ExperienceAmount, Value2 = 0
            });
        }

        if (grant.CpBonusAmount != 0)
        {
            GrantContributionPoints(state.CharacterId, grant.CpBonusAmount);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = RegularWarContributionPointChangeSort, Value = grant.CpBonusAmount, Value2 = 0
            });
        }

        if (grant.LeaderboardCpAmount != 0)
        {
            GrantContributionPoints(state.CharacterId, grant.LeaderboardCpAmount);
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = RegularWarContributionPointChangeSort, Value = grant.LeaderboardCpAmount, Value2 = 0
            });
        }

        if (grant.HeroRankPoints > 0 && state.Level + state.Level2 >= RegularWarHeroRankMinimumCombinedLevel)
        {
            state.HeroRankPoints += grant.HeroRankPoints;
            _heroRankPointAccumulator.AddPending(state.CharacterId, grant.HeroRankPoints, state.Tribe, state.Level);

            // Total absolu apres credit, unicast: Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:817
            state.Session.Send(new AvatarStatUpdateResponse
            {
                Sort = HeroRankPointStatSort, Value = state.HeroRankPoints, Value2 = 0
            });
        }

        if (grant.RequestItemDrop)
        {
            SpawnGroundItem(RegularWarRewardItemWhiteFeather, 1, state.PosX, state.PosY, state.PosZ, state.Name,
                "", 0);
            SpawnGroundItem(RegularWarRewardItemCostumeStone30Percent, 1, state.PosX, state.PosY, state.PosZ,
                state.Name, "", 0);
        }

        if (state.VisibleState != 0)
            CreditWaterfallOccupationQuest(state);
    }
}
