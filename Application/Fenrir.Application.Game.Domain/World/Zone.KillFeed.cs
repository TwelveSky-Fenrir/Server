using System.Buffers;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     C15: the kill-feed broadcast (opaque <c>ZoneEventInfoResponse</c> sub-sort 3000) and the persistent
///     per-instance top-3 killer leaderboard it reads from -- <see cref="Combat.KillFeedLeaderboard" /> owns
///     the data structure itself, <see cref="Combat.KillFeedZoneCatalog" /> decides which map ids get one
///     allocated/enabled, <see cref="Combat.KillFeedEndOfBattleRewardCalculator" /> computes the end-of-battle
///     top-1/2/3 CP reward this same leaderboard also feeds. This file is the orchestration seam a war-zone
///     kill site (<c>Zone.Combat.cs</c>'s ordinary enemy-tribe kill, <c>Zone.Combat.DamagePipeline.cs</c>'s
///     reflect-kill) and a battle-lifecycle owner (<c>ZoneWar.Zone335FfaEventCycleSystem</c>,
///     <c>ZoneWar.RegularWarSchedule</c>'s outcome consumer) are each expected to call into -- see this
///     mechanic's own wiring manifest for the exact call sites, none of which this file may edit itself.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:11,509 (the whole ranking subsystem is one
///     <c>
///         #ifdef
///         LNW33
///     </c>
///     block, unconditionally live -- Server/Header/Protocol/DEFINE.h:21-23) ; :514-552 (the
///     kill-increment step: calls the kill-feed routine, returns early -- no feed at all -- for a
///     "stun"-category kill) ; :1300-1332,1376-1402 (the two lethal enemy-kill call sites: reflect/
///     return-damage and ordinary).
/// </remarks>
public sealed partial class Zone
{
    private const int KillFeedBroadcastSort = 3000;

    /// <summary>
    ///     Per-battle "already granted the one-time FFA participant bundle" guard (source contract step 9) --
    ///     cleared alongside the leaderboard itself in <see cref="ClearKillFeedLeaderboard" />.
    /// </summary>
    private readonly HashSet<int> _ffaParticipantBundleGranted = [];

    /// <summary>
    ///     Own, independent 3-minute per-ordered-pair anti-farm gate for the FFA War Point/Blood Point award
    ///     (<see cref="KillFeedRewardConstants.FfaAntiFarmCooldown" />) -- deliberately mutable (not
    ///     <see langword="readonly" />), unlike every sibling <see cref="KillCooldownTracker" /> field on
    ///     <c>Zone.Combat.cs</c>: <see cref="ClearKillFeedLeaderboard" /> replaces this with a fresh instance
    ///     to reproduce the source contract's step-12 "entire anti-farm timestamp matrix" reset, since
    ///     <see cref="KillCooldownTracker" /> itself exposes no <c>Clear</c> method to call instead.
    /// </summary>
    private KillCooldownTracker _ffaKillFeedAntiFarmCooldown = new();

    private KillFeedLeaderboard? _killFeedLeaderboard;

    /// <summary>
    ///     Lazily allocates this zone's own <see cref="KillFeedLeaderboard" /> the first time it's needed, iff
    ///     <see cref="KillFeedZoneCatalog.HasLeaderboardStore" /> says this <see cref="MapId" /> is one of the
    ///     war-zone types that gets one at all -- matching the source contract's "the backing store stays
    ///     unallocated [on any non-war instance], and every ranking/reward routine early-returns doing
    ///     nothing." Safe to call from any of this file's methods; <see cref="MapId" /> never changes after
    ///     construction so the catalog check's result can never flip mid-lifetime.
    /// </summary>
    private KillFeedLeaderboard? ResolveKillFeedLeaderboard()
    {
        if (_killFeedLeaderboard is not null)
            return _killFeedLeaderboard;

        return KillFeedZoneCatalog.HasLeaderboardStore(MapId) ? _killFeedLeaderboard = new KillFeedLeaderboard() : null;
    }

