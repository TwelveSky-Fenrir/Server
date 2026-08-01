using System.Buffers;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int KillFeedBroadcastSort = 3000;

    private static readonly ImmutableArray<int> EndOfBattleLeaderboardCpByRank = [100, 50, 25];

    private KillFeedLeaderboard? _killFeedLeaderboard;

    private KillFeedLeaderboard? ResolveKillFeedLeaderboard()
    {
        if (_killFeedLeaderboard is not null)
            return _killFeedLeaderboard;

        return KillFeedZoneCatalog.HasLeaderboardStore(MapId) ? _killFeedLeaderboard = new KillFeedLeaderboard() : null;
    }

    public void RecordEnemyKillForFeed(PlayerRuntimeState killer, PlayerRuntimeState victim, bool isStunTrigger,
        bool warStateActive)
    {
        if (isStunTrigger)
            return;

        if (killer.GuildId is { } killerGuildId && RegularWarMapCatalog.TryGet(MapId, out _))
            fourGuildKillPointQueue?.Enqueue(killerGuildId);

        var leaderboard = ResolveKillFeedLeaderboard();
        if (leaderboard is null)
            return;

        var top3 = ImmutableArray<KillFeedRankedEntry>.Empty;
        if (warStateActive)
        {
            killer.SessionKillCount++;
            leaderboard.RecordKill(killer.CharacterId, killer.Name, killer.Tribe, killer.SessionKillCount);
            top3 = leaderboard.GetTopThree();
        }

        if (KillFeedZoneCatalog.IsFeedEnabled(MapId))
            BroadcastKillFeed(killer, victim, top3);
    }

    private void BroadcastKillFeed(PlayerRuntimeState killer, PlayerRuntimeState victim,
        ImmutableArray<KillFeedRankedEntry> top3)
    {
        var payload = KillFeedBroadcastPayload.Create(killer.Name, killer.Tribe, victim.Name, victim.Tribe, top3);
        var response = new ZoneEventInfoResponse
            { Sort = KillFeedBroadcastSort, Data = KillFeedBroadcastEncoder.Encode(payload) };

        var total = FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var player in _players.Values)
                try
                {
                    if (TryGetZoneWideBroadcastRecipient(player.CharacterId, out var clientSession))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Kill-feed broadcast to character {RecipientId} in zone {MapId} failed",
                        player.CharacterId, MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public void ApplyKillFeedEndOfBattleRewards(bool isFfaMap, bool isZone267)
    {
        var leaderboard = _killFeedLeaderboard;
        if (leaderboard is null)
            return;

        var top3 = leaderboard.GetTopThree();
        var rewardedRanks = Math.Min(EndOfBattleLeaderboardCpByRank.Length, top3.Length);

        for (var rank = 0; rank < rewardedRanks; rank++)
        {
            if (!_players.TryGetValue(top3[rank].CharacterId, out var state))
                continue;

            if (state.IsMovingZone || state.VisibleState == 0)
                continue;

            GrantContributionPoints(state.CharacterId, EndOfBattleLeaderboardCpByRank[rank]);
        }
    }

    public void ClearKillFeedLeaderboard()
    {
        _killFeedLeaderboard?.Clear();
    }
}
