using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const DisconnectReason Zone175TerminalDisconnectReason = DisconnectReason.LabyrinthMissionEnded;

    private const int Zone175MoneyChangeSort = 23;

    private const int Zone175ExperienceChangeSort = 1;

    private const int Zone175ContributionPointChangeSort = 3;

    public bool HasAnyZone175QualifyingPlayer()
    {
        foreach (var (_, state) in _players)
            if (Zone175EligibilityRules.IsPresent(state))
                return true;

        return false;
    }

    public int CountLivingZone175WaveBosses(byte specialType)
    {
        _ = specialType;

        var count = 0;
        foreach (var (_, monster) in _monsters)
            if (Zone175RewardTables.IsWaveBossSpecialType(monster.Template.SpecialType))
                count++;

        return count;
    }

    public void GrantZone175WaveReward(int stage, int experienceRatio)
    {
        var money = Math.Min(Zone175RewardTables.MoneyForStage(stage), StoreMoneyPolicy.MaxMoney);
        var contributionPoints = Zone175RewardTables.ContributionPointsForStage(stage);

        foreach (var (_, state) in _players)
        {
            if (!Zone175EligibilityRules.IsRewardEligible(state))
                continue;

            if (state.IsStunned)
            {
                state.IsStunned = false;
                state.StunDurationTicks = 0;
            }

            var experience = Zone175RewardTables.WaveClearExperience(state.Level, state.Level2, experienceRatio);
            if (experience > 0)
            {
                ApplyCharacterExperienceGain(state, Math.Min(experience, int.MaxValue));
                state.Session.Send(new AvatarStatUpdateResponse
                {
                    Sort = Zone175ExperienceChangeSort,
                    Value = experience,
                    Value2 = 0
                });
            }

            if (money > 0)
            {
                QueueMoneyGrant(state.CharacterId, money);

                state.Session.Send(new AvatarStatUpdateResponse
                {
                    Sort = Zone175MoneyChangeSort,
                    Value = (int)Math.Min(money, int.MaxValue),
                    Value2 = 0
                });
            }

            foreach (var item in Zone175RewardTables.ItemsForStage(stage))
                SpawnGroundItem(item.ItemId, item.Quantity, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);

            if (contributionPoints != 0)
            {
                GrantContributionPoints(state.CharacterId, contributionPoints);
                state.Session.Send(new AvatarStatUpdateResponse
                {
                    Sort = Zone175ContributionPointChangeSort,
                    Value = contributionPoints,
                    Value2 = 0
                });
            }

            state.Zone175BossDamage = 0;
        }
    }

    public void ForceDisconnectAllForZone175()
    {
        List<IPacketSession>? toKick = null;
        foreach (var (_, state) in _players)
            if (state.Session is { } client)
                (toKick ??= []).Add(client);

        if (toKick is null)
            return;

        foreach (var client in toKick)
            client.Abort(Zone175TerminalDisconnectReason);
    }
}
