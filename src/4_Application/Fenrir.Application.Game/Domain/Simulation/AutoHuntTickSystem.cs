using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class AutoHuntTickSystem(
    WorldDataCache worldData,
    DirtyTracker<int> dirtyTracker,
    IOptions<GameServerOptions> options) : ISimulationSystem
{
    private const int NoManaRelocateThreshold = 1000;

    private static readonly FrozenDictionary<int, ImmutableArray<int>> AutoCastGateSlots =
        new Dictionary<int, ImmutableArray<int>>
        {
            [7] = [4], [26] = [4], [45] = [4],
            [11] = [1], [34] = [1], [49] = [1],
            [15] = [0], [30] = [0], [53] = [0],
            [19] = [3, 7], [38] = [3, 7], [57] = [3, 7],
            [82] = [9],
            [83] = [10],
            [84] = [11],
            [103] = [12],
            [104] = [13],
            [105] = [14]
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

        if (AdvanceBudgetAndRelocateIfExhausted(state, legacyTicksElapsed))
            return;

        if (state.IsDead || state.PshopOpen || state.Mana < 1 || state.IsStunned)
            return;

        if (IsSuppressedByZoneServerType(zone))
            return;

        TryAutoCastBuff(zone, state, config);
    }

    private void TryAutoCastBuff(Zone zone, PlayerRuntimeState state, AutoHunt config)
    {
        var slotCount = state.AutoBuffTime >= GameDate.Today() ? 8 : 2;

        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot)?.ItemId;
        var weaponSort = weaponItemId is { } itemId && worldData.ItemsById.TryGetValue(itemId, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;

        var isZone126 = zone.IsZone126TypeZone;

        for (var i = 0; i < slotCount; i++)
        {
            var skillId = config.BuffStore[i * 2];
            if (skillId < 1 || !AutoCastGateSlots.TryGetValue(skillId, out var gateSlots))
                continue;

            if (IsAlreadyActive(state, gateSlots))
                continue;

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
                    0;
                return;
            }

            if (isZone126 && result.Failure == SkillCastResolver.FailureReason.InsufficientMana)
            {
                EscalateNoMana(state);
                return;
            }
        }
    }

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

    private static void EscalateNoMana(PlayerRuntimeState state)
    {
        state.NoManaCount++;
        if (state.NoManaCount == NoManaRelocateThreshold)
            state.Session.Send(new ReturnToHomeZoneResponse());
        else if (state.NoManaCount > NoManaRelocateThreshold && state.Session is ClientSession client)
            client.Abort(DisconnectReason.StateViolation);
    }

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

    private static int GetMaxLearnedGrade(int skillId, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == skillId)
                return learned.Grade;

        return -1;
    }
}
