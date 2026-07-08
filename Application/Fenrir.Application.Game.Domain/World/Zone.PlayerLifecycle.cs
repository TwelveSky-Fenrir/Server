using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     <c>game.EventLog.EventCode</c> for a character death (any <see cref="DeathCause" />, including a
    ///     GM-forced one -- <see cref="ApplyDeath" /> logs unconditionally regardless of cause) -- an
    ///     app-owned numbering scheme scoped independently within <see cref="EventLogCategory.Death" /> (see
    ///     <c>game.EventLog.sql</c>'s own "app-owned numbering scheme" comment; EventCode is only ever
    ///     caller-interpreted alongside its Category, so reusing small values across categories is safe). The
    ///     cause itself rides in the row's Outcome byte (the <see cref="DeathCause" /> enum's own numeric
    ///     value) rather than a distinct EventCode per cause, so an investigation query can isolate "every
    ///     death" without also filtering by cause.
    /// </summary>
    private const short CharacterDeathEventCode = 1;

    /// <summary>
    ///     <c>game.EventLog.EventCode</c> for the XP-loss (or, at the level cap, CP-loss) consequence of a
    ///     monster-kill death -- see <see cref="ApplyDeathExperienceLoss" />. Distinct from
    ///     <see cref="CharacterDeathEventCode" /> so an investigation query can isolate "did this death cost
    ///     experience/CP" without parsing Payload.
    /// </summary>
    private const short DeathExperienceLossEventCode = 2;

    /// <summary><see cref="PendingDeathEventLog.Outcome" /> for the ordinary XP-range loss branch.</summary>
    private const byte ExperienceLossOutcome = 0;

    /// <summary><see cref="PendingDeathEventLog.Outcome" /> for the at-level-cap CP-loss branch.</summary>
    private const byte ContributionPointsLossOutcome = 1;

    /// <summary>
    ///     <see cref="Movement.CharacterMotionEvaluation.SkillCategoryCode" /> value
    ///     <see cref="Movement.CharacterMotionWhitelist" /> resolves for the real op15 skill-cast Sorts (32,
    ///     33, and the weapon-class-dependent bands 38-90) -- <see cref="ApplySkillCastManaCharge" />'s own
    ///     trigger. Category 1 (action-Sort 30, "stand up from death") is a DIFFERENT, non-mana-charging
    ///     category and must never be conflated with this one -- see the skill-casting-cooldown-mechanics
    ///     behavior contract.
    /// </summary>
    private const int SkillCastEffectCategoryCode = 2;

    /// <summary>
    ///     Action-category Sort for "rest"/stand-up (op15 only) -- <see cref="ApplyRestActionProtectionAndHeal" />'s own
    ///     trigger.
    /// </summary>
    private const int RestActionSort = 0;

    /// <summary>
    ///     Action-category Sort for op16 (CZ_UPDATE_AVATAR_ACTION)'s "create effect value" confirm --
    ///     <see cref="ApplySkillEffectConfirm" />'s own trigger. An unrelated value space from skill NUMBER
    ///     30 (one specific self-buff skill) and from action-Sort 30 (stand-up-from-death) -- do not
    ///     conflate any of these three.
    /// </summary>
    private const int SkillEffectConfirmActionSort = 1;

    /// <summary>
    ///     S010CHARACTER_HP -- <see cref="AvatarStatUpdateResponse.Sort" /> for the HP-changed notification
    ///     <see cref="ApplyRestActionProtectionAndHeal" /> sends (Server/Header/Protocol/STRUCT.h:1525).
    /// </summary>
    private const int CharacterHpStatSort = 10;

    /// <summary>
    ///     Released once per queued row so <c>DeathEventLogFlushHost</c> can flush as soon as one arrives
    ///     instead of waiting up to a full flush interval -- same signal pattern as <c>Zone.Monsters.cs</c>'s
    ///     own <c>_moneyGrantSignal</c>.
    /// </summary>
    private readonly SemaphoreSlim _deathEventLogSignal = new(0, int.MaxValue);

    /// <summary>
    ///     Kills <paramref name="characterId" /> in this zone: Life -&gt; 0, <see cref="PlayerRuntimeState.IsDead" />
    ///     set, and the territorial revive-eligibility gate armed (<see cref="DeathGateTickSystem" /> grants it
    ///     back via <see cref="GrantReviveEligibility" /> once eligible). Public and characterId-addressed so
    ///     the combat handler never needs its own <see cref="PlayerRuntimeState" /> reference -- only this
    ///     zone's own tick may construct/mutate one. A no-op if the character is not tracked here, or already
    ///     dead (so a duplicate killing blow never re-arms the death-gate timers).
    /// </summary>
    /// <remarks>
    ///     XP penalty on death is applied here, but only for <see cref="DeathCause.MonsterKill" /> -- a PvP
    ///     death instead rewards the killer (not implemented, see <see cref="Combat.CombatResolver" />'s
    ///     remarks) and does not dock the victim's XP.
    /// </remarks>
    /// <summary>
    ///     Reusable scratch buffer for <see cref="ApplyDeath" />'s death-pose broadcast recipient list -- same
    ///     non-allocating shape and reuse justification as <see cref="_moveNeighborScratch" />: single tick
    ///     thread, cleared before use, consumed entirely by the immediately-following broadcast call before
    ///     <see cref="ApplyDeath" /> returns.
    /// </summary>
    private readonly List<int> _deathNeighborScratch = [];

    /// <summary>
    ///     Reusable scratch buffer for <see cref="HandleEnter" />'s mutual-visibility neighbor list -- same
    ///     non-allocating shape and reuse justification as <see cref="_moveNeighborScratch" /> (single tick
    ///     thread; cleared before use; both consumers -- the direct per-neighbor send loop and the broadcast
    ///     call right after -- finish reading it before <see cref="HandleEnter" /> returns, so no reentrant use
    ///     can observe a half-built or already-cleared buffer).
    /// </summary>
    private readonly List<int> _enterNeighborScratch = [];

    /// <summary>
    ///     Reusable scratch buffer for <see cref="HandleMove" />'s neighbor-broadcast recipient list. Movement
    ///     (CZ_AVATAR_ACTION_SEND/CZ_UPDATE_AVATAR_ACTION) is the single highest-frequency player-driven event
    ///     this server handles, so computing that recipient list via <c>AoiGrid.Neighbors(...).Where(...).ToArray()</c>
    ///     on every accepted packet -- a fresh iterator, a per-call closure, and an array, every time -- is
    ///     worth avoiding in favor of one field reused across calls via <see cref="AoiGrid.NeighborsExcludingSelf" />.
    ///     Safe to reuse: <c>DrainInbox</c> processes queued zone commands one at a time on this zone's own
    ///     single tick thread (this class's own header remarks), so no two <see cref="HandleMove" /> calls, nor
    ///     anything else touching this field, ever overlap.
    /// </summary>
    private readonly List<int> _moveNeighborScratch = [];

    private readonly ConcurrentQueue<PendingDeathEventLog> _pendingDeathEventLogs = new();

    /// <summary>
    ///     Reusable scratch buffer for <see cref="RebroadcastAvatars" />'s per-player neighbor-broadcast
    ///     recipient list -- same non-allocating shape and reuse justification as <see cref="_moveNeighborScratch" />
    ///     (single tick thread, cleared immediately before each per-player use within the loop, never read after
    ///     the immediately-following broadcast call returns).
    /// </summary>
    private readonly List<int> _rebroadcastNeighborScratch = [];

    /// <summary>
    ///     Grants territorial revive-eligibility (side effect 1, <c>S07_MyGame04.cpp:257-327</c>): called by
    ///     <see cref="DeathGateTickSystem" /> once its own recheck succeeds for a player who has been dead at
    ///     least <see cref="SimulationClock.ReviveEligibilityLegacyTicks" /> legacy ticks. Clears every
    ///     death-gate flag together (anti-abuse flag off, potions re-permitted, death-in-progress flag off,
    ///     death sub-counter reset) and forces HP to 1 regardless of MaxLife, in place (same zone/position) --
    ///     the legacy only auto-clears state locally; an actual "return to town" transfer is always
    ///     client-driven (CZ_DEMAND_ZONE_SERVER_INFO_2), already handled by <c>ZoneMoveHandler</c>, never this
    ///     path. A no-op if the character is not (or no longer) dead. Deliberately independent of, and never a
    ///     substitute for, <see cref="ApplyRestActionProtectionAndHeal" />'s own Sort-0-triggered
    ///     PvP-immunity re-arm + one-third-max-HP restoration -- this tick-based mechanism never touches
    ///     <see cref="PlayerRuntimeState.ZoneEntryAtZoneClock" /> under any legacy branch (its own distinct
    ///     timer field, <c>mTickCountFor10Second</c>, is not the immunity-window field at all), so a revived
    ///     character has no PvP immunity and only 1 HP until it actually sends a Sort-0 action.
    /// </summary>
    /// <summary>
    ///     Reusable scratch buffer for <see cref="GrantReviveEligibility" />'s neighbor-broadcast recipient
    ///     list -- same non-allocating shape and reuse justification as <see cref="_moveNeighborScratch" />:
    ///     single tick thread, cleared before use, consumed entirely by the immediately-following broadcast
    ///     call before <see cref="GrantReviveEligibility" /> returns.
    /// </summary>
    private readonly List<int> _reviveNeighborScratch = [];

    /// <summary>
    ///     Queues a death-related <c>game.EventLog</c> row rather than awaiting
    ///     <see cref="IEventLogRepository.LogAsync" /> inline -- see <see cref="PendingDeathEventLog" />'s own
    ///     remarks for why.
    /// </summary>
    private void QueueDeathEventLog(short eventCode, int characterId, byte? outcome, string? payload)
    {
        _pendingDeathEventLogs.Enqueue(new PendingDeathEventLog(eventCode, characterId, options.ShardId, outcome,
            payload));
        _deathEventLogSignal.Release();
    }

    /// <summary>
    ///     Resolves as soon as a death-related event-log row is queued (or immediately, if one is already
    ///     pending un-awaited) -- lets <c>DeathEventLogFlushHost</c> race this against its own periodic timer
    ///     via <see cref="Task.WhenAny(Task[])" /> rather than only ever waking up on the timer's fixed cadence.
    /// </summary>
    public Task WaitForDeathEventLogAsync(CancellationToken ct)
    {
        return _deathEventLogSignal.WaitAsync(ct);
    }

    /// <summary>Callable from any thread; the only intended caller is <c>DeathEventLogFlushHost</c>.</summary>
    public IReadOnlyList<PendingDeathEventLog> DrainPendingDeathEventLogs()
    {
        if (_pendingDeathEventLogs.IsEmpty)
            return [];

        List<PendingDeathEventLog>? entries = null;
        while (_pendingDeathEventLogs.TryDequeue(out var entry))
            (entries ??= []).Add(entry);

        return (IReadOnlyList<PendingDeathEventLog>?)entries ?? [];
    }

    /// <summary>
    ///     Keep-alive rebroadcast: re-emits every avatar's current state to its surroundings every 3.5 s even
    ///     when idle, so late-arriving or packet-lossy neighbors converge. A dead avatar still has its
    ///     throttle timestamp stamped (so it never queues up a burst of catch-up broadcasts once revived) but
    ///     is never actually sent -- matching legacy's hidden-or-dead short-circuit inside the same avatar
    ///     loop (<c>S07_MyGame01.cpp:2432-2454</c>: the broadcast-throttle check stamps the timer unconditionally,
    ///     then skips the send itself for a hiding or dead avatar). Fenrir has no player-hidden/invisible state
    ///     yet, so only the death half of that legacy branch has an analog here.
    /// </summary>
    private void RebroadcastAvatars()
    {
        // Direct enumeration (no Values snapshot): ConcurrentDictionary's enumerator is lock-free, and the
        // tick thread is the only mutator anyway.
        foreach (var (characterId, state) in _players)
        {
            if (_clock - state.LastAvatarRebroadcastAt < SimulationClock.AvatarRebroadcastInterval)
                continue;

            state.LastAvatarRebroadcastAt = _clock;

            // Stamp-but-don't-send for a dead avatar (S07_MyGame01.cpp:2432-2454) -- a corpse still stuck in
            // ReviveEligibility limbo would otherwise re-walk the AOI grid and emit a keep-alive frame every
            // 3.5s for no client-visible reason. Distinct from IsReviveHackBroadcastSuppressed below, which
            // filters RECIPIENTS by their own unresolved-death state, not whether this SOURCE avatar is dead.
            if (state.IsDead)
                continue;

            // Uses _rebroadcastNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray() -- this
            // loop runs once per connected player every tick (gated by the 3.5s per-player interval), so the
            // per-player LINQ iterator/closure/array allocation was repeated for the whole zone population
            // every tick a rebroadcast came due.
            _rebroadcastNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_rebroadcastNeighborScratch, state.CurrentCell, characterId, state.PosX,
                state.PosY, state.PosZ);
            BroadcastAvatarAction(_rebroadcastNeighborScratch, state);
        }
    }

    private void HandleEnter(int characterId, PlayerEnterData data)
    {
        // Unconditional force-clear of any stale duel-related state (is-dueling indicator, duel id, side
        // marker, negotiation state) on every zone entry, independent of DuelMaintenanceSystem's own
        // tick-based cleanup -- see DuelRegistry.ForceClearOnZoneEntry's own remarks.
        _duelRegistry.ForceClearOnZoneEntry(characterId);

        var state = new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = data.Session,
            Name = data.Name,
            Tribe = data.Tribe,
            Gender = data.Gender,
            HeadType = data.HeadType,
            FaceType = data.FaceType,
            Level = data.Level,
            MapId = data.MapId,
            PosX = data.PosX,
            PosY = data.PosY,
            PosZ = data.PosZ,
            Heading = data.Heading,
            Life = data.Life,
            MaxLife = data.MaxLife,
            Mana = data.Mana,
            MaxMana = data.MaxMana,
            FlushSequence = data.FlushSequence,
            LastMoveUtc = DateTime.UtcNow,
            // NOT staggered, unlike Zone.SpawnMonster/Zone.SpawnGroundItem -- see this field's own remarks for
            // why: a mass-reconnect burst COULD in principle land several HandleEnter calls in one Zone.Tick
            // (DrainInbox drains up to InboxDrainCapPerTick queued commands per call), but unlike
            // MonsterSpawnScheduler's InitialPopDone -- a tight in-process loop that pops an entire zone's whole
            // spawn-region pool with zero real time passing between iterations, EVERY zone, EVERY boot,
            // guaranteed -- reaching this line at all requires a full TCP handshake + auth/world-entry round
            // trip per player first, which already spreads real arrivals across many real ticks in every
            // observed/plausible case. Verified against ZoneRebroadcastTests' own exact-cadence-boundary
            // assertions (LastAvatarRebroadcastAt must equal _clock precisely at entry) before deciding not to
            // apply the same stagger here -- doing so would have silently broken those tests' "not yet due"
            // assertions for no evidenced benefit.
            LastAvatarRebroadcastAt = _clock,
            // Death-gate state carried through an in-process handoff so a player mid-death who transfers
            // zones doesn't silently come back "alive" with 0 HP on arrival, nor lose/prematurely-clear their
            // territorial-eligibility/anti-abuse tick counters.
            IsDead = data.IsDead,
            TicksSinceDeath = data.TicksSinceDeath,
            ReviveHackFlag = data.ReviveHackFlag,
            CanUseConsumables = data.CanUseConsumables,
            DeathSubCounter = data.DeathSubCounter,
            StatVit = data.StatVit,
            StatStr = data.StatStr,
            StatInt = data.StatInt,
            StatDex = data.StatDex,
            StatPoints = data.StatPoints,
            Title = data.Title,
            Halo = data.Halo,
            RebirthCount = data.RebirthCount,
            Experience = data.Experience,
            ContributionPoints = data.ContributionPoints,
            TeacherPoint = data.TeacherPoint,
            Level2 = data.Level2,
            Exp2 = data.Exp2,
            Zone241Time = data.Zone241Time,
            IsMuted = data.IsMuted,
            GuildId = data.GuildId,
            GuildName = data.GuildName,
            GuildRoleDb = data.GuildRoleDb,
            GuildCallName = data.GuildCallName,
            TribeRole = data.TribeRole,
            // game.Characters.PreviousTribe is a real, independently-persisted column (Migrations/018),
            // EnterWorldService.HandleAsync gates entry on Tribe/PreviousTribe self-consistency
            // (Server/ts25zone/S04_MyWork02.cpp:880-901), and PlayerEnterData.PreviousTribe now carries the
            // real persisted value through both world entry and in-process zone transfer (see that field's
            // own remarks) -- so this is correct for a tribe-3 (Fujin) character too, not just the
            // main-faction (0-2) case where it happens to equal Tribe.
            PreviousTribe = data.PreviousTribe,
            // One of exactly two legitimate write sites for this field -- the other is every accepted Sort-0
            // ("rest"/stand-up) CZ_AVATAR_ACTION_SEND action, see ApplyRestActionProtectionAndHeal. A one-shot
            // ~10s combat grace period starting now, for every arrival. Combat code must never write this
            // field on its own (taking/dealing damage never re-arms it) -- see ZoneEntryAtZoneClock's own
            // remarks.
            ZoneEntryAtZoneClock = _clock,
            // Carried through an in-process handoff so a client mid cash-catalog-notify window doesn't
            // silently lose that state on a map transfer -- see PlayerRuntimeState.KnownCashCatalogVersion's
            // own remarks. Defaults to CashCatalogVersionUnknown for a brand-new login.
            KnownCashCatalogVersion = data.KnownCashCatalogVersion,
            // Zone-241 "LOD" personal-dungeon quota -- see PlayerRuntimeState.DungeonInstanceRoundsRemaining's
            // own remarks (always 0 today, no persisted source wired up yet).
            DungeonInstanceRoundsRemaining = data.DungeonInstanceRoundsRemaining,
            // World-entry hydration (EnterWorldService) or, for an in-process handoff, the live value carried
            // through by ZoneTransfer.CreateEnterData -- see PlayerRuntimeState.HeroRankPoints's own remarks.
            HeroRankPoints = data.HeroRankPoints,
            // Stat/elixir-potion lifetime counters -- see PlayerRuntimeState.EatLifePotion's own remarks.
            EatLifePotion = data.EatLifePotion,
            EatManaPotion = data.EatManaPotion,
            EatStrPotion = data.EatStrPotion,
            EatDexPotion = data.EatDexPotion,
            EatElePotion = data.EatElePotion,
            // mSupportSkillTimeUpRatio's two source fields -- see PlayerRuntimeState.PremiumExpireUtc/
            // BuffX2Time's own remarks. SupportSkillTimeUpRatio itself is recomputed from these just below,
            // not copied from any prior in-memory value (zone-enter/character-load is itself one of the
            // behavior contract's own recompute triggers).
            PremiumExpireUtc = data.PremiumExpireUtc,
            BuffX2Time = data.BuffX2Time,
            // Store/coffre money pool + second-page expiry dates -- see PlayerRuntimeState.Vault.cs's own
            // remarks and PlayerEnterData.StoreMoney's own remarks for why this must travel through both
            // world entry and an in-process zone transfer.
            StoreMoney = data.StoreMoney,
            InventoryDate = data.InventoryDate,
            StoreDate = data.StoreDate
        };

        // Trigger 1 of the mSupportSkillTimeUpRatio behavior contract ("buff-application-stacking-decay"):
        // recompute on every zone-enter/character-load, fresh login and in-process zone transfer alike --
        // never left at the field's own neutral default beyond this point.
        state.RecomputeSupportSkillTimeUpRatio(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Items/Stats are already-computed data handed down through the command -- a plain copy, never a
        // catalog lookup, keeping this tick-thread method's cost independent of WorldDataCache size.
        if (data.Items is { } items)
            state.Inventory.Seed(items);
        if (data.Stats is { } stats)
            state.Stats = stats;
        if (data.Skills is { } skills)
        {
            var builder = ImmutableDictionary.CreateBuilder<byte, LearnedSkill>();
            foreach (var skill in skills)
                builder[skill.SlotIndex] = new LearnedSkill(skill.SkillId, skill.Grade);
            state.LearnedSkills = builder.ToImmutable();
        }

        // game.CharacterHotkeys/CharacterHotkeyDto stores the legacy aHotKey[page][key][0..2] triple verbatim
        // under the column names Sort/Value1/Value2, but cross-checked against real seed data (world.
        // StarterKitHotkeys: a skill-bound key has Sort=<a real SkillId>/Value1=<grade>/Value2=1; an item-bound
        // key has Sort=34 -- world.Items 34 "Rejuvenation Pill (L)", a real "Register in quick slot to use"
        // consumable -- /Value1=999 (MAX_ITEM_DUPLICATION_NUM, a quantity ceiling) /Value2=3), the FIRST raw
        // int is the bound id (skill id or item id), the SECOND is the secondary value (grade or quantity),
        // and the THIRD is the actual HOTKEY_SORT/kind discriminator -- not the first int, despite the DB
        // column's "Sort" name. HotkeySlot's own (Kind, Value1, Value2) shape mirrors that same tagged union
        // positionally, so the correct construction is Kind &lt;- row.Value2, HotkeySlot.Value1 &lt;- row.Sort,
        // HotkeySlot.Value2 &lt;- row.Value1. This mapping is inferred from already-committed repo evidence
        // (not a fresh legacy-behavior-translator contract for op22 specifically) -- flagged for a follow-up
        // citation check.
        if (data.Hotkeys is { } hotkeys)
        {
            var hotkeyBuilder = ImmutableDictionary.CreateBuilder<(byte Page, byte Index), HotkeySlot>();
            foreach (var hotkey in hotkeys)
                hotkeyBuilder[(hotkey.Page, hotkey.KeyIndex)] =
                    new HotkeySlot((HotkeyBindingKind)hotkey.Value2, hotkey.Sort, hotkey.Value1);
            state.Hotkeys = hotkeyBuilder.ToImmutable();
        }

        if (data.FriendsBySlot is { } friends)
            foreach (var (slot, friendId) in friends)
                state.Friends[slot] = friendId;

        // Carries the live BUFF_INFO through an in-process handoff (Server/ts25zone/S04_MyWork02.cpp:2017-2186's
        // broker round trip) -- null only on a fresh login, which has no live snapshot to carry (see
        // PlayerEnterData.Buffs' own remarks). state.Buffs itself is get-only/init-only by design (never a
        // shared backing array across players), so its contents are copied in place rather than the reference
        // being replaced.
        if (data.Buffs is { } buffs)
            buffs.Buff.CopyTo(state.Buffs.Buff, 0);

        state.TeacherCharacterId = data.TeacherCharacterId;
        state.StudentCharacterId = data.StudentCharacterId;

        state.QuestStepPermanent = data.QuestProgress.StepPermanent;
        state.QuestActiveFlag = data.QuestProgress.ActiveFlag;
        state.QuestSort = data.QuestProgress.QSort;
        state.QuestTargetPhase = data.QuestProgress.TargetPhase;
        state.QuestKillCounter = data.QuestProgress.KillCounter;
        state.MissionJoinWar = data.MissionJoinWar;
        state.MissionKillOtherTribe = data.MissionKillOtherTribe;
        state.MissionKillMonster = data.MissionKillMonster;
        state.MissionPlayTime = data.MissionPlayTime;
        state.AutoHuntEnabled = data.AutoHuntEnabled;
        state.AutoHuntConfig = data.AutoHuntConfig;
        state.AutoLifeRatio = data.AutoLifeRatio;
        state.AutoManaRatio = data.AutoManaRatio;
        state.PetGrowth = data.PetGrowth;
        state.PetActivity = data.PetActivity;
        state.PetExpX2Time = data.PetExpX2Time;
        state.LastSeenPetItemId = data.Items is { } petScanItems
            ? PetSlots.ResolveEquippedPetItemId(petScanItems)
            : 0;

        var cell = _grid.CellOf(state.PosX, state.PosZ);
        state.CurrentCell = cell;

        if (!_players.TryAdd(characterId, state))
        {
            // TryAdd only fails when characterId is already tracked here. _players is mutated only from this
            // zone's own tick thread (this class's own header remarks), so the TryGetValue below is
            // guaranteed to observe the very entry TryAdd just collided with -- no other mutator can have
            // raced it away in between.
            _players.TryGetValue(characterId, out var existing);

            if (existing is not null && !ReferenceEquals(existing.Session, state.Session))
            {
                // A DIFFERENT session is still tracked under this characterId: that session's own disconnect
                // teardown (GameConnectionHost.OnAcceptedAsync's finally block, which is what posts its
                // Leave) hasn't run yet. For a same-shard relogin that lag is normally sub-tick, but the
                // cross-process kick path (AccountSessionKickPollHost) that is the only thing that can Abort
                // a still-connected stale session that reconnected to a DIFFERENT shard polls only every
                // GameServerOptions.AccountSessionPollIntervalSeconds (default 20s) -- wide enough for a fresh
                // login to reach here first. This Enter already passed ZoneHandshakeService.ConsumeTicketAsync's
                // own AccountSessions.TransitionToGameAsync check, the cross-process authority that proves this
                // is the newer login for the account, so it must win here rather than silently losing to
                // whichever Enter happened to reach this zone's single-writer tick thread first -- dropping it
                // (the prior behavior) left the new session a permanently untracked zombie that believes it
                // registered, since EnterWorldService.CompleteWorldEntryAsync already sent all three
                // world-entry response payloads before this command was even posted, and zone.Post() itself
                // reports success regardless of what HandleEnter goes on to do with the queued command.
                //
                // Evict the stale session in place instead: same map, same tick thread, no lock needed.
                logger.LogWarning(
                    "Character {CharacterId} entered zone {MapId} while a stale prior session was still tracked -- evicting the old session and adopting the newer one",
                    characterId, MapId);

                _grid.Remove(characterId, existing.CurrentCell);
                _players[characterId] = state;

                // Stops the stale session's own eventual disconnect teardown from posting a Leave for -- or
                // flushing/logout-logging -- the character that was just replaced here under the same
                // characterId: GameConnectionHost.OnAcceptedAsync only does any of that while CurrentZone is
                // still non-null. Exactly as safe as HandleLeave's own CurrentZone reassignment a few lines
                // below (and ZoneClientSession's own remarks on this field): a plain reference write is atomic,
                // and the stale session's own next inbound action, if any slips in before its Abort below takes
                // effect, will find CurrentZone null and simply drop it.
                if (existing.Session is ZoneClientSession staleZoneSession)
                    staleZoneSession.CurrentZone = null;

                if (existing.Session is ClientSession staleClientSession)
                    staleClientSession.Abort(DisconnectReason.Evicted);
            }
            else
            {
                // Same session (or, in principle, existing already null -- unreachable per the remark above,
                // kept only as a defensive fallback): a true duplicate Enter command for a session already
                // tracked here. Fails safe -- ignored, not applied twice.
                logger.LogWarning(
                    "Character {CharacterId} entered zone {MapId} while already tracked -- ignoring duplicate Enter",
                    characterId, MapId);
                return;
            }
        }

        _grid.Add(characterId, cell, state.PosX, state.PosY, state.PosZ);

        // Marked dirty on entry so a handoff's map change reaches SQL even if the player never moves again;
        // on a fresh world entry the sequence already equals the DB baseline, so this flush is a deliberate no-op.
        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Once per player per zone visit, never on the per-tick rebroadcast paths -- cheap.
        logger.LogInformation("Character {CharacterId} entered zone {MapId}", characterId, MapId);

        // Mutual visibility: existing neighbors learn about the new arrival, and vice versa. The self-spawn
        // packet is sent directly by the registration handler before this command is posted. Uses
        // _enterNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray() -- see that field's own
        // remarks.
        _enterNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_enterNeighborScratch, cell, characterId, state.PosX, state.PosY, state.PosZ);

        // Direct send to each neighbor's own session for the new arrival's view of them; the new arrival
        // itself is announced to neighbors via the broadcast below. Swapping these would send the new
        // arrival's own data to itself and leave it blind to everyone already there.
        foreach (var otherId in _enterNeighborScratch)
            if (_players.TryGetValue(otherId, out var other))
                SendAvatarAction(state.Session, other);

        BroadcastAvatarAction(_enterNeighborScratch, state);

        // Immediate monster-visibility exchange (parity-gap fix -- see SendExistingMonstersTo's own remarks
        // for the citation caveat): without this, a monster already alive in this zone stayed invisible to
        // the new arrival until that specific monster's own next independent 5 s keep-alive fired.
        SendExistingMonstersTo(state);

        // Trigger 2 (Server/ts25zone/S04_MyWork02.cpp:1142-1194): unconditional re-arm + personal-boss summon
        // attempt on every entry into a Zone-241-type zone, whether a fresh login or an in-process handoff.
        if (IsZone241TypeZone)
            TryEnterZone241PersonalInstance(characterId);
    }

    private void HandleLeave(int characterId, Zone? handoffTarget, (float X, float Y, float Z)? handoffPosition = null)
    {
        if (!_players.TryRemove(characterId, out var state))
            return;

        _grid.Remove(characterId, state.CurrentCell);

        if (handoffTarget is null)
        {
            // Once per player per zone visit, never on the per-tick rebroadcast paths -- cheap.
            logger.LogInformation("Character {CharacterId} left zone {MapId}", characterId, MapId);

            // Behavior contract "session-state-machine", disconnect/logout cleanup: "any active party is
            // broken ... distinguishing member left from leader left" -- but ONLY on a ready,
            // non-mid-transfer disconnect (Server/ts25zone/S03_MyUser.cpp:338-411,360's own guard). This same
            // handoffTarget-is-null branch also fires for the OLD shard's own connection teardown once a
            // cross-shard zone-transfer client reconnects elsewhere (ZoneMoveService.HandleCrossShardAsync's
            // own remarks: "the ordinary connection-close path already flushes/tidies this player's in-memory
            // state the same way any other disconnect does") -- IsMovingZone is true for exactly that window,
            // so gating on it here reproduces legacy's deliberate skip (breaking the party mid-transfer would
            // race the new zone's own claim on the same character).
            if (!state.IsMovingZone)
                BreakPartyOnDisconnect(characterId, state.Name);

            // Unlike BreakPartyOnDisconnect, deliberately NOT gated on IsMovingZone -- see
            // ClearTradeOnDisconnect's own remarks for why a cross-shard transfer must still release this
            // shard's trade slot.
            ClearTradeOnDisconnect(characterId);

            // Plain leave (disconnect). No despawn/logout opcode exists in the M1 client protocol -- nearby
            // clients simply stop receiving updates for this entity. A documented gap, not an oversight.
            if (characterShardLocations is not null)
                // Fire-and-forget: this method runs on the zone's own tick thread, which must never block on
                // inbound or outbound I/O (see this class's own header remarks). A failed/slow remove just
                // means the cross-shard directory keeps pointing at this shard/map for a bit longer -- every
                // reader already filters by this shard's own heartbeat freshness, so a stuck row is bounded,
                // never permanent, and never blocks this or any other player's tick.
                _ = CleanupShardLocationAsync(characterId);
            return;
        }

        // In-process map transfer: the live state is snapshotted into the Enter command and travels inside
        // it -- this zone has already forgotten the player (TryRemove above), so the character never exists
        // in two zones at once.
        var enterData = ZoneTransfer.CreateEnterData(state, handoffTarget.MapId, handoffPosition);

        if (!handoffTarget.Post(ZoneCommand.Enter(characterId, enterData)))
        {
            // The player is now in no zone, permanently invisible, while their client still believes it is in
            // the world. Fail loudly and drop the connection rather than leave a phantom.
            logger.LogError(
                "Zone {TargetMapId} inbox full: dropped handoff Enter for character {CharacterId} from zone {MapId} -- aborting session",
                handoffTarget.MapId, characterId, MapId);

            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        // Plain reference write: atomic, and a stale read by a racing movement handler is benign.
        if (state.Session is ZoneClientSession zoneSession)
            zoneSession.CurrentZone = handoffTarget;

        logger.LogInformation("Character {CharacterId} handed off from zone {MapId} to zone {TargetMapId}",
            characterId, MapId, handoffTarget.MapId);
    }

    /// <summary>
    ///     <see cref="HandleLeave" />'s disconnect-time party cleanup: breaks any active party
    ///     <paramref name="characterId" /> belongs to (via <see cref="PartyRegistry.LeaveForDisconnect" />) and
    ///     sends the same wire notifications the explicit CZ_PARTY_LEAVE_SEND/CZ_PARTY_BREAK_SEND handlers
    ///     already send (<c>Fenrir.Application.Game.Handlers.Handlers.Social.PartyLeaveHandler</c>/
    ///     <c>PartyDisbandHandler</c>), so a party-changed notification looks identical on the wire whether the
    ///     departure was voluntary or a disconnect. A remaining member can be tracked by any zone this shard
    ///     hosts, not just this one -- resolved via <see cref="_zoneRegistry" />, the same cross-zone lookup
    ///     those handlers use from their own request thread. A no-op if the character had no live party (the
    ///     overwhelmingly common case, so this stays cheap on the hot disconnect path).
    /// </summary>
    private void BreakPartyOnDisconnect(int characterId, string disconnectingName)
    {
        var result = _partyRegistry.LeaveForDisconnect(characterId);

        switch (result.Kind)
        {
            case PartyDisconnectKind.NotInParty:
                return;

            case PartyDisconnectKind.LeaderDisbanded:
            {
                // USE_PARTY_V3 is off in this build, matching PartyDisbandHandler's own remarks -- Sort is
                // always 1 and AvatarName always blank.
                var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
                foreach (var memberId in result.MembersBeforeLeave)
                    if (memberId != characterId)
                        SendToCharacter(memberId, disbandNotice);
                return;
            }

            case PartyDisconnectKind.MemberLeft:
            {
                var leaveNotice = new PartyLeaveResponse { AvatarName = disconnectingName };
                foreach (var memberId in result.MembersBeforeLeave)
                    if (memberId != characterId)
                        SendToCharacter(memberId, leaveNotice);

                var roster = BuildPartyRoster(3, result.RemainingMembers);
                foreach (var memberId in result.RemainingMembers)
                    SendToCharacter(memberId, roster);
                return;
            }

            case PartyDisconnectKind.MemberLeftAndDisbanded:
            {
                var leaveNotice = new PartyLeaveResponse { AvatarName = disconnectingName };
                var disbandNotice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
                foreach (var memberId in result.MembersBeforeLeave)
                {
                    if (memberId == characterId)
                        continue;

                    SendToCharacter(memberId, leaveNotice);
                    SendToCharacter(memberId, disbandNotice);
                }

                return;
            }
        }
    }

    /// <summary>
    ///     <see cref="HandleLeave" />'s disconnect-time trade cleanup: unconditionally clears any trade-process
    ///     state <paramref name="characterId" /> holds (<see cref="TradeRegistry.ClearForDisconnect" />) and, if
    ///     a partner was recorded, notifies them with the same CZ_TRADE_CANCEL_SEND/CZ_TRADE_END_SEND
    ///     notification shape (<c>Fenrir.Application.Game.Handlers.Handlers.Social.TradeCancelHandler</c>/
    ///     <c>TradeEndHandler</c>, via their own services) already send on the voluntary path, so a
    ///     disconnect-triggered teardown looks identical on the wire to a normal cancel/end. Closes the gap that
    ///     previously left
    ///     <see cref="TradeRegistry.IsBusy" /> permanently true for a character who disconnected mid-negotiation
    ///     -- see <see cref="TradeRegistry.ClearForDisconnect" />'s own remarks for why no legacy
    ///     <c>Server/path:line</c> citation backs this specific method (Fenrir-only availability fix).
    ///     <para>
    ///         Unlike <see cref="BreakPartyOnDisconnect" />, deliberately NOT gated on
    ///         <see cref="PlayerRuntimeState.IsMovingZone" />: <see cref="TradeRegistry" /> is one instance per
    ///         <c>GameServer</c> shard process (same same-zone-process-only scope the six trade opcode handlers
    ///         already enforce, per this behavior's own contract), so a character leaving this shard entirely --
    ///         whether by a genuine disconnect or the OLD shard's own connection teardown for a cross-shard
    ///         transfer -- must release its slot in THIS shard's registry either way, or a partner still tracked
    ///         here would stay stuck exactly as before. This is a different architectural scope than
    ///         <see cref="PartyRegistry" />'s own IsMovingZone gate, which exists for party-specific reasons, not
    ///         reproduced here.
    ///     </para>
    ///     A no-op if the character had no live trade-process state at all (the overwhelmingly common case, so
    ///     this stays cheap on the hot disconnect path).
    /// </summary>
    private void ClearTradeOnDisconnect(int characterId)
    {
        var result = _tradeRegistry.ClearForDisconnect(characterId);

        switch (result.Notification)
        {
            case TradeDisconnectNotification.Cancel:
                SendToCharacter(result.PartnerId, new TradeCancelResponse());
                return;

            case TradeDisconnectNotification.End:
                SendToCharacter(result.PartnerId, new TradeEndResponse { Result = 1 });
                return;
        }
    }

    /// <summary>5 name slots, leader first -- same shape as PartyBroadcast.BuildRoster's ZC_PARTY_MAKE_INFO builder.</summary>
    private PartyRosterResponse BuildPartyRoster(int sort, IReadOnlyList<int> memberIds)
    {
        Span<string> names = ["", "", "", "", ""];
        for (var i = 0; i < memberIds.Count && i < 5; i++)
            if (TryFindPlayer(memberIds[i], out var member))
                names[i] = member.Name;

        return new PartyRosterResponse
        {
            Sort = sort,
            AvatarName01 = names[0],
            AvatarName02 = names[1],
            AvatarName03 = names[2],
            AvatarName04 = names[3],
            AvatarName05 = names[4]
        };
    }

    /// <summary>
    ///     Sends to a character tracked either by this zone or, failing that, any other zone on this shard (via
    ///     <see cref="_zoneRegistry" />). Silently drops the send if the character isn't tracked anywhere right
    ///     now (already disconnected too, or mid-handoff) -- same best-effort posture as every other
    ///     party-changed notification loop in this codebase.
    /// </summary>
    private void SendToCharacter<TPacket>(int characterId, in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        if (TryFindPlayer(characterId, out var member))
            member.Session.Send(packet);
    }

    private bool TryFindPlayer(int characterId, [NotNullWhen(true)] out PlayerRuntimeState? state)
    {
        if (_players.TryGetValue(characterId, out state))
            return true;

        return _zoneRegistry is not null && _zoneRegistry.TryGetPlayer(characterId, out state);
    }

    /// <summary>
    ///     <see cref="ZoneCommandKind.MarkZoneTransferPending" />'s handler -- the only site permitted to set
    ///     <see cref="PlayerRuntimeState.IsMovingZone" />, preserving the single-writer invariant this whole
    ///     class relies on (this runs on the zone's own tick thread; the poster,
    ///     <c>ZoneMoveService.HandleCrossShardAsync</c>, runs on a request thread and never touches
    ///     <see cref="PlayerRuntimeState" /> directly). A no-op if the character is no longer tracked here --
    ///     the narrow race where the source zone's own tick already removed this player (disconnect/another
    ///     handoff) between the cross-shard resolution and this command draining, same posture as every other
    ///     <c>_players.TryGetValue</c> miss elsewhere in this class.
    /// </summary>
    private void HandleMarkZoneTransferPending(int characterId)
    {
        if (_players.TryGetValue(characterId, out var state))
            state.IsMovingZone = true;
    }

    /// <summary>
    ///     <see cref="ZoneCommandKind.SetMuted" />'s handler -- the only site permitted to update
    ///     <see cref="PlayerRuntimeState.IsMuted" /> once a character is already tracked here, same
    ///     single-writer posture as <see cref="HandleMarkZoneTransferPending" />. A no-op if the character has
    ///     already disconnected/handed off between <c>MuteRefreshPollHost</c>'s snapshot and this command
    ///     draining -- the next poll simply re-evaluates whichever zone the character is tracked in by then.
    /// </summary>
    private void HandleSetMuted(int characterId, bool muted)
    {
        if (_players.TryGetValue(characterId, out var state))
            state.IsMuted = muted;
    }

    /// <summary>
    ///     Best-effort cross-shard-directory cleanup for a true disconnect (never an in-process handoff --
    ///     <see cref="HandleLeave" />'s handoff branch never calls this, since <c>ShardId</c> doesn't change on
    ///     a same-shard hop). Deliberately awaited nowhere: <see cref="HandleLeave" /> runs on this zone's own
    ///     tick thread and must return immediately regardless of how long the remove takes. Any failure is
    ///     logged and otherwise swallowed -- a stuck row degrades to a bounded staleness window (every reader
    ///     already filters on this shard's own heartbeat freshness), never a thrown exception reaching the
    ///     tick loop.
    /// </summary>
    private async Task CleanupShardLocationAsync(int characterId)
    {
        try
        {
            await characterShardLocations!.RemoveAsync(characterId, options.ShardId, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Zone {MapId}: failed to remove character {CharacterId} from the cross-shard location directory",
                MapId, characterId);
        }
    }

    public void ApplyDeath(int characterId, DeathCause cause = DeathCause.Unknown)
    {
        if (!_players.TryGetValue(characterId, out var state))
        {
            logger.LogWarning(
                "ApplyDeath({CharacterId}) on zone {MapId}: character not tracked here -- ignoring (already disconnected or mid-handoff)",
                characterId, MapId);
            return;
        }

        if (state.IsDead)
            return;

        state.Life = 0;
        state.IsDead = true;
        state.TicksSinceDeath = 0;

        // mProtect_ReviveHack (S07_MyGame04.cpp:1617-1624): armed for every cause except a private duel.
        state.ReviveHackFlag = cause != DeathCause.Duel;
        state.CanUseConsumables = false;
        state.DeathSubCounter = ReviveEligibilityRules.DeathSubCounterBaseline;

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        // Logged unconditionally, regardless of cause -- covers PvP/monster-kill/stun-lock/duel deaths and
        // any GM-forced kill (which simply calls this with the default Unknown cause) alike. Queued rather
        // than awaited inline; see PendingDeathEventLog's own remarks for why.
        QueueDeathEventLog(CharacterDeathEventCode, characterId, (byte)cause, $"Cause={cause};Level={state.Level}");

        if (cause == DeathCause.MonsterKill)
            ApplyDeathExperienceLoss(state);

        // ProcessForDeath unconditionally purges buffs on every death (ProcessForDeleteNormalBuffEffectValue,
        // S07_MyGame04.cpp:1617-1658, the same shared entry point Zone.Stun.cs's team-stun-lock force-kill
        // also routes through) -- so this applies to every DeathCause, not just the stun-lock one. When the
        // victim was stunned, that state is cleared too; the repeated-stun counter itself is deliberately
        // left untouched here (only a cure or natural expiry resets it -- see Zone.Stun.cs's ClearStun).
        // CanUseConsumables is deliberately NOT restored here even though the stun-clear path normally does
        // that: the character is dead, and the death-gate above (mProtect_ReviveHack) owns that flag until
        // GrantReviveEligibility restores it -- letting the stun-clear's own restore win here would let a
        // character who happened to die while stunned immediately use potions despite being dead.
        if (state.IsStunned)
        {
            state.IsStunned = false;
            state.StunDurationSeconds = 0;
            state.StunCountdownAccumulatorTicks = 0;
        }

        ClearAllBuffs(state);

        // Death pose (aAction.aSort = 12) so nearby clients see the character fall immediately. Self is
        // excluded: the combat handler tells the dying player's own client via combat-result packets instead.
        var deathPet = PetActionFieldsOf(state);
        var deathAction = new ActionInfo
        {
            Type = 0,
            Sort = 12,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = deathPet.PetLocation,
            PetTargetLocation = deathPet.PetTargetLocation,
            PetFront = deathPet.PetFront,
            PetSort = deathPet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };

        // Uses _deathNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray() -- see that field's
        // own remarks.
        _deathNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_deathNeighborScratch, state.CurrentCell, characterId, state.PosX, state.PosY,
            state.PosZ);
        BroadcastAvatarAction(_deathNeighborScratch, state, deathAction);
    }

    /// <summary>
    ///     Zeroes every occupied <see cref="PlayerRuntimeState.Buffs" /> slot and broadcasts the change --
    ///     shares <see cref="Simulation.BuffExpirySystem" />'s own per-slot clearing convention (rather than
    ///     duplicating a fresh one) so a bulk clear and a natural per-tick expiry always look identical on the
    ///     wire, and its lazy-allocate-on-first-change posture: no notification, and no scratch-buffer clear,
    ///     unless at least one slot was actually occupied. Uses <paramref name="state" />'s own
    ///     <see cref="PlayerRuntimeState.BuffChangeScratch" /> instead of allocating a fresh <c>int[35]</c> per
    ///     call.
    /// </summary>
    private void ClearAllBuffs(PlayerRuntimeState state)
    {
        var changedSlots = state.BuffChangeScratch;
        var anyChanged = false;

        for (var slot = 0; slot < 35; slot++)
        {
            if (state.Buffs.Buff[slot * 2] == 0 && state.Buffs.Buff[slot * 2 + 1] == 0)
                continue;

            state.Buffs.Buff[slot * 2] = 0;
            state.Buffs.Buff[slot * 2 + 1] = 0;

            if (!anyChanged)
            {
                Array.Clear(changedSlots);
                anyChanged = true;
            }

            changedSlots[slot] = 1;
        }

        if (anyChanged)
            RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    /// <summary>
    ///     The MvP XP-loss branch of <see cref="ApplyDeath" /> (<c>S07_MyGame02.cpp:3445-3489</c>): refuses
    ///     below level 10 or at/above the level cap (loses CP instead, <see cref="ExperienceFormulas.CpLossAtLevelCap" />).
    ///     A level outside the catalog contributes 0 (no loss).
    /// </summary>
    private void ApplyDeathExperienceLoss(PlayerRuntimeState state)
    {
        switch (state.Level)
        {
            case < ExperienceFormulas.MinimumLevelForDeathExperienceLoss:
                return;
            case >= ExperienceFormulas.MaxLimitLevel:
                state.ContributionPoints -= ExperienceFormulas.CpLossAtLevelCap;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId, ContributionPointsLossOutcome,
                    $"Kind=ContributionPoints;Loss={ExperienceFormulas.CpLossAtLevelCap};Level={state.Level}");
                return;
        }

        if (!worldData.LevelsByLevel.TryGetValue(state.Level, out var levelRow))
            return;

        var loss = ExperienceFormulas.ComputeDeathExperienceLoss(state.Experience, levelRow.ExpRangeMin);
        if (loss <= 0)
            return;

        state.Experience -= loss;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        QueueDeathEventLog(DeathExperienceLossEventCode, state.CharacterId, ExperienceLossOutcome,
            $"Kind=Experience;Loss={loss};Level={state.Level}");
    }

    public void GrantReviveEligibility(PlayerRuntimeState state)
    {
        if (!state.IsDead)
            return;

        state.IsDead = false;
        state.Life = 1;
        state.ReviveHackFlag = false;
        state.CanUseConsumables = true;
        state.TicksSinceDeath = 0;
        state.DeathSubCounter = ReviveEligibilityRules.DeathSubCounterBaseline;

        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        SendAvatarAction(state.Session, state);

        // Uses _reviveNeighborScratch instead of AoiGrid.Neighbors(...).Where(...).ToArray() -- see that
        // field's own remarks.
        _reviveNeighborScratch.Clear();
        _grid.NeighborsExcludingSelf(_reviveNeighborScratch, state.CurrentCell, state.CharacterId, state.PosX,
            state.PosY, state.PosZ);
        BroadcastAvatarAction(_reviveNeighborScratch, state);
    }

    private void HandleMove(int characterId, in ActionInfo action, bool isResumeAction = false)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        // Anti-stun-hack veto (W_AVATAR_ACTION_SEND's server-side hardening, S04_MyWork02.cpp:1293 context,
        // veto logic :1329-1338): while stunned, only an action whose own Sort echoes the stun pose itself
        // (11) passes through; every other action-state request is discarded and the character's current
        // stun pose is re-broadcast instead of ever being applied -- not merely client-side animation
        // locking. Runs ahead of every other gate below (including the motion whitelist) since a stunned
        // character must never be disconnected or corrected for an action that was always going to be
        // vetoed anyway.
        if (state.IsStunned && action.Sort != StunActionSort)
        {
            BroadcastStunActionState(state, state.StunDurationSeconds);
            return;
        }

        // CZ_AVATAR_ACTION_SEND (op15) and CZ_UPDATE_AVATAR_ACTION (op16) each run their own, separate
        // inline Sort/Type legality switch in the legacy source -- CheckValidCharacterMotionForSend has
        // exactly one call site in the whole legacy codebase, inside op15's own handler
        // (S04_MyWork02.cpp:1544); op16's own switch (S04_MyWork02.cpp:1815-1878) is materially more
        // permissive for several Sorts (19, 31, 92-95) that op15's table has no row for at all, and never
        // narrower for the two Sorts (90/91) present in both. An (Sort, Type) pair outside whichever table
        // applies to this opcode is a hostile-client signal (a real client only ever sends a legal
        // combination for the opcode it used), not an ordinary business-rule failure -- the whole session is
        // torn down, matching every other malformed-input handler in this class. Runs ahead of
        // MovementRules.IsPlausible below (a Fenrir-only anti-speed-hack addition with no legacy analogue)
        // since this is validating the packet's shape, not its claimed position.
        var motion = default(CharacterMotionEvaluation);
        if (isResumeAction)
        {
            if (!AvatarActionResumeWhitelist.IsLegal(action.Sort, action.Type))
            {
                if (state.Session is ClientSession client)
                    client.Abort(DisconnectReason.Faulted);
                return;
            }
        }
        else if (!CharacterMotionWhitelist.TryEvaluate(action.Sort, action.Type, out motion))
        {
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.Faulted);
            return;
        }

        var now = DateTime.UtcNow;

        if (!movementRules.IsPlausible(state, in action, now, Geometry))
        {
            // Reject: reply with the player's own last-known-good state so the client corrects itself -- no
            // dedicated ForcePositionSync packet exists in the M1 protocol, so this reuses ZC_AVATAR_ACTION_RECV.
            SendAvatarAction(state.Session, state);
            return;
        }

        // Captured BEFORE the unconditional recorded-action overwrite just below, so ApplySkillEffectConfirm
        // (op16 Sort==1) can compare the just-echoed skill number/grade against what this character's own
        // most recent accepted op15 action last recorded -- the legacy op16 handler's own ordering
        // (S04_MyWork02.cpp:1917 overwrites only AFTER the Sort==1 effect-creation check already ran against
        // the PREVIOUS recorded action, S04_MyWork02.cpp:1818). Unused (and meaningless) for an op15 action.
        var previousActionSkillNumber = state.ActionSkillNumber;
        var previousActionSkillGradeNum1 = state.ActionSkillGradeNum1;
        var previousActionSkillGradeNum2 = state.ActionSkillGradeNum2;

        state.PosX = action.Location[0];
        state.PosY = action.Location[1];
        state.PosZ = action.Location[2];
        state.Heading = action.Front;
        state.LastMoveUtc = now;
        state.FlushSequence++;

        // Mirrors the legacy's persistent mDATA.aAction fields for every accepted action, not just movement --
        // sit/meditation and skill casts ride the same unified CZ_AVATAR_ACTION_SEND wire shape.
        state.ActionSort = action.Sort;
        state.ActionSkillNumber = action.SkillNumber;
        state.ActionSkillGradeNum1 = action.SkillGradeNum1;
        state.ActionSkillGradeNum2 = action.SkillGradeNum2;

        // CheckValidCharacterMotionForSend's own side effects: unconditionally replace whatever the previous
        // action left here and start a fresh attack sub-packet budget, even if the previous action's own
        // budget wasn't yet exhausted. Applied alongside ActionSort (not immediately after the whitelist check
        // above) rather than right where the check itself runs, so the two always change together -- an
        // action MovementRules rejects never touches ActionSort either, and letting these fall out of step
        // would let AttackPacketBudget's replay guard (compared against ActionSort) wrongly reject sub-packets
        // for an action that was never actually recorded as current.
        //
        // Exclusive to op15: CheckValidCharacterMotionForSend's attack-budget outputs (skill-category code,
        // enforcement flag, family tag, sub-packet ceiling) have no equivalent computation anywhere in op16's
        // own handler body -- op16 never populates or reads them. An op16-originated action therefore leaves
        // whatever budget op15 last established untouched instead of overwriting it with a lookup that was
        // never legacy-accurate for this opcode.
        if (!isResumeAction)
        {
            state.AttackBudgetEnforced = motion.AttackBudgetEnforced;
            state.AttackFamilyTag = motion.AttackFamilyTag;
            state.AttackSubPacketCeiling = motion.AttackSubPacketCeiling;
            state.AttackSubPacketsUsed = 0;
        }

        var newCell = _grid.CellOf(state.PosX, state.PosZ);
        _grid.Move(characterId, state.CurrentCell, newCell, state.PosX, state.PosY, state.PosZ);
        state.CurrentCell = newCell;

        dirtyTracker.MarkDirty(characterId, DirtyFlags.Position);

        // Op15 (CZ_AVATAR_ACTION_SEND) accepted-action tail: the confirmation is delivered to two independent
        // recipient sets from this same handler invocation, not one shared send reused for both -- the acting
        // client itself, sent directly and unconditionally (ahead of, and independent from, the neighbor
        // broadcast's own per-recipient hiding-state suppression, IsReviveHackBroadcastSuppressed below), plus
        // every neighbor via the broadcast just underneath. Legacy fires both from the same tail
        // (B_AVATAR_ACTION_RECV self-send then SendBroadcast, S04_MyWork02.cpp:1770-1777) -- the self-send is
        // NOT itself gated on hiding state, only the broadcast leg is. Op16 (CZ_UPDATE_AVATAR_ACTION,
        // isResumeAction) has no self-send for an ordinary accepted action in legacy at all
        // (S04_MyWork02.cpp:1789-1922), so this is gated to the op15 path only.
        if (!isResumeAction)
            SendAvatarAction(state.Session, state, action);

        // Op16 (CZ_UPDATE_AVATAR_ACTION, isResumeAction) has no broadcast call anywhere in its legacy body
        // outside the inert, self-only war-answer early-return (S04_MyWork02.cpp:1789-1922) -- so, matching
        // the self-send gate immediately above (and per the broadcast-spread behavior contract's
        // "whitelisted secondary movement-state opcode" edge case), the neighbor broadcast below is gated
        // to the op15 path only. An op16-only state change (fishing-state Sorts 92-95, skill-effect-confirm
        // Sort 1, party-buff Sorts, etc. -- see AvatarActionResumeWhitelist) is still applied to this
        // character's server-side state above; it stays invisible to neighbors until the next periodic
        // 3.5s avatar catch-up broadcast (or never, if no further state change occurs), exactly like
        // legacy's documented server-knows/clients-don't-know staleness gap for this opcode. Uses the
        // reusable _moveNeighborScratch buffer (see its own remarks) instead of
        // AoiGrid.Neighbors(...).Where(...).ToArray() -- this runs on every accepted movement packet, the
        // highest-frequency player-driven event in the server.
        if (!isResumeAction)
        {
            _moveNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_moveNeighborScratch, newCell, characterId, state.PosX, state.PosY,
                state.PosZ);
            BroadcastAvatarAction(_moveNeighborScratch, state, action);
        }

        // Phase A (cast-start mana charge, op15 category-2 Sorts only) vs. Phase B (effect confirm, op16
        // Sort==1 only) -- see the skill-casting-cooldown-mechanics behavior contract. Neither phase is
        // keyed on action.Sort == 30: that action-Sort is the unrelated "stand up from death" request
        // (AvatarActionService's own ReviveHackFlag gate), not a skill-cast trigger in either the
        // action-Sort or skill-number sense.
        if (!isResumeAction)
        {
            if (motion.SkillCategoryCode == SkillCastEffectCategoryCode)
                ApplySkillCastManaCharge(state, action);
            else if (action.Sort == RestActionSort)
                ApplyRestActionProtectionAndHeal(state);
        }
        else if (action.Sort == SkillEffectConfirmActionSort)
        {
            ApplySkillEffectConfirm(state, action, previousActionSkillNumber, previousActionSkillGradeNum1,
                previousActionSkillGradeNum2);
        }
    }

    /// <summary>
    ///     Tail of an accepted CZ_AVATAR_ACTION_SEND (op15) action whose Sort is 0 ("rest"/stand-up) --
    ///     <c>S04_MyWork02.cpp:1779-1785</c>: re-arms the mutual PvP-attack-immunity window (the same
    ///     <see cref="PlayerRuntimeState.ZoneEntryAtZoneClock" /> field <see cref="Combat.CombatResolver" />/
    ///     <see cref="Combat.MonsterCombatResolver" />/<see cref="Combat.StunResolver" /> check on both the
    ///     attacker and defender side of an attack) and unconditionally overwrites current HP to one third of
    ///     max HP plus one. Not conditioned on <see cref="PlayerRuntimeState.IsDead" /> or any other death-gate
    ///     flag -- the legacy handler this reproduces reads none of them before this tail step runs, so this
    ///     fires for every accepted Sort-0 action alike, whether the character is alive or dead, and even if it
    ///     was already at full HP (an unconditional overwrite, never a floor/clamp-if-lower). This is
    ///     deliberately independent of, and never a substitute for, <see cref="GrantReviveEligibility" />'s own
    ///     separate ~5s tick-based death-gate auto-clear (<see cref="Simulation.DeathGateTickSystem" />), which
    ///     never touches HP or this timestamp under any legacy branch observed -- see that method's own remarks.
    /// </summary>
    private void ApplyRestActionProtectionAndHeal(PlayerRuntimeState state)
    {
        state.ZoneEntryAtZoneClock = _clock;

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        state.Life = maxLife / 3 + 1;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = CharacterHpStatSort, Value = state.Life, Value2 = 0 });
    }

    /// <summary>
    ///     Op156 CZ_UPDATE_PET_ACTION_SEND -- copies only the pet sub-fields of <paramref name="action" />,
    ///     matching the legacy handler exactly (no reply, no broadcast; the update rides along on the next
    ///     periodic full-avatar keep-alive rebroadcast instead).
    /// </summary>
    private void HandlePetAction(int characterId, in ActionInfo action)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        state.PetActionSort = action.PetSort;
        state.PetActionFront = action.PetFront;
        state.PetActionLocationX = action.PetLocation[0];
        state.PetActionLocationY = action.PetLocation[1];
        state.PetActionLocationZ = action.PetLocation[2];
        state.PetActionTargetLocationX = action.PetTargetLocation[0];
        state.PetActionTargetLocationY = action.PetTargetLocation[1];
        state.PetActionTargetLocationZ = action.PetTargetLocation[2];
    }

    /// <summary>
    ///     Phase A of a non-attack skill cast (behavior contract "skill-casting-cooldown-mechanics"):
    ///     CZ_AVATAR_ACTION_SEND (op15) accepted with an action-category Sort that
    ///     <see cref="Movement.CharacterMotionWhitelist" /> resolves into <see cref="SkillCastEffectCategoryCode" />
    ///     (the real skill-cast Sorts 32, 33, 38-90 -- NOT action-Sort 30, the unrelated stand-up-from-death
    ///     request). Charges mana unconditionally at cast-start, independent of whether the matching op16
    ///     confirmation (<see cref="ApplySkillEffectConfirm" />) ever arrives. Silent no-op on every failure
    ///     path (unknown/uncastable skill, wrong weapon class, insufficient mana), matching the legacy's own
    ///     bare early-return contract -- no dedicated failure packet exists.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1589-1651 (category-2 mana-cost computation +
    ///     sufficiency check), :1680-1683 (the actual mana deduction, unconditional on any later effect
    ///     resolving).
    ///     <para>
    ///         Damage-dealing skill casts ride this same op15 category-2 Sort family in the legacy source
    ///         too, but this method's own <see cref="SkillCastResolver.TryCast" /> call only ever resolves
    ///         the closed self-buff/heal effect-eligible skill set (<see cref="SkillEffectCatalog" />), so an
    ///         attack-type skill number still silently falls through as UnknownSkill/NotCastable HERE and
    ///         charges nothing via this call site -- by design, not a gap: their mana cost is charged
    ///         instead by <c>Zone.ApplyCombatCommand</c>'s <c>mCase</c> 2 branch (<c>Zone.Combat.cs</c>), keyed
    ///         off the SAME <see cref="SkillValueKind.ManaUse" /> lookup against the SAME per-skill/grade
    ///         table, on the follow-up CZ_PROCESS_ATTACK_SEND attack-resolution packet rather than this
    ///         originating op15 action -- see that method's own remarks for why (Fenrir's action/attack split
    ///         carries no "already paid" flag forward from this earlier packet to that later one) and for the
    ///         still-open Duel/PvM parity gaps that timing choice does not close. Net effect across both call
    ///         sites: every op15 category-2 skill cast that reaches an effect (buff/heal here, or a damage
    ///         attack there) pays its mana cost exactly once, matching the skill-casting-cooldown-mechanics
    ///         behavior contract's "applies uniformly to attack-type and buff-type skill kinds" premise even
    ///         though the two kinds are charged from two different call sites.
    ///     </para>
    /// </remarks>
    private void ApplySkillCastManaCharge(PlayerRuntimeState state, ActionInfo action)
    {
        // One skill-cast attempt per legacy tick. Null (never cast) always passes.
        if (state.LastSkillCastAtZoneClock is { } lastCast && _clock - lastCast < SimulationClock.LegacyTick)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var gradePoints = action.SkillGradeNum1 + action.SkillGradeNum2;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var result = SkillCastResolver.TryCast(skillDef, gradePoints, state.Mana, maxLife, weaponSort,
            state.SupportSkillTimeUpRatio);
        if (!result.Success)
            return;

        state.LastSkillCastAtZoneClock = _clock;
        state.Mana -= result.ManaCost;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        // Effect write (buff slots / targeted heal) is deliberately NOT applied here -- see
        // ApplySkillEffectConfirm, which is what actually writes it, and only once a matching op16
        // confirmation arrives.
    }

    /// <summary>
    ///     Phase B of a non-attack skill cast (behavior contract "skill-casting-cooldown-mechanics"):
    ///     CZ_UPDATE_AVATAR_ACTION (op16) accepted with action-category Sort == <see cref="SkillEffectConfirmActionSort" />.
    ///     Re-validates the just-echoed skill number/grade numbers against whatever this character's own most
    ///     recent accepted op15 action last recorded (<paramref name="previousSkillNumber" />/
    ///     <paramref name="previousGradeNum1" />/<paramref name="previousGradeNum2" />, captured by the
    ///     caller BEFORE this op16 action's own unconditional recorded-action overwrite) and, only on an
    ///     exact match, resolves and applies the buff/heal effect. A mismatch (a confirm for a different
    ///     cast, a stale/duplicate confirm, or no preceding cast at all) is a silent no-op -- no effect, no
    ///     broadcast, no error.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1815-1922 (op16 handler body; Sort==1 branch at :1818
    ///     calls <c>ProcessForCreateEffectValue</c>; the handler's own recorded-action overwrite at :1917 runs
    ///     AFTER this check, against the PREVIOUS recorded action); Server/ts25zone/S07_MyGame04.cpp:1333-1563
    ///     (<c>ProcessForCreateEffectValue</c>: echo-match gate at :1335-1338, effect write at :1509-1563).
    ///     <para>
    ///         Mana is never (re)charged here -- see <see cref="ApplySkillCastManaCharge" /> for the
    ///         cast-start charge, which already ran (or silently didn't) on the preceding op15 action. This
    ///         reuses <see cref="SkillCastResolver.TryCast" /> purely to re-derive the deterministic
    ///         buff-write/heal-amount for the matched skill/grade/weapon; <c>int.MaxValue</c> is passed for
    ///         the mana parameter so a balance already reduced by the op15 charge can never spuriously fail
    ///         this second, mana-independent resolution.
    ///     </para>
    ///     <para>
    ///         Formation skills 76/77/79/81 additionally require the caster's own party to have exactly
    ///         <see cref="Social.Party.PartyRegistry.MaxMembers" /> ready members present in this same zone
    ///         (<see cref="HasFullPartyPresent" />, checked via <see cref="SkillCastResolver.Result.RequiresFullParty" />)
    ///         -- short of a full 5 (including solo), no buff is written even to the caster, matching
    ///         <c>AVATAR_OBJECT::ProcessForCreateEffectValue</c>'s own all-or-nothing gate
    ///         (S07_MyGame04.cpp:1348-1374). Not modeled here: the two-step <c>mParty_Buff_Act</c> CAST/DONE
    ///         cast-lifecycle marker the legacy also requires (S04_MyWork02.cpp:1684-1699) -- this method's own
    ///         skill/grade staleness check above already enforces the same "a cast must have started before
    ///         this confirm applies" ordering, so it stands in as the structural equivalent; and the
    ///         independent grade-bound recheck the legacy performs for every other skill number
    ///         (S07_MyGame04.cpp:1376-1381) -- neither established by the behavior contract this satisfies
    ///         (flagged there as further-tracing items, not guessed at here).
    ///     </para>
    /// </remarks>
    private void ApplySkillEffectConfirm(PlayerRuntimeState state, ActionInfo action, int previousSkillNumber,
        int previousGradeNum1, int previousGradeNum2)
    {
        if (action.SkillNumber != previousSkillNumber ||
            action.SkillGradeNum1 != previousGradeNum1 ||
            action.SkillGradeNum2 != previousGradeNum2)
            return;

        worldData.SkillsById.TryGetValue(action.SkillNumber, out var skillDef);
        var gradePoints = action.SkillGradeNum1 + action.SkillGradeNum2;
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId;
        var weaponSort = weaponItemId is { } id && worldData.ItemsById.TryGetValue(id, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        // int.MaxValue for casterMana: mana was already charged (or the cast already silently failed) in
        // ApplySkillCastManaCharge -- this second resolution must never fail on a now-lower live mana
        // balance, since Phase B's own preconditions never re-check mana (see remarks above).
        var result = SkillCastResolver.TryCast(skillDef, gradePoints, int.MaxValue, maxLife, weaponSort,
            state.SupportSkillTimeUpRatio);
        if (!result.Success)
            return;

        // Formation skills 76/77/79/81 only (SkillEffectCatalog.RequiresFullParty) -- see this method's own
        // remarks and the "Formation Party-Buff Exact-Five-Member Gate" behavior contract. Every other skill
        // number leaves RequiresFullParty false and this check is a no-op for it.
        if (result.RequiresFullParty && !HasFullPartyPresent(state.CharacterId))
            return;

        switch (result.Kind)
        {
            case SkillEffectKind.SelfBuff:
                ApplyBuffWrites(state, result.BuffWrites);
                break;
            case SkillEffectKind.HealLife:
                ApplyTargetedHeal(action, true, result.HealAmount);
                break;
            case SkillEffectKind.HealMana:
                ApplyTargetedHeal(action, false, result.HealAmount);
                break;
        }
    }

    /// <summary>
    ///     Behavior contract "Formation Party-Buff Exact-Five-Member Gate": true only when
    ///     <paramref name="characterId" /> belongs to a party whose full <see cref="Social.Party.PartyRegistry.MaxMembers" />
    ///     (5) members are all currently present (spawned/registered) in this same zone -- mirrors
    ///     <c>AVATAR_OBJECT::ProcessForCreateEffectValue</c>'s zone-local, name-matched ready-avatar count
    ///     (S07_MyGame04.cpp:1356-1374), adapted to Fenrir's CharacterId-keyed <see cref="Social.Party.PartyRegistry" />
    ///     instead of the legacy's party-name string match (no per-avatar party name is modeled, matching the
    ///     same substitution <c>Zone.Stun.cs</c>'s <c>ApplyTeamStunSubMechanic</c> already makes for the
    ///     analogous team-stun exact-5 gate). A party can never exceed 5 total members in
    ///     <see cref="Social.Party.PartyRegistry" />, so this also implicitly requires the party to be full,
    ///     not merely non-empty.
    /// </summary>
    private bool HasFullPartyPresent(int characterId)
    {
        var members = _partyRegistry.GetMembers(characterId);
        if (members.Count != PartyRegistry.MaxMembers)
            return false;

        var presentCount = 0;
        foreach (var memberId in members)
            if (_players.ContainsKey(memberId))
                presentCount++;

        return presentCount == PartyRegistry.MaxMembers;
    }

    /// <summary>
    ///     Applies a resolved buff-write set to <paramref name="state" />'s live buff slots and notifies
    ///     (stat recompute + AOI broadcast) -- shared by the manual self-buff cast confirm above
    ///     (<see cref="SkillEffectKind.SelfBuff" />) and <see cref="Simulation.AutoHuntTickSystem" />'s
    ///     bot-buff loop. These two call sites used to be independent, byte-for-byte-identical method bodies
    ///     (one private here, one a "small, deliberate duplicate" in <see cref="Simulation.AutoHuntTickSystem" />)
    ///     -- unified here per the buff-slot-write-and-notify-pattern behavior contract. Uses
    ///     <paramref name="state" />'s own <see cref="PlayerRuntimeState.BuffChangeScratch" /> instead of
    ///     allocating a fresh <c>int[35]</c> on every call. A write whose slot falls outside 0-34 is silently
    ///     skipped -- every other write in the same call still applies normally (preserved exactly from both
    ///     original call sites); the mask is still sent (even if every write happened to be out of range) as
    ///     long as <paramref name="writes" /> was non-empty, matching the original eager-allocate behavior of
    ///     both original methods.
    /// </summary>
    internal void ApplyBuffWrites(PlayerRuntimeState state, ImmutableArray<SkillCastResolver.BuffWrite> writes)
    {
        if (writes.IsEmpty)
            return;

        var changedSlots = state.BuffChangeScratch;
        Array.Clear(changedSlots);
        foreach (var write in writes)
        {
            if (write.Slot is < 0 or >= 35) continue;
            state.Buffs.Buff[write.Slot * 2] = write.Value;
            state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;
            changedSlots[write.Slot] = 1;
        }

        RecomputeStatsAndBroadcastBuffs(state, changedSlots);
    }

    /// <summary>
    ///     Targeted heal (skills 106-111): resolves the target against this same zone, clamps the flat heal
    ///     amount to remaining capacity (<c>S07_MyGame03.cpp:9500-9510/9563-9573</c>). A target at full HP/MP,
    ///     or not found/dead, silently receives nothing.
    /// </summary>
    private void ApplyTargetedHeal(ActionInfo action, bool isLife, int rawAmount)
    {
        if (rawAmount < 1)
            return;
        if (!_players.TryGetValue(action.TargetObjectIndex, out var target))
            return;
        if (target.UniqueNumber != unchecked((uint)action.TargetObjectUniqueNumber))
            return;
        if (target.IsDead)
            return;

        if (isLife)
        {
            var max = target.Stats?.MaxLife ?? target.MaxLife;
            var amount = Math.Min(rawAmount, max - target.Life);
            if (amount < 1) return;
            target.Life += amount;
        }
        else
        {
            var max = target.Stats?.MaxMana ?? target.MaxMana;
            var amount = Math.Min(rawAmount, max - target.Mana);
            if (amount < 1) return;
            target.Mana += amount;
        }

        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    /// <summary>
    ///     Recomputes <see cref="PlayerRuntimeState.Stats" /> from the live Equipment container + current
    ///     <see cref="PlayerRuntimeState.Buffs" /> snapshot, and broadcasts the updated buff view to this
    ///     player and their AOI neighbors. Unlike the legacy's live-read wrappers,
    ///     <see cref="PlayerRuntimeState.Stats" /> is an explicit cache that must be refreshed on every buff change.
    /// </summary>
    public void RecomputeStatsAndBroadcastBuffs(PlayerRuntimeState state, int[] changedSlots)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        state.Stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution);

        var response = new AvatarEffectStateResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            EffectValue = state.Buffs.Buff,
            EffectValueState = changedSlots
        };

        // Rent-once/write-once/copy-N-times -- same idiom as BroadcastAvatarAction below: the frame is
        // encoded exactly once and copied into each recipient's own transport, with a per-recipient failure
        // isolated (logged, skipped) rather than aborting the rest of this broadcast.
        var total = FrameWriter.FrameSizeOf<AvatarEffectStateResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            SendBuffStateFrame(state.CharacterId, span);
            foreach (var neighborId in _grid.Neighbors(state.CurrentCell, state.PosX, state.PosY, state.PosZ))
            {
                if (neighborId == state.CharacterId) continue;
                SendBuffStateFrame(neighborId, span);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void SendBuffStateFrame(int recipientId, ReadOnlySpan<byte> frame)
    {
        try
        {
            if (_players.TryGetValue(recipientId, out var recipient) &&
                recipient.Session is ClientSession clientSession)
                clientSession.SendRaw(frame);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zone {MapId} buff-state broadcast to character {RecipientId} failed", MapId,
                recipientId);
        }
    }

    private void SendAvatarAction(IPacketSession session, PlayerRuntimeState state)
    {
        session.Send(BuildAvatarActionRecv(state));
    }

    /// <summary>
    ///     Overload that echoes a SPECIFIC just-accepted <see cref="ActionInfo" /> back to <paramref name="session" />
    ///     verbatim -- e.g. <see cref="HandleMove" />'s op15 accepted-action self-send, which must reflect the
    ///     actual action just written into <paramref name="state" /> (matching legacy's
    ///     <c>B_AVATAR_ACTION_RECV(...,&amp;tUserInfo-&gt;mDATA,1)</c>, which reflects the just-written
    ///     <c>mDATA.aAction = r-&gt;tAction</c>) rather than the parameterless overload above's synthesized
    ///     resting Sort-0 pose.
    /// </summary>
    private void SendAvatarAction(IPacketSession session, PlayerRuntimeState state, ActionInfo action)
    {
        session.Send(BuildAvatarActionRecv(state, action));
    }

    /// <summary>
    ///     Serialize-once broadcast: the frame is written to a rented buffer once and copied into each recipient's own
    ///     pipe.
    /// </summary>
    private void BroadcastAvatarAction(IReadOnlyList<int> recipientCharacterIds, PlayerRuntimeState state,
        ActionInfo? action = null)
    {
        if (recipientCharacterIds.Count == 0)
            return;

        var packet = action is null ? BuildAvatarActionRecv(state) : BuildAvatarActionRecv(state, action.Value);
        var total = FrameWriter.FrameSizeOf<AvatarActionResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipientCharacterIds)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        !IsReviveHackBroadcastSuppressed(recipient))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    // A recipient whose transport is already gone must not abort the broadcast for every other one.
                    logger.LogError(ex, "Zone {MapId} broadcast to character {RecipientId} failed", MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    ///     Shared recipient filter (side effect 3, S07_MyGame03.cpp:809-833/857-880 Broadcast11, :893-935/954-978
    ///     Broadcast22): a recipient still flagged from an unresolved death, 30+ ticks in, silently receives
    ///     nothing from any of this zone's AOI-radius broadcasts -- except on
    ///     <see cref="ReviveEligibilityZones.BroadcastSuppressionExemptZoneId" />, where the suppression itself is
    ///     disabled (S07_MyGame01.cpp:508-522, ZONE124 macro unconditionally defined at H07_MyGame.h:19).
    ///     Applied identically by <see cref="BroadcastAvatarAction" />, <see cref="BroadcastMonsterAction" />,
    ///     <see cref="BroadcastGroundItemAction" />, and <see cref="BroadcastProxyShopState" /> -- previously this
    ///     method (as <c>IsAvatarBroadcastSuppressed</c>) was called only from the first of those, on the mistaken
    ///     premise that Broadcast11/Broadcast22 backed avatar broadcasts alone. Re-verified this session: every
    ///     legacy call site for all four families routes through the same two functions, which is where this
    ///     exact check lives, not a per-family opt-in --
    ///     <c>AVATAR_OBJECT::SendBroadcastForLogic</c> (S07_MyGame04.cpp:2762-2765, Broadcast22),
    ///     <c>ITEM_OBJECT::SendBroadcast</c>/<c>SendBroadcastForLogic</c> (S07_MyGame06.cpp:99-107, periodic
    ///     expiry call site :32-54, both Broadcast11),
    ///     <c>MONSTER_OBJECT::Send1</c>/<c>Send2</c>/<c>Send3</c>/<c>SendSpecialNumber</c>
    ///     (S07_MyGame05.cpp:3967-4002, Broadcast11, scale 1/2/3), and the proxy-shop periodic keep-alive plus its
    ///     own explicit close path (S07_MyGame01.cpp:2606; S07_MyGame09.cpp:208-209, both Broadcast11). Note:
    ///     <see cref="BroadcastAttackResult" /> and the avatar buff-state send (<c>AVATAR_OBJECT::SendBroadcast</c>,
    ///     itself also observed this session to route through Broadcast11 at S07_MyGame04.cpp:2752-2755, backing
    ///     both the buff-value broadcast at S07_MyGame04.cpp:591-592 and the avatar-vs-avatar damage broadcast at
    ///     S07_MyGame02.cpp:1370-1371) were NOT brought under this filter here -- that is a separate, not yet
    ///     audit-confirmed gap outside this fix's scope; it needs its own legacy-behavior-translator contract
    ///     before either broadcast path is changed.
    /// </summary>
    private bool IsReviveHackBroadcastSuppressed(PlayerRuntimeState recipient)
    {
        if (MapId == ReviveEligibilityZones.BroadcastSuppressionExemptZoneId)
            return false;

        return recipient.ReviveHackFlag &&
               recipient.TicksSinceDeath >= SimulationClock.DeathBroadcastSuppressionLegacyTicks;
    }

    private AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state)
    {
        var pet = PetActionFieldsOf(state);

        return BuildAvatarActionRecv(state, new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [state.PosX, state.PosY, state.PosZ],
            TargetLocation = [state.PosX, state.PosY, state.PosZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = pet.PetLocation,
            PetTargetLocation = pet.PetTargetLocation,
            PetFront = pet.PetFront,
            PetSort = pet.PetSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        });
    }

    /// <summary>
    ///     Companion-pet follow sub-fields (op156 CZ_UPDATE_PET_ACTION_SEND, see <see cref="HandlePetAction" />
    ///     and <see cref="PlayerRuntimeState.PetActionSort" />'s own remarks) read back into a fresh
    ///     <see cref="ActionInfo" /> for <paramref name="state" /> -- shared by every avatar-action broadcast
    ///     builder in this class so the pet's last-reported pose/position finally rides along on the next
    ///     independently-triggered avatar-action broadcast, instead of the empty/zero placeholder every one of
    ///     these call sites used before this was wired up (companion-pet-follow-rebroadcast finding).
    /// </summary>
    private static (float[] PetLocation, float[] PetTargetLocation, float PetFront, int PetSort) PetActionFieldsOf(
        PlayerRuntimeState state)
    {
        return (
            [state.PetActionLocationX, state.PetActionLocationY, state.PetActionLocationZ],
            [state.PetActionTargetLocationX, state.PetActionTargetLocationY, state.PetActionTargetLocationZ],
            state.PetActionFront,
            state.PetActionSort);
    }

    /// <summary>
    ///     Internal (not private): reused by <c>ZoneMoveHandler</c> to build the self-spawn packet for a
    ///     zone-transfer, with an explicit <paramref name="action" /> carrying the just-resolved arrival
    ///     position rather than <paramref name="state" />'s own (still the source zone's).
    /// </summary>
    /// <remarks>
    ///     Broadcast-spread-gap fix: this is the single builder shared by every avatar-action broadcast in the
    ///     zone (per-move neighbor fan-out, the 3.5s periodic catch-up, zone-enter mutual visibility,
    ///     death/revive, duel end, and the op139 costume full-record rebroadcast), so
    ///     <see cref="ObjectForAvatar.EffectValueForView" />/<see cref="ObjectForAvatar.DuelState" />/
    ///     <see cref="ObjectForAvatar.CostumeNumber" />/<see cref="ObjectForAvatar.CostumeState" /> must reflect
    ///     <paramref name="state" />'s live values here, not a permanent zero placeholder -- otherwise an
    ///     observer who enters AOI range AFTER that state was set (rather than at the exact tick it changed)
    ///     never learns it through any broadcast path, contradicting the periodic mechanism's documented
    ///     "guaranteed convergence within one throttle window" property (see the broadcast-spread behavior
    ///     contract's Edge cases). Instance (not static) specifically so it can resolve <paramref name="state" />'s
    ///     own active duel via <see cref="_duelRegistry" />.
    /// </remarks>
    public AvatarActionResponse BuildAvatarActionRecv(PlayerRuntimeState state, ActionInfo action)
    {
        return new AvatarActionResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Data = new ObjectForAvatar
            {
                VisibleState = 0,
                SpecialState = 0,
                KillOtherTribe = 0,
                GoodFellow = 0,
                GuildName = "",
                GuildRole = 0,
                CallName = "",
                GuildMarkEffect = 0,
                Name = state.Name,
                Tribe = state.Tribe,
                PreviousTribe = state.PreviousTribe,
                Gender = state.Gender,
                HeadType = state.HeadType,
                FaceType = state.FaceType,
                Level1 = state.Level,
                Level2 = state.Level2,
                // Reflects the live Equipment container instead of a hardcoded blank.
                EquipForView =
                    EquipmentViewCodec.BuildEquipForView(state.Inventory.GetContainer(ContainerMatrix.Equipment)),
                AnimalNumber = 0,
                Title = state.Title,
                Halo = state.Halo,
                RebirthNum = state.RebirthCount,
                BattleTeam = 0,
                Action = action,
                MaxLifeValue = state.MaxLife,
                LifeValue = state.Life,
                MaxManaValue = state.MaxMana,
                ManaValue = state.Mana,
                EffectValueForView = BuildEffectValueForView(state),
                PartyName = "",
                DuelState = ResolveDuelStateForView(state.CharacterId),
                PShopState = 0,
                PShopName = "",
                CostumeNumber = state.CostumeNumber,
                BufEffectTimeState = 0,
                BufSort = 0,
                AutoState = 0,
                FishingState = 0,
                FishingStep = 0,
                FishingPoint = new float[3],
                RankPoint = 0,
                TargetState = 0,
                AnimalAbsorbState = 0,
                PetValid = 0,
                Unk1 = 0,
                PetLocation = new float[3],
                PetFrame = 0,
                Unk624 = 0,
                Unk625 = 0,
                UniqueSkillNumber = 0,
                UniqueSkillBuffTime = 0,
                CostumeState = state.CostumeState,
                StellarCoreNumber = 0
            },
            CheckChangeActionState = 0
        };
    }

    /// <summary>
    ///     The value half of each of <paramref name="state" />'s 35 buff slots (<see cref="BuffInfo.Buff" />'s
    ///     own flattened [value, duration] pairing per slot -- see <see cref="RecomputeStatsAndBroadcastBuffs" />
    ///     and <see cref="ClearAllBuffs" />, which index it identically), reshaped into the 35-element view array
    ///     <see cref="ObjectForAvatar.EffectValueForView" /> expects. This is the full-record avatar broadcast's
    ///     own copy of the buff icons every observer's client renders -- independent of whether that observer
    ///     also happens to receive the narrower event-driven <see cref="AvatarEffectStateResponse" /> at the
    ///     moment a buff actually changes.
    /// </summary>
    private static int[] BuildEffectValueForView(PlayerRuntimeState state)
    {
        var view = new int[35];
        for (var slot = 0; slot < 35; slot++)
            view[slot] = state.Buffs.Buff[slot * 2];

        return view;
    }

    /// <summary>
    ///     <c>aDuelState[3]</c> for <paramref name="characterId" />: [0] is 1 when actively dueling else 0,
    ///     [1] is the shared <see cref="ActiveDuel.UniqueNumber" /> of the active duel, and [2] is this
    ///     character's side -- 1 for <see cref="ActiveDuel.PlayerA" /> (the CZ_DUEL_START_SEND caller) or 2 for
    ///     <see cref="ActiveDuel.PlayerB" /> (the opponent). All-zero when <paramref name="characterId" /> has
    ///     no active duel.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8400-8451 (BEGIN_CZ(DUEL_START_SEND): sets
    ///     <c>aDuelState[0]=1</c>, <c>aDuelState[1]=</c>the newly-minted shared duel id, and
    ///     <c>aDuelState[2]=1</c> for the calling <c>tUserInfo</c> vs. <c>=2</c> for <c>tOtherUserInfo</c>) ;
    ///     Server/Header/Protocol/STRUCT.h:766 (<c>aDuelState[3]</c> field itself, part of the avatar full
    ///     record embedded in every avatar-action broadcast, STRUCT.h:745-793).
    /// </remarks>
    private int[] ResolveDuelStateForView(int characterId)
    {
        return _duelRegistry.TryGetActiveDuel(characterId, out var duel) && duel is not null
            ? [1, duel.UniqueNumber, characterId == duel.PlayerA ? 1 : 2]
            : new int[3];
    }

    /// <summary>
    ///     A single pending <c>game.EventLog</c> row for a death-related event (<see cref="ApplyDeath" /> /
    ///     <see cref="ApplyDeathExperienceLoss" />) -- queued rather than awaited inline because
    ///     <see cref="Tick" /> must stay fully synchronous and never block on SQL I/O, same posture as
    ///     <c>Zone.Monsters.cs</c>'s own pending-money-grant queue. Drained from any thread by
    ///     <c>Fenrir.Application.Game.Hosting.World.DeathEventLogFlushHost</c>, which resolves the actual
    ///     <see cref="IEventLogRepository.LogAsync" /> call -- always under <see cref="EventLogCategory.Death" />,
    ///     the single-row high-stakes path (deaths are explicitly enumerated there), never the high-frequency
    ///     <c>BatchLogAsync</c>/<c>EventLogQueue</c> path reserved for low-stakes telemetry.
    /// </summary>
    public readonly record struct PendingDeathEventLog(
        short EventCode,
        int ActorCharacterId,
        short? ShardId,
        byte? Outcome,
        string? Payload);
}
