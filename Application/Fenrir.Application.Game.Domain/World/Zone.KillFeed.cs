using System.Buffers;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int KillFeedBroadcastSort = 3000;

    private readonly HashSet<int> _ffaParticipantBundleGranted = [];

    private KillCooldownTracker _ffaKillFeedAntiFarmCooldown = new();

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

        if (warStateActive && MapId == KillFeedZoneCatalog.FfaMapNumber)
            ApplyFfaKillFeedPointsAward(killer, victim);
    }

    private void ApplyFfaKillFeedPointsAward(PlayerRuntimeState killer, PlayerRuntimeState victim)
    {
        if (!_ffaKillFeedAntiFarmCooldown.TryRegisterKill(killer.CharacterId, victim.CharacterId, DateTime.UtcNow,
                KillFeedRewardConstants.FfaAntiFarmCooldown))
            return;

        GrantWarPoints(killer.CharacterId, KillFeedRewardConstants.FfaWarPointPerKill);
        GrantBloodPoints(killer.CharacterId, KillFeedRewardConstants.FfaBloodPointPerKill);
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
        foreach (var reward in KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(top3, isFfaMap, isZone267))
            if (_players.ContainsKey(reward.CharacterId))
                GrantContributionPoints(reward.CharacterId, reward.ContributionPoints);

        if (!isFfaMap)
            return;

        foreach (var player in _players.Values)
        {
            if (player.IsMovingZone)
                continue;
            if (!_ffaParticipantBundleGranted.Add(player.CharacterId))
                continue;

            logger.LogInformation(
                "KillFeed FFA participant bundle due for character {CharacterId} in zone {MapId} -- item grant not yet wired (placeholder ids only)",
                player.CharacterId, MapId);
        }
    }

    public void ClearKillFeedLeaderboard()
    {
        if (_killFeedLeaderboard is null)
            return;

        _killFeedLeaderboard.Clear();

        if (MapId != KillFeedZoneCatalog.FfaMapNumber)
            return;

        _ffaParticipantBundleGranted.Clear();
        _ffaKillFeedAntiFarmCooldown = new KillCooldownTracker();
    }
}
