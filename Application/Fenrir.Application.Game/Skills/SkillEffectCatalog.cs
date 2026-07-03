using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Which BUFF_INFO slot(s) a non-attack skill cast (<c>AVATAR_ACTION_SEND</c> Sort=30, report 12 §4.2)
///     writes, and with which <see cref="SkillValueKind" /> supplies the value/duration -- the skill-ID
///     whitelist and per-ID wiring is copied VERBATIM from <c>MyUtil::ProcessForCreateBuff</c>, read in full
///     (<c>Server/ts25zone/S07_MyGame03.cpp:9315-9631</c>), not inferred from field-name similarity. Every
///     entry cites its exact source lines.
/// </summary>
/// <remarks>
///     NOT reproduced (open issues, explicitly out of this pass's scope -- see this task's StructuredOutput):
///     <list type="bullet">
///         <item>Party-wide application for skills 76/77/79/81 ("Formation" skills): the legacy requires a
///             FULL 5-member party present (<c>mParty_Buff_Act</c>/<c>MAX_PARTY_AVATAR_NUM</c> gate,
///             S07_MyGame04.cpp:1344-1375) before buffing every member. Fenrir has no Party subsystem yet, so
///             these four collapse to a SELF-only cast here -- strictly weaker than legacy, never exploitable
///             the other way.</item>
///         <item>Skill 82's zone-124 10s real-time additional cooldown (<c>mLastHSTick</c>) and the dead
///             <c>MG5ORIGIN_ECAPE</c> cape-redirect block (that macro is never <c>#define</c>d anywhere in the
///             tree -- confirmed dead code, correctly NOT ported).</item>
///         <item><c>mSupportSkillTimeUpRatio</c> (duration multiplier, up to ×4 for premium): no such field
///             exists on <c>PlayerRuntimeState</c> yet -- every duration below is the raw <c>RunTime</c> value,
///             equivalent to a ratio of 1.0.</item>
///     </list>
/// </remarks>
public static class SkillEffectCatalog
{
    private static readonly FrozenDictionary<int, SkillEffectDefinition> BySkillId = Build();

    public static bool TryGet(int skillId, out SkillEffectDefinition definition)
    {
        return BySkillId.TryGetValue(skillId, out definition!);
    }

