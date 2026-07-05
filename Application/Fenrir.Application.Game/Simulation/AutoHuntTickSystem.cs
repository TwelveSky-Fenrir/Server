using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     Auto-hunt bot buff loop (<c>AVATAR_OBJECT::BotBuff</c>, S07_MyGame04.cpp:2271-2497): while
///     <see cref="PlayerRuntimeState.AutoHuntEnabled" />, auto-casts the first eligible configured buff skill
///     from <see cref="PlayerRuntimeState.AutoHuntConfig" />'s BuffStore, at most one cast per legacy tick --
///     matching legacy's own single <c>break</c> the moment a slot clears its "already buffed" gate, win or
///     lose on the mana/weapon check that follows.
/// </summary>
/// <remarks>
///     Not reproduced, and why:
///     <list type="bullet">
///         <item>
///             <c>BotHotKey</c>/<c>BotHotKeySend</c> (the hotkey-refill half of the same legacy bot loop) --
///             Fenrir has no in-memory hotkey model at all yet (<see cref="PlayerRuntimeState" /> carries no
///             Hotkeys collection; <c>game.CharacterHotkeys</c> is only ever written once, at character
///             creation, never loaded back into a live Zone) and no potion/food consume-effect system exists
///             either (<see cref="Handlers.UseInventoryItemHandler" />'s own remarks: the iSort==2 potion
///             family is entirely unimplemented). Both are real, larger prerequisite gaps that would need their
///             own investigation, not something to fake here.
///         </item>
///         <item>
///             <c>mCheckStun</c> and the mid-animation <c>aAction.aSort</c> (41/60-68/75) gate -- Fenrir has no
///             stun state and no multi-tick cast-animation state machine (a buff applies instantly, same as
///             every manual op30 cast), so there is nothing equivalent to gate on.
///         </item>
///         <item>
///             <c>MyFactor::GetBonusSkillValue</c>'s equipment-derived skill-grade bonus -- already a
///             documented gap on the manual op30 cast path too (see <see cref="AutoBuffActivationResolver" />'s
///             own remarks); this reuses the exact same <see cref="SkillCastResolver" /> and inherits the same
///             simplification.
///         </item>
///         <item>
///             The zone-type-flag/no-mana escalation branch (<c>mCheckZone126TypeServer</c>'s "kick after 1000
///             failed ticks") -- no zone-type-flag system exists anywhere in Fenrir yet, and legacy itself only
///             exercises the escalation on what BotBuff calls a "test server" configuration; an ordinary
///             insufficient-mana slot is simply skipped here instead.
///         </item>
///     </list>
/// </remarks>
public sealed class AutoHuntTickSystem(WorldDataCache worldData, DirtyTracker<int> dirtyTracker) : ISimulationSystem
{
    /// <summary>FEQUIP_TYPE::EWEAPON slot index -- same convention as AutoHuntToggleHandler/Zone.ApplySkillCast.</summary>
    private const byte WeaponSlot = 7;

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
            TryAutoCastBuff(zone, state);
    }

    private void TryAutoCastBuff(Zone zone, PlayerRuntimeState state)
    {
        if (!state.AutoHuntEnabled || state.AutoHuntConfig is not { } config)
            return;

        // BotBuff's own top-level gates: mCheckDeath, aPShopState == 1, aManaValue < 1.
        if (state.IsDead || state.PshopOpen || state.Mana < 1)
            return;

        // aBotSkillNum: only the first 2 configured slots, or all 8 while a "continuous auto-buff" cash-shop
        // timer is active (AutoBuffTime, CZ_CONTINUE_SKILL_USE_SEND op95 -- see ContinueSkillUseHandler).
        var slotCount = state.AutoBuffTime >= GameDate.Today() ? 8 : 2;

        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, WeaponSlot)?.ItemId;
        var weaponSort = weaponItemId is { } itemId && worldData.ItemsById.TryGetValue(itemId, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

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
            // fate this slot would meet in legacy too.
            var requestedGrade = config.BuffStore[i * 2 + 1];
            var grade = Math.Min(requestedGrade, GetMaxLearnedGrade(skillId, state.LearnedSkills));

            worldData.SkillsById.TryGetValue(skillId, out var skillDef);
            var result = SkillCastResolver.TryCast(skillDef, grade, state.Mana, maxLife, weaponSort);
            if (!result.Success || result.Kind != SkillEffectKind.SelfBuff)
                continue;

            state.Mana -= result.ManaCost;
            dirtyTracker.MarkDirty(state.CharacterId, DirtyFlags.Vitals);
            ApplyBuffWrites(zone, state, result.BuffWrites);
            return; // BotBuff's own `break` -- at most one auto-cast per legacy tick.
        }
    }

    private static bool IsAlreadyActive(PlayerRuntimeState state, ImmutableArray<int> gateSlots)
    {
        foreach (var slot in gateSlots)
            if (state.Buffs.Buff[slot * 2 + 1] > 0)
                return true;

        return false;
    }

    /// <summary>Mirrors Zone's own (private) ApplySkillBuffWrites -- a small, deliberate duplicate rather than exposing it.</summary>
    private static void ApplyBuffWrites(Zone zone, PlayerRuntimeState state,
        ImmutableArray<SkillCastResolver.BuffWrite> writes)
    {
        if (writes.IsEmpty)
            return;

        var changedSlots = new int[35];
        foreach (var write in writes)
        {
            if (write.Slot is < 0 or >= 35)
                continue;
            state.Buffs.Buff[write.Slot * 2] = write.Value;
            state.Buffs.Buff[write.Slot * 2 + 1] = write.DurationTicks;
            changedSlots[write.Slot] = 1;
        }

        zone.RecomputeStatsAndBroadcastBuffs(state, changedSlots);
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
