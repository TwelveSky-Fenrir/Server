using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     The auto-hunt bot's per-tick server-driven upkeep, run once per <see cref="Simulate" /> for every
///     <see cref="PlayerRuntimeState.AutoHuntEnabled" /> player, in the legacy order:
///     <list type="number">
///         <item>
///             the two-tier paid auto-hunt-time budget decrement (<c>S07_MyGame04.cpp:787-824</c>, via
///             <see cref="AutoHuntBudgetPolicy" />) -- runs first, gated only on the auto-hunt flag, and on
///             exhaustion relocates the character to the auto zone (<c>ReturnToHomeZoneResponse</c>) and abandons
///             the rest of this tick's upkeep;
///         </item>
///         <item>
///             the bot buff-cast (<c>AVATAR_OBJECT::BotBuff</c>, <c>S07_MyGame04.cpp:2271-2497</c>): auto-casts
///             the first eligible configured buff skill from <see cref="PlayerRuntimeState.AutoHuntConfig" />'s
///             BuffStore, at most one cast per legacy tick -- matching legacy's own single <c>break</c> the moment
///             a slot clears its "already buffed" gate -- including the zone-126-type-server no-mana escalation
///             (<c>:2469-2485</c>);
///         </item>
///         <item>
///             the hotkey resupply pass (<c>BotHotKey</c>, <c>S07_MyGame04.cpp:2499-2559</c>): implemented as the
///             pure, fully-tested <see cref="Hotkeys.BotHotKeyResupplyPolicy" /> but deliberately NOT applied live
///             yet -- see that policy's own remarks and this class's own below.
///         </item>
///     </list>
/// </summary>
/// <remarks>
///     <para>
///         <b>Regen / no-server-side-auto-attack fidelity (zero-code note, do not "fix"):</b> the auto-hunt tick
///         upkeep path deliberately performs NO server-side auto-attack -- legacy's own bot tick invokes only the
///         buff-cast and hotkey-resupply steps (<c>S07_MyGame04.cpp:345-346</c>), never an attack step; the
///         actual attacking is client-driven, and reproducing a server-side auto-attacker would be a divergence,
///         not a fix. HP/MP regeneration likewise stays sit-only (owned by <see cref="MeditationRegenSystem" />,
///         gated on <c>ActionSort == 31</c>) and is never added to this path. Both are legacy-faithful and
///         intentional. NOTE: the "regeneration is sit-only" half was flagged in the originating B11 behavior
///         contract as uncited (not observed in any citation opened for it) and carried forward for a
///         cpp-zone-gameplay-analyst re-check -- documented here, not relied upon for any new behavior.
///     </para>
///     Not reproduced (or reproduced only as an unwired policy), and why:
///     <list type="bullet">
///         <item>
///             <c>BotHotKey</c>/<c>BotHotKeySend</c> (the hotkey-refill half of the same legacy bot loop,
///             Server/ts25zone/S07_MyGame04.cpp:341-350 call site, :2499-2559 bodies,
///             Server/ts25zone/S07_MyGame03.cpp:9165-9219 <c>ProcessHotkeyForAutoHunt</c>) -- the resupply
///             DECISION is now implemented and fully unit-tested as the pure
///             <see cref="Hotkeys.BotHotKeyResupplyPolicy" />, but its result is deliberately NOT applied live on
///             this tick path. Re-checked rather than assumed stale: two of the three prerequisite gaps previously
///             cited here have since closed elsewhere in the codebase -- <see cref="PlayerRuntimeState.Hotkeys" />
///             now exists as an in-memory model, and pet-equipped (Equipment container slot 8) / active-mount
///             (<c>PlayerRuntimeState.AnimalNumber</c>) state plus the two auto-hunt consumption flags
///             (<c>AutoHunt.AnimalPreyCmd</c>/<c>AnimalFoodCmd</c>) this behavior needs are all readable today.
///             Two concrete blockers remain (either alone makes applying a move here unsafe -- an unnotified,
///             unpersisted inventory debit would desync the client and risk item loss, violating the atomic-move
///             invariant):
///             <list type="bullet">
///                 <item>
///                     <see cref="PlayerRuntimeState.Hotkeys" /> is never actually populated -- neither world
///                     entry (<c>Zone.PlayerLifecycle</c>) nor an in-process zone transfer
///                     (<see cref="World.ZoneTransfer" />) sets it from <c>game.CharacterHotkeys</c> despite
///                     that type's own remarks claiming otherwise, and no handler reads or writes it either:
///                     <see cref="Handlers.UseHotkeyItemHandler" />'s op22 flow addresses an inventory page/slot
///                     directly (the client resolves the hotkey binding locally), never this dictionary, and its
///                     own remarks still say it "does not yet apply the item's use effect or decrement
///                     quantity." A sibling <see cref="Consumables.HotkeyItemConsumptionResolver" /> domain
///                     resolver models the consume side against a <c>HotkeySlot</c> parameter, but nothing calls
///                     it yet -- so no hotkey slot can be populated OR emptied through any reachable code path
///                     today, and there is nothing live for a per-tick bucket scan to read.
///                 </item>
///                 <item>
///                     The one prerequisite wire packet for this exact behavior already exists --
///                     <c>AutoHuntHotkeyRebindResponse</c> (op120, legacy <c>ZC_SET_HOTKEY_INVENTORY_RECV</c>,
///                     under <c>Network.Serialization.Packets.Zone</c>) -- but its three generically-named
///                     <c>Value0</c>/<c>Value1</c>/<c>Value2</c> fields have no cited mapping onto the legacy
///                     <c>HOTKEY_SORT</c> tagged union's raw 3-int storage. <see cref="Hotkeys.HotkeyBindingKind" />'s
///                     own remarks already flag this exact gap: its numeric values are "a Fenrir-internal
///                     convention, not a transcription of the legacy enum's own numbering," and "the behavior
///                     contract this type implements deliberately does not state the legacy wire-level
///                     binding-kind codes, so none are guessed here." The same rule blocks writing
///                     <c>Value0</c> for this response without inventing a legacy wire code from nothing.
///                     Needs a follow-up cpp-protocol-header-analyst/legacy-behavior-translator pass on
///                     <c>ZONE.h:1129-1138</c> (<c>ZC_SET_HOTKEY_INVENTORY_RECV</c>) together with
///                     <c>S04_MyWork05.cpp:122-159</c> (<c>HOTKEY_SORT</c>) before this is safe to wire up.
///                 </item>
///             </list>
///         </item>
///         <item>
///             The mid-animation <c>aAction.aSort</c> (41/60-68/75) gate -- Fenrir has no multi-tick
///             cast-animation state machine (a buff applies instantly, same as every manual op30 cast), so
///             there is nothing equivalent to gate on. <c>mCheckStun</c> itself IS now modeled and gated on
///             below (<see cref="World.PlayerRuntimeState.IsStunned" />) -- this bullet no longer covers it.
///         </item>
///         <item>
///             <c>MyFactor::GetBonusSkillValue</c>'s equipment-derived skill-grade bonus -- already a
///             documented gap on the manual op30 cast path too (see <see cref="AutoBuffActivationResolver" />'s
///             own remarks); this reuses the exact same <see cref="SkillCastResolver" /> and inherits the same
///             simplification.
///         </item>
///         <item>
///             The zone-type-flag/no-mana escalation branch (<c>mCheckZone126TypeServer</c>'s "kick after 1000
///             failed ticks", <c>S07_MyGame04.cpp:2469-2485</c>) -- implemented (see <see cref="EscalateNoMana" />
///             / <see cref="PlayerRuntimeState.NoManaCount" />), distinct from the RvR/event zone-server-type gate
///             (see <see cref="IsSuppressedByZoneServerType" />): a separate zone-126-only "test server"
///             escalation. On a zone-126-type server a selected buff whose mana cost exceeds current mana
///             increments the counter (relocate at exactly 1000, disconnect beyond); a proceeding cast resets it.
///             Outside a zone-126-type server the counter never grows -- an ordinary insufficient-mana slot is
///             simply skipped here, deliberately NOT reproducing legacy's "cast anyway into negative mana"
///             fall-through (a defect-shaped quirk this project's harden-never-reproduce policy declines to copy,
///             and one that would contradict the existing insufficient-mana-skips-cast test coverage). The
///             zone-126-type flag itself now resolves through the canonical, config-driven
///             <see cref="World.Zone.IsZone126TypeZone" /> (<see cref="GameServerOptions.Zone126TypeMapIds" />,
///             verified server numbers 210-218 at <c>S07_MyGame01.cpp:895-913</c>) rather than a hardcoded
///             <c>MapId == 126</c> guess -- corrected per the B11-zone126-escalation contract's own citation,
///             which resolves what was previously flagged here as an unverified "inference by analogy."
///         </item>
///         <item>
///             <c>mCheckZone053TypeServer</c> ("Stone War"), the fourth term of that same gate -- deliberately
///             never checked, because its own legacy initialization is compiled only under a build macro never
///             defined in any shipped configuration (Server/ts25zone/S07_MyGame01.cpp:731-791;
///             ServerDocs/12_ts25zone/09_MyGame01_PartieB.md:113-119), so the flag can never be true in
///             production -- there is no live behavior here to reproduce, only dead code to skip.
///         </item>
///     </list>
/// </remarks>
public sealed class AutoHuntTickSystem(
    WorldDataCache worldData,
    DirtyTracker<int> dirtyTracker,
    IOptions<GameServerOptions> options) : ISimulationSystem
{
    /// <summary>FEQUIP_TYPE::EWEAPON slot index -- same convention as AutoHuntToggleHandler/Zone.ApplySkillCastManaCharge.</summary>
    private const byte WeaponSlot = 7;

    /// <summary>
    ///     The persistent no-mana counter value at which the character is relocated to the auto zone; strictly
    ///     beyond it the session is disconnected (S07_MyGame04.cpp:2474-2481).
    /// </summary>
    private const int NoManaRelocateThreshold = 1000;

    /// <summary>
    ///     Exactly the skill-id switch BotBuff() recognizes (S07_MyGame04.cpp:2337-2459), mapped to which
    ///     BUFF_INFO slot(s) gate a re-cast. Legacy checks only these specific slot(s) to decide a skill is
    ///     "already buffed" -- not necessarily every slot the cast itself writes (skills 7/26/45 write slots 4
    ///     and 6 but only ever gate on slot 4). Every other buff_store skill id -- including the Charge family
    ///     (6/25/44) and the party "formation" skills (76/77/79/81), both ordinary SelfBuff entries in
    ///     <see cref="SkillEffectCatalog" /> for a manual op30 cast -- falls through legacy's own
    ///     "default: continue" and is never auto-cast.
    /// </summary>
    private static readonly FrozenDictionary<int, ImmutableArray<int>> AutoCastGateSlots =
        new Dictionary<int, ImmutableArray<int>>
        {
            [7] = [4], [26] = [4], [45] = [4], // Element Attack / Attack Speed
            [11] = [1], [34] = [1], [49] = [1], // Defense Power
            [15] = [0], [30] = [0], [53] = [0], // Attack Power
            [19] = [3, 7], [38] = [3, 7], [57] = [3, 7], // Attack Block / Run Speed
            [82] = [9], // Holy Shield -- legacy also gates on slot 31, not modeled anywhere else in Fenrir
            [83] = [10], // Critical
            [84] = [11], // Luck
            [103] = [12], // Return Success
            [104] = [13], // Stun Defense
            [105] = [14] // Destroy Success
        }.ToFrozenDictionary();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            RunBotUpkeep(zone, state, legacyTicksElapsed);
    }

    private void RunBotUpkeep(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed)
    {
        if (!state.AutoHuntEnabled || state.AutoHuntConfig is not { } config)
            return;

        // Group D (S07_MyGame04.cpp:787-824): the two-tier paid auto-hunt-time budget decrement runs FIRST and
        // is gated only on the auto-hunt flag (NOT the buff/hotkey death/stun/zone-type gate below). On
        // exhaustion it relocates the character to the auto zone and abandons the rest of this tick's upkeep.
        if (AdvanceBudgetAndRelocateIfExhausted(state, legacyTicksElapsed))
            return;

        // BotBuff/BotHotKey's own top-level gates: mCheckDeath, aPShopState == 1, aManaValue < 1, !mCheckStun
        // (S07_MyGame04.cpp:341, "!mCheckStun" -- previously undocumented as a real gap, now modeled).
        if (state.IsDead || state.PshopOpen || state.Mana < 1 || state.IsStunned)
            return;

        // Same guard line also wraps BotBuff()/BotHotKey() in a four-way zone-server-type gate -- see
        // IsSuppressedByZoneServerType's own remarks for exactly which of the four can ever be true.
        if (IsSuppressedByZoneServerType(zone))
            return;

        TryAutoCastBuff(zone, state, config);

        // Group A (hotkey resupply, S07_MyGame04.cpp:2499-2559) would run here next. Its decision is implemented
        // and unit-tested as Hotkeys.BotHotKeyResupplyPolicy, but is deliberately NOT applied on this live path
        // yet -- see this class's remarks and that policy's own for the prerequisites (Hotkeys population at
        // entry, the op120 notification's uncited wire mapping, and a persistence path) a follow-up must close.
    }

    private void TryAutoCastBuff(Zone zone, PlayerRuntimeState state, AutoHunt config)
    {
        // aBotSkillNum: only the first 2 configured slots, or all 8 while a "continuous auto-buff" cash-shop
        // timer is active (AutoBuffTime, CZ_CONTINUE_SKILL_USE_SEND op95 -- see ContinueSkillUseHandler).
        var slotCount = state.AutoBuffTime >= GameDate.Today() ? 8 : 2;

        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, WeaponSlot)?.ItemId;
        var weaponSort = weaponItemId is { } itemId && worldData.ItemsById.TryGetValue(itemId, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        // Legacy mCheckZone126TypeServer (S07_MyGame01.cpp:895-913, server numbers 210-218) -- resolved via the
        // canonical, config-driven Zone.IsZone126TypeZone (Zone.ZoneTypeClassification.cs /
        // GameServerOptions.Zone126TypeMapIds) rather than a hardcoded MapId literal, matching the same
        // server-number-to-map-id translation MonsterDropTailResolver/MonsterSpawnScheduler already use for this
        // exact flag. Empty by default (inert on every shard until an operator lists a map id), same posture as
        // IsZone241TypeZone/IsZone039TypeZone.
        var isZone126 = zone.IsZone126TypeZone;

        for (var i = 0; i < slotCount; i++)
        {
            var skillId = config.BuffStore[i * 2];
            if (skillId < 1 || !AutoCastGateSlots.TryGetValue(skillId, out var gateSlots))
                continue;

            if (IsAlreadyActive(state, gateSlots))
                continue;

            // GetMaxSkillGradeNum: clamps to the character's own learned grade, -1 if the skill was never
            // learned at all -- matched verbatim rather than skipped, since SkillCatalog.ReturnSkillValue
            // already resolves any sub-1 grade to a harmless zero-cost/zero-value/zero-duration cast, the same
            // fate this slot would meet in legacy too. This is the one stored-config field legacy re-validates
            // server-side at use time (S07_MyGame04.cpp:2333-2336).
            var requestedGrade = config.BuffStore[i * 2 + 1];
            var grade = Math.Min(requestedGrade, GetMaxLearnedGrade(skillId, state.LearnedSkills));

            worldData.SkillsById.TryGetValue(skillId, out var skillDef);
            var result = SkillCastResolver.TryCast(skillDef, grade, state.Mana, maxLife, weaponSort,
                state.SupportSkillTimeUpRatio);

            if (result.Success && result.Kind == SkillEffectKind.SelfBuff)
            {
                state.Mana -= result.ManaCost;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
                zone.ApplyBuffWrites(state, result.BuffWrites);
                state.NoManaCount =
                    0; // a proceeding cast resets the no-mana escalation counter (S07_MyGame04.cpp:2485)
                return; // BotBuff's own `break` -- at most one auto-cast per legacy tick.
            }

            // Group C (S07_MyGame04.cpp:2469-2485): on a zone-126-type server, a SELECTED buff (one that cleared
            // the whitelist + already-buffed gate) whose mana cost exceeds current mana escalates the no-mana
            // counter instead of casting, and legacy breaks here regardless. Outside a zone-126-type server the
            // counter never grows and this slot is simply skipped (we do NOT reproduce legacy's cast-into-
            // negative-mana fall-through -- see this class's remarks for why).
            if (isZone126 && result.Failure == SkillCastResolver.FailureReason.InsufficientMana)
            {
                EscalateNoMana(state);
                return;
            }

            // Any other skip reason (wrong weapon class, non-buff, or a non-zone-126 mana shortfall): try the
            // next configured slot, matching the pre-B11 loop's own continue-on-failure behavior.
        }
    }

    /// <summary>
    ///     Group D: advances the two-tier paid auto-hunt-time budget via <see cref="AutoHuntBudgetPolicy" /> and,
    ///     when both tiers reach zero, sends the relocate-to-auto-zone signal (the real
    ///     <c>ZC_RETURN_TO_AUTO_ZONE</c>, same packet <see cref="AntiCampingForcedReturnSystem" /> uses) and
    ///     returns <see langword="true" /> so the caller abandons the rest of this tick's upkeep.
    /// </summary>
    /// <remarks>
    ///     The day/minute-budget CHANGE notifications (S07_MyGame04.cpp:795,808) are deferred: no auto-hunt-time
    ///     change wire packet exists in Fenrir yet (none identified this session) -- a fenrir-wire-protocol-
    ///     implementer concern flagged in this workstream's wiring notes. Because no paid-time acquisition path
    ///     grants a budget yet, both tiers are always zero and this whole step is a dormant no-op in practice
    ///     (<see cref="AutoHuntBudgetPolicy" />'s present-based gate), so the missing change notifications have no
    ///     observable effect today; only the exhaustion relocation (real packet) is wired.
    /// </remarks>
    private static bool AdvanceBudgetAndRelocateIfExhausted(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        var result = AutoHuntBudgetPolicy.Advance(state.AutoHuntPaidDayBudget, state.AutoHuntPaidMinuteBudget,
            state.AutoHuntBudgetMinuteAccrualTicks, legacyTicksElapsed, GameDate.Today());

        state.AutoHuntPaidDayBudget = result.DayBudget;
        state.AutoHuntPaidMinuteBudget = result.MinuteBudget;
        state.AutoHuntBudgetMinuteAccrualTicks = result.MinuteAccrualTicks;

        if (result.Signal != AutoHuntBudgetPolicy.Signal.Exhausted)
            return false;

        state.Session.Send(new ReturnToHomeZoneResponse());
        return true;
    }

    /// <summary>
    ///     Group C escalation (S07_MyGame04.cpp:2469-2485): increment the persistent no-mana counter; at exactly
    ///     <see cref="NoManaRelocateThreshold" /> relocate the character to the auto zone, strictly beyond it
    ///     disconnect the session. Incremented once per <see cref="Simulate" /> pass (the bot buff-cast is itself
    ///     at-most-once-per-tick), never scaled by a stalled-host catch-up burst, so the exact-1000 relocate
    ///     threshold is always reached before the disconnect step rather than skipped by a multi-tick jump.
    /// </summary>
    private static void EscalateNoMana(PlayerRuntimeState state)
    {
        state.NoManaCount++;
        if (state.NoManaCount == NoManaRelocateThreshold)
            state.Session.Send(new ReturnToHomeZoneResponse());
        else if (state.NoManaCount > NoManaRelocateThreshold && state.Session is ClientSession client)
            client.Abort(DisconnectReason.StateViolation);
    }

    /// <summary>
    ///     BotBuff/BotHotKey's own four-way zone-server-type gate (S07_MyGame04.cpp:341-350;
    ///     ServerDocs/12_ts25zone/13_MyGame04_06_07_Avatar_Item_Tick.md §2.3): the entire auto-hunt
    ///     buff-cast/hotkey-refill routine is suppressed outright on a Regular War, Stone War, Sacred-Stone
    ///     ("zone 038"), or Rebirth-chain instance zone. Each term is a static, per-map identity fixed at legacy
    ///     zone-server boot from that process's own configured server number -- never a dynamic "war currently in
    ///     progress" toggle -- so this reads existing per-map/per-shard classification rather than tracking
    ///     anything new over time:
    ///     <list type="bullet">
    ///         <item>
    ///             Regular War (zone 049): true for any of the 11 <see cref="RegularWarMapCatalog.ConfiguredMaps" />
    ///             (Server/ts25zone/S07_MyGame01.cpp:647-698, the only write site to this flag in the codebase).
    ///         </item>
    ///         <item>
    ///             Stone War (zone 053): omitted -- its own initialization is compiled only under a build macro
    ///             never defined in any shipped configuration (Server/ts25zone/S07_MyGame01.cpp:731-791;
    ///             ServerDocs/12_ts25zone/09_MyGame01_PartieB.md:113-119), so the flag can never be true in
    ///             production.
    ///         </item>
    ///         <item>
    ///             Sacred Stone / "zone 038" (Server/ts25zone/S07_MyGame01.cpp:793-818;
    ///             ServerDocs/12_ts25zone/08_MyGame01_PartieA.md:428 -- distinct from Stone War above): true only
    ///             when this zone is the shard's designated <see cref="GameServerOptions.HolyStoneMapId" /> AND
    ///             <see cref="GameServerOptions.HolyStoneWarEnabled" /> is armed -- the map id alone is not
    ///             enough, matching legacy's own two-independent-conditions requirement.
    ///         </item>
    ///         <item>
    ///             Rebirth-chain (zone 241): <see cref="Zone.IsZone241TypeZone" />
    ///             (Server/ts25zone/S07_MyGame01.cpp:1209-1256; the per-tick re-assertion at :2694-2701 is the
    ///             same static value, not a dynamic re-check).
    ///         </item>
    ///     </list>
    /// </summary>
    private bool IsSuppressedByZoneServerType(Zone zone)
    {
        if (RegularWarMapCatalog.TryGet(zone.MapId, out _))
            return true;

        if (options.Value.HolyStoneWarEnabled && zone.MapId == options.Value.HolyStoneMapId)
            return true;

        return zone.IsZone241TypeZone;
    }

    private static bool IsAlreadyActive(PlayerRuntimeState state, ImmutableArray<int> gateSlots)
    {
        foreach (var slot in gateSlots)
            if (state.Buffs.Buff[slot * 2 + 1] > 0)
                return true;

        return false;
    }

    /// <summary>Mirrors MyUtil::GetMaxSkillGradeNum exactly (same posture as AutoBuffSkillResolver's own independent copy).</summary>
    private static int GetMaxLearnedGrade(int skillId, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == skillId)
                return learned.Grade;

        return -1;
    }
}