    private static FrozenDictionary<int, SkillEffectDefinition> Build()
    {
        var entries = new Dictionary<int, SkillEffectDefinition>();

        // l.9325-9332: Charge (buff slot 8).
        AddSelfBuff(entries, [6, 25, 44], [(8, SkillValueKind.ChargingDamageUp, false)]);

        // l.9333-9344: Element Attack (slot 4) + Attack Speed (slot 6), no weapon gate. Slot 6 reads factor 18
        // (AttackSpeedUp, GameSystem_03_Skill.cpp:151-154) -- NOT factor 17/ElementDefenseUp, a prior pass here
        // guessed the "obvious" element-defense pairing without checking the actual source column read.
        AddSelfBuff(entries, [7, 26, 45],
            [(4, SkillValueKind.ElementAttackUp, false), (6, SkillValueKind.AttackSpeedUp, false)]);

        // l.9345-9357: Defense Power (slot 1), requires weapon iSort in {13,17,19} ("def"-class weapons).
        AddSelfBuff(entries, [11, 34, 49], [(1, SkillValueKind.DefensePowerUp, false)], [13, 17, 19]);

        // l.9358-9370: Attack Power (slot 0), requires weapon iSort in {14,16,20} ("atk"-class weapons).
        AddSelfBuff(entries, [15, 30, 53], [(0, SkillValueKind.AttackPowerUp, false)], [14, 16, 20]);

        // l.9371-9387: Attack Block (slot 3) + Run Speed (slot 7), requires weapon iSort in {15,18,21} ("spd").
        AddSelfBuff(entries, [19, 38, 57],
            [(3, SkillValueKind.AttackBlockUp, false), (7, SkillValueKind.RunSpeedUp, false)], [15, 18, 21]);

        // l.9388-9436 (minus the dead MG5ORIGIN_ECAPE block): Holy Shield (slot 9), value = ratio% x MaxLife x 0.01.
        AddSelfBuff(entries, [82], [(9, SkillValueKind.ShieldLifeUp, true)]);

        // l.9437-9442: Critical (slot 10).
        AddSelfBuff(entries, [83], [(10, SkillValueKind.CriticalUp, false)]);

        // l.9443-9448: Luck (slot 11).
        AddSelfBuff(entries, [84], [(11, SkillValueKind.LuckUp, false)]);

        // l.9577-9582 / 9583-9588 / 9589-9594: the 3 "10x" support skills (slots 12/13/14).
        AddSelfBuff(entries, [103], [(12, SkillValueKind.ReturnSuccessUp, false)]);
        AddSelfBuff(entries, [104], [(13, SkillValueKind.StunDefenseUp, false)]);
        AddSelfBuff(entries, [105], [(14, SkillValueKind.DestroySuccessUp, false)]);

        // l.9595-9622: party "Formation" skills -- collapsed to self-only here (class remarks).
        AddPartyBuffAsSelf(entries, 76, [(2, SkillValueKind.AttackSuccessUp, false)]);
        AddPartyBuffAsSelf(entries, 77, [(3, SkillValueKind.AttackBlockUp, false)]);
        AddPartyBuffAsSelf(entries, 79, [(9, SkillValueKind.ShieldLifeUp, true)]);
        AddPartyBuffAsSelf(entries, 81, [(10, SkillValueKind.CriticalUp, false)]);

        // l.9449-9511: targeted HP heal, flat amount = RecoverInfo1 (kind 2).
        foreach (var skillId in (int[]) [106, 108, 110])
            entries[skillId] = SkillEffectDefinition.HealLife;

        // l.9512-9576: targeted MP heal, flat amount = RecoverInfo2 (kind 3).
        foreach (var skillId in (int[]) [107, 109, 111])
            entries[skillId] = SkillEffectDefinition.HealMana;

        return entries.ToFrozenDictionary();
    }

    private static void AddSelfBuff(Dictionary<int, SkillEffectDefinition> entries, int[] skillIds,
        (int Slot, SkillValueKind Kind, bool IsPercentOfMaxLife)[] slots, int[]? requiredWeaponSorts = null)
    {
        var definition = new SkillEffectDefinition(
            SkillEffectKind.SelfBuff,
            slots.Select(s => new BuffEffectSlot(s.Slot, s.Kind, s.IsPercentOfMaxLife)).ToImmutableArray(),
            requiredWeaponSorts?.ToImmutableArray() ?? ImmutableArray<int>.Empty);

        foreach (var skillId in skillIds)
            entries[skillId] = definition;
    }

    private static void AddPartyBuffAsSelf(Dictionary<int, SkillEffectDefinition> entries, int skillId,
        (int Slot, SkillValueKind Kind, bool IsPercentOfMaxLife)[] slots)
    {
        entries[skillId] = new SkillEffectDefinition(
            SkillEffectKind.SelfBuff,
            slots.Select(s => new BuffEffectSlot(s.Slot, s.Kind, s.IsPercentOfMaxLife)).ToImmutableArray(),
            ImmutableArray<int>.Empty);
    }
}

public enum SkillEffectKind
{
    SelfBuff,
    HealLife,
    HealMana
}

/// <summary>One BUFF_INFO slot a skill writes on cast: value from <see cref="Kind" />, duration always from <see cref="SkillValueKind.RunTime" />.</summary>
public readonly record struct BuffEffectSlot(int Slot, SkillValueKind Kind, bool IsPercentOfMaxLife);

public sealed record SkillEffectDefinition(
    SkillEffectKind Kind,
    ImmutableArray<BuffEffectSlot> BuffSlots,
    ImmutableArray<int> RequiredWeaponSorts)
{
    public static readonly SkillEffectDefinition HealLife =
        new(SkillEffectKind.HealLife, ImmutableArray<BuffEffectSlot>.Empty, ImmutableArray<int>.Empty);

    public static readonly SkillEffectDefinition HealMana =
        new(SkillEffectKind.HealMana, ImmutableArray<BuffEffectSlot>.Empty, ImmutableArray<int>.Empty);
}
