using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

        private const DisconnectReason Zone175TerminalDisconnectReason = DisconnectReason.LabyrinthMissionEnded;

        public bool HasAnyZone175QualifyingPlayer()
    {
        foreach (var (_, state) in _players)
            if (Zone175EligibilityRules.IsPresent(state))
                return true;

        return false;
    }

        public int CountLivingZone175WaveBosses(byte specialType)
    {
        var count = 0;
        foreach (var (_, monster) in _monsters)
            if (monster.Template.SpecialType == specialType)
                count++;

        return count;
    }

        public void RemoveZone175MissionMonsters()
    {
        foreach (var (index, monster) in _monsters)
            if (Zone175RewardTables.IsWaveBossSpecialType(monster.Template.SpecialType) &&
                _monsters.TryRemove(index, out _))
                RemoveMonsterFromGrid(monster);
    }

        public void GrantZone175WaveReward(int stage, float experienceRatio)
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
                state.StunDurationSeconds = 0;
            }

            var experience = Zone175RewardTables.WaveClearExperience(state.RebirthCount, experienceRatio);
            if (experience > 0)
            {
                state.Experience += experience;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            }

            if (money > 0)
                QueueMoneyGrant(state.CharacterId, money);


            if (contributionPoints != 0)
                GrantContributionPoints(state.CharacterId, contributionPoints);

            state.Zone175BossDamage = 0;
        }
    }

        public void ForceDisconnectAllForZone175()
    {
        List<ClientSession>? toKick = null;
        foreach (var (_, state) in _players)
            if (state.Session is ClientSession client)
                (toKick ??= []).Add(client);

        if (toKick is null)
            return;

        foreach (var client in toKick)
            client.Abort(Zone175TerminalDisconnectReason);
    }
}
