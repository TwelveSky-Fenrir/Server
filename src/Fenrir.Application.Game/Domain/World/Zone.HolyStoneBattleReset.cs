using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int HolyStoneBattleRankResetInboxCapacity = 4;

    private const int RankPointClearedStatSort = 66;

    private const int RankPointDateResetStatSort = 67;

    private const int RankBuffClearedStatSort = 68;

    private readonly Channel<HolyStoneBattleRankResetZoneCommand> _holyStoneBattleRankResetInbox =
        Channel.CreateBounded<HolyStoneBattleRankResetZoneCommand>(
            new BoundedChannelOptions(HolyStoneBattleRankResetInboxCapacity)
                { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    public bool PostHolyStoneBattleRankReset()
    {
        return _holyStoneBattleRankResetInbox.Writer.TryWrite(default);
    }

    private void DrainHolyStoneBattleRankResetCommands(int maximum)
    {
        for (var processed = 0;
             processed < maximum && _holyStoneBattleRankResetInbox.Reader.TryRead(out _);
             processed++)
            try
            {
                ApplyHolyStoneBattleRankReset();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} HSB cluster-wide rank reset failed", MapId);
            }
    }

    private void ApplyHolyStoneBattleRankReset()
    {
        var today = GameDate.Today();

        foreach (var state in _players.Values)
        {
            if (state.IsMovingZone)
            {
                if (state.RankPointDate == today)
                    continue;

                state.RankPointDate = today;
                state.RankPoint = 0;
                state.RankBuffType = 0;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                continue;
            }

            if (state.RankPoint > 0)
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = RankPointClearedStatSort, Value = 0, Value2 = 0 });
            state.RankPoint = 0;

            state.RankPointDate = today;
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = RankPointDateResetStatSort, Value = today, Value2 = 0 });


            if (state.RankBuffType != 0)
            {
                state.RankBuffType = 0;
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = RankBuffClearedStatSort, Value = 0, Value2 = 0 });

                var changedSlots = state.BuffChangeScratch;
                Array.Clear(changedSlots);
                RecomputeStatsAndBroadcastBuffs(state, changedSlots);
            }

            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }
    }
}

public readonly record struct HolyStoneBattleRankResetZoneCommand;