    /// <summary>
    ///     The kill-site hook: call once per qualifying enemy-tribe (never duel) kill, immediately after the
    ///     defeat is otherwise fully resolved. Exposed as a single entry point covering both legacy call sites
    ///     (an ordinary lethal hit and a reflect/return-damage kill, with <paramref name="killer" />/
    ///     <paramref name="victim" /> already normalized to "the one who landed the final blow" /
    ///     "the one who died" for either case) -- see this mechanic's wiring manifest for exactly where each
    ///     site should call this.
    /// </summary>
    /// <param name="killer">The character credited with this kill.</param>
    /// <param name="victim">The character who died.</param>
    /// <param name="isStunTrigger">
    ///     The stun-vs-not collapse of the legacy's three-value kill-type marker (same parameter shape as
    ///     <c>ApplyPvpKillRewards</c>'s own <c>isStunTrigger</c>). A stun-triggered kill skips this whole
    ///     method entirely -- no feed line, no leaderboard update, no FFA points -- matching the source
    ///     contract's own "the kill-increment step is skipped entirely when the kill category is stun."
    /// </param>
    /// <param name="warStateActive">
    ///     Whether this zone's own war-state value currently equals the "active battle" code (state 3) --
    ///     caller-resolved rather than read from a collaborator this file owns, since the authoritative source
    ///     differs per zone type (Zone049-type: <c>regularWarActiveMapTracker.IsBattleInProgress</c> ; FFA-335:
    ///     <c>ZoneWar.Zone335FfaEventCycleSystem.Phase == Battle</c>, which has no accessor reachable from
    ///     <see cref="Zone" /> today -- see this mechanic's own Open Questions). When
    ///     <paramref name="warStateActive" /> is <see langword="false" />, the feed (if this zone's type
    ///     enables one) still broadcasts, but with an unconditionally blank top-3 and no leaderboard/counter
    ///     mutation at all -- matching the source contract's "Feed without ranking" edge case exactly.
    /// </param>
    public void RecordEnemyKillForFeed(PlayerRuntimeState killer, PlayerRuntimeState victim, bool isStunTrigger,
        bool warStateActive)
    {
        if (isStunTrigger)
            return;

        // C17 Part B1: +1 four-guild point for the killer's guild, Zone049-type maps only -- the narrower
        // subset of KillFeedZoneCatalog.HasLeaderboardStore's own zone-type union just below (267 and the
        // FFA map 335 never qualify; see FourGuildScoringService's own remarks, "the zone-049 event mode").
        // Independent of warStateActive/leaderboard eligibility: a guildless killer or a non-Zone049-type map
        // is a silent no-op here, and a null fourGuildKillPointQueue (most test call sites) degrades the same
        // way eventLogQueue does elsewhere in this class family.
        if (killer.GuildId is { } killerGuildId && RegularWarMapCatalog.TryGet(MapId, out _))
            fourGuildKillPointQueue?.Enqueue(killerGuildId);

        var leaderboard = ResolveKillFeedLeaderboard();
        if (leaderboard is null)
            return; // non-war instance / unmodeled zone-type -- silent no-op, matching every other gate here

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

    /// <summary>
    ///     FFA-335 per-kill War Point/Blood Point award (source contract steps 6-8) -- its own 3-minute,
    ///     per-ordered-pair anti-farm gate, entirely separate from every other cooldown table in
    ///     <c>Zone.Combat.cs</c> (the C05 10-minute gate, the FFA/Regular-War-host 2-minute flat-CP-override
    ///     gates). Reuses the already-public <see cref="GrantWarPoints" />/<see cref="GrantBloodPoints" />
    ///     (<c>Zone.Combat.cs</c>) for the actual mutation + client notification, so this method only owns the
    ///     cooldown gate and the two literal amounts.
    /// </summary>
    private void ApplyFfaKillFeedPointsAward(PlayerRuntimeState killer, PlayerRuntimeState victim)
    {
        if (!_ffaKillFeedAntiFarmCooldown.TryRegisterKill(killer.CharacterId, victim.CharacterId, DateTime.UtcNow,
                KillFeedRewardConstants.FfaAntiFarmCooldown))
            return; // repeat of this exact directed kill within the window -- silently no points, feed/rank unaffected

        GrantWarPoints(killer.CharacterId, KillFeedRewardConstants.FfaWarPointPerKill);
        GrantBloodPoints(killer.CharacterId, KillFeedRewardConstants.FfaBloodPointPerKill);
    }

    private void BroadcastKillFeed(PlayerRuntimeState killer, PlayerRuntimeState victim,
        ImmutableArray<KillFeedRankedEntry> top3)
    {
        var payload = KillFeedBroadcastPayload.Create(killer.Name, killer.Tribe, victim.Name, victim.Tribe, top3);
        var response = new ZoneEventInfoResponse
            { Sort = KillFeedBroadcastSort, Data = KillFeedBroadcastEncoder.Encode(payload) };

        // Rent-once/write-once/copy-N-times -- same idiom as BroadcastTowerStatus (Zone.Combat.cs). Scoped to
        // THIS zone's own connected players only, matching the source contract's "delivered to every
        // connected client in the instance" (not the cluster-wide fan-out ZoneWar.ZoneEventBroadcaster uses
        // for its own RvR sort codes).
        var total = FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var player in _players.Values)
                try
                {
                    if (player.Session is ClientSession clientSession)
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

    /// <summary>
    ///     End-of-battle top-1/2/3 CP reward (source contract steps 9-11), plus the FFA one-time participant
    ///     bundle (step 9) -- caller (a battle-lifecycle owner) invokes this exactly once per battle
    ///     conclusion, see this mechanic's own wiring manifest. A no-op when this zone never had a leaderboard
    ///     allocated in the first place (nothing to reward). <paramref name="isFfaMap" />/
    ///     <paramref name="isZone267" /> are caller-resolved rather than derived from <see cref="MapId" />
    ///     internally, matching the same "caller owns zone-type classification" posture as
    ///     <paramref name="warStateActive" /> on <see cref="RecordEnemyKillForFeed" />.
    /// </summary>
    public void ApplyKillFeedEndOfBattleRewards(bool isFfaMap, bool isZone267)
    {
        var leaderboard = _killFeedLeaderboard;
        if (leaderboard is null)
            return;

        var top3 = leaderboard.GetTopThree();
        foreach (var reward in KillFeedEndOfBattleRewardCalculator.ComputeRankRewards(top3, isFfaMap, isZone267))
            // "credited only to the player whose slot index matches the leaderboard entry at that rank" --
            // Fenrir's equivalent is "still present in this zone's own _players map."
            if (_players.ContainsKey(reward.CharacterId))
                GrantContributionPoints(reward.CharacterId, reward.ContributionPoints);

        if (!isFfaMap)
            return;

        foreach (var player in _players.Values)
        {
            if (player.IsMovingZone)
                continue;
            if (!_ffaParticipantBundleGranted.Add(player.CharacterId))
                continue; // already granted this battle -- source contract step 9's own per-player guard

            // TODO(wiring): grant KillFeedRewardConstants.FfaParticipantBundleItemIdsPlaceholder (four items)
            // to player.CharacterId's inventory. Deliberately NOT performed here: (1) those four item ids are
            // themselves unrecoverable placeholders per the source contract's own Open Question 1 -- never
            // invent real ones; (2) Zone has no existing "grant item by id to a live player's inventory"
            // entry point reachable from this partial file without editing an existing file/service. Logged
            // instead of silently dropped so the gap is observable.
            logger.LogInformation(
                "KillFeed FFA participant bundle due for character {CharacterId} in zone {MapId} -- item grant not yet wired (placeholder ids only)",
                player.CharacterId, MapId);
        }
    }

    /// <summary>
    ///     Battle open/reset/close leaderboard clear (source contract step 12) -- resets every leaderboard
    ///     entry to empty and, on the FFA map only, the participant-bundle "given" flags and the entire
    ///     anti-farm timestamp matrix (by replacing <see cref="_ffaKillFeedAntiFarmCooldown" /> outright, see
    ///     that field's own remarks). Deliberately does NOT touch any player's own
    ///     <see cref="PlayerRuntimeState.SessionKillCount" /> -- see that property's own remarks. A no-op when
    ///     this zone never had a leaderboard allocated.
    /// </summary>
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
