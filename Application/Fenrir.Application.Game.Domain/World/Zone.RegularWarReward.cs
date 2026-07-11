using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     Regular War (Zone049) battle-end reward-loop ground-item drops -- item 695 (White Feather) and item
    ///     8102 (Stone of Costume 30%), quantity 1 each, granted unconditionally to every non-draw participant
    ///     regardless of win/loss.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5376-5402 (C15-regwar-reward contract).</remarks>
    private const int RegularWarRewardItemWhiteFeather = 695;

    private const int RegularWarRewardItemCostumeStone30Percent = 8102;

    /// <summary>
    ///     <c>S001CHARACTER_EXP</c> (<c>Server/Header/Protocol/STRUCT.h:1516</c>) -- Sort on
    ///     <see cref="AvatarStatUpdateResponse" />, the Regular-War-reward-loop-specific experience-change
    ///     notification (a delta, not a running total -- distinct from <see cref="ExperienceStatSort" />'s own
    ///     always-fires Exp1-total push, which <see cref="ApplyCharacterExperienceGain" /> still also sends
    ///     unconditionally as part of the same call; both are reproduced rather than picking one, since nothing
    ///     in the C15 contract suggests the generic Exp1 push is suppressed for this specific source).
    /// </summary>
    private const int RegularWarExperienceChangeSort = 1;

    /// <summary>
    ///     <c>S003CONTRIBUTION_POINT</c> (<c>Server/Header/Protocol/STRUCT.h:1518</c>) -- Sort on
    ///     <see cref="AvatarStatUpdateResponse" />. <see cref="GrantContributionPoints" /> itself sends no
    ///     notification on its own (verified this session), so this handler sends the delta notification
    ///     directly at each of its two distinct call sites (the per-instance CP bonus and the top-3 leaderboard
    ///     CP), matching the C15 contract's own "each carrying a delta (not a new total)" framing.
    /// </summary>
    private const int RegularWarContributionPointChangeSort = 3;

    /// <summary>
    ///     <c>S023MONEY</c> (<c>Server/Header/Protocol/STRUCT.h:1538</c>) -- Sort on
    ///     <see cref="AvatarStatUpdateResponse" />, carrying the amount added, never the resulting balance
    ///     (contract's own explicit wording). Sent synchronously right after <see cref="QueueMoneyGrant" />
    ///     queues the write-behind grant; if that grant is later silently dropped by the overflow check inside
    ///     the write-behind flush pipeline (Zone.Monsters.cs/<c>MonsterLootFlushHost</c>), this notification will
    ///     already have gone out -- the same accepted, documented tradeoff
    ///     <see cref="ZoneWar.RegularWarRewardCalculator" />'s own remarks already make for not re-checking
    ///     overflow at grant-compute time (see also <c>Zone.Zone175Labyrinth.GrantZone175WaveReward</c>'s own gap
    ///     note for the same underlying architectural limit: no in-memory current-money balance exists on
    ///     <see cref="PlayerRuntimeState" /> to check against here).
    /// </summary>
    private const int RegularWarMoneyChangeSort = 23;

    /// <summary>
    ///     <c>AddHeroRankPoint</c>'s own combined-level (base level + rebirth/grade tier) gate
    ///     (Server/ts25zone/UpperCom/S06_MyUpperCom02.cpp:774-808, cited by the C15 contract) -- below this, no
    ///     hero-rank contribution at all, silently. A different, better-sourced number than
    ///     <see cref="Fenrir.Application.Game.Domain.Combat.PvpKillRewardZoneCatalog.HeroPointMinimumCombinedLevel" />
    ///     (which defaults to 0
    ///     specifically because no legacy value was available to THAT contract) -- kept as its own local
    ///     constant here rather than reusing that one, since the two gate unrelated call sites and conflating
    ///     them would misattribute this contract's own citation to a different one's placeholder.
    /// </summary>
    private const int RegularWarHeroRankMinimumCombinedLevel = 113;

    /// <summary>
    ///     <see cref="ZoneCommandKind.ApplyRegularWarReward" />'s handler (pending wiring -- see this cluster's
    ///     report) -- the ONLY site permitted to apply a Regular War (Zone049) battle-end reward-loop grant,
    ///     preserving the single-writer-per-zone invariant (this runs on the zone's own tick thread; the poster,
    ///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarRewardGrantSink</c>, runs on
    ///     <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.RegularWarSchedulerHost" />'s own
    ///     background-timer thread and never touches <see cref="PlayerRuntimeState" /> directly -- same
    ///     cross-thread precedent as <see cref="HandleRegularWarConclusionCredit" />/
    ///     <see cref="HandleGrantValleyWarRewardDrop" />). A no-op if the character already left this zone, or is
    ///     mid zone-transfer, between the reward-loop snapshot and this command draining.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5293-5413 (C15-regwar-reward contract's own reward-loop
    ///         citation range) -- applies exactly the amounts <see cref="RegularWarRewardCalculator.Compute" />
    ///         already computed; this handler performs no further formula work, only the mutation + notification
    ///         for each already-decided field on <paramref name="grant" />.
    ///     </para>
    ///     <para>
    ///         <b>Money/experience/CP amounts stay bounded at their existing sinks</b> -- <see cref="QueueMoneyGrant" />'s
    ///         write-behind overflow check for money, <see cref="GrantContributionPoints" />'s zero-floor for CP.
    ///         No further clamping happens here: every field on <paramref name="grant" /> is a server-computed
    ///         value from a tick-driven pipeline, never a client-supplied packet field, so there is nothing here
    ///         that needs a fresh bound.
    ///     </para>
    ///     <para>
    ///         <b>Still gated on the 12-row money table / experience-formula gap.</b> <paramref name="grant" />'s
    ///         <see cref="RegularWarRewardGrant.MoneyAmount" />/<see cref="RegularWarRewardGrant.ExperienceAmount" />
    ///         are computed by whatever <see cref="IRegularWarRewardValueProvider" /> the scheduler host was
    ///         constructed with -- <c>UnavailableRegularWarRewardValueProvider</c> (always zero) today, since
    ///         neither the C15 contract nor any contract before it supplies the real 12-row money table or the
    ///         3-band <c>GetBattleExpReward</c> experience-formula's literal per-band coefficients. This handler
    ///         applies whatever value it is handed either way -- a future contract supplying those literals needs
    ///         no change here, only a new <see cref="IRegularWarRewardValueProvider" /> registration.
    ///     </para>
    /// </remarks>
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
        }

        if (grant.RequestItemDrop)
        {
            SpawnGroundItem(RegularWarRewardItemWhiteFeather, 1, state.PosX, state.PosY, state.PosZ, state.Name,
                "", 0);
            SpawnGroundItem(RegularWarRewardItemCostumeStone30Percent, 1, state.PosX, state.PosY, state.PosZ,
                state.Name, "", 0);
        }
    }
}
