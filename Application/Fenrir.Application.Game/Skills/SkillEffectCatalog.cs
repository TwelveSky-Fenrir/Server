using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Which BUFF_INFO slot(s) a non-attack skill cast (AVATAR_ACTION_SEND Sort=30) writes, and with which
///     SkillValueKind supplies the value/duration -- ported verbatim from MyUtil::ProcessForCreateBuff
///     (S07_MyGame03.cpp:9315-9631).
/// </summary>
/// <remarks>
///     Not reproduced: full 5-member party gating for skills 76/77/79/81 (no Party subsystem yet, so these
///     collapse to self-only casts); skill 82's zone-124 cooldown and the dead MG5ORIGIN_ECAPE block;
///     mSupportSkillTimeUpRatio duration multiplier (no such field yet, so every duration below is raw RunTime).
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

        AddSelfBuff(entries, [6, 25, 44], [(8, SkillValueKind.ChargingDamageUp, false)]); // Charge

        // Element Attack + Attack Speed; slot 6 reads AttackSpeedUp, not ElementDefenseUp.
        AddSelfBuff(entries, [7, 26, 45],
            [(4, SkillValueKind.ElementAttackUp, false), (6, SkillValueKind.AttackSpeedUp, false)]);

        AddSelfBuff(entries, [11, 34, 49], [(1, SkillValueKind.DefensePowerUp, false)], [13, 17, 19]); // Defense Power
        AddSelfBuff(entries, [15, 30, 53], [(0, SkillValueKind.AttackPowerUp, false)], [14, 16, 20]); // Attack Power

        AddSelfBuff(entries, [19, 38, 57],
            [(3, SkillValueKind.AttackBlockUp, false), (7, SkillValueKind.RunSpeedUp, false)], [15, 18, 21]);

        AddSelfBuff(entries, [82],
            [(9, SkillValueKind.ShieldLifeUp, true)]); // Holy Shield, value = ratio% x MaxLife x 0.01
        AddSelfBuff(entries, [83], [(10, SkillValueKind.CriticalUp, false)]);
        AddSelfBuff(entries, [84], [(11, SkillValueKind.LuckUp, false)]);

        AddSelfBuff(entries, [103], [(12, SkillValueKind.ReturnSuccessUp, false)]);
        AddSelfBuff(entries, [104], [(13, SkillValueKind.StunDefenseUp, false)]);
        AddSelfBuff(entries, [105], [(14, SkillValueKind.DestroySuccessUp, false)]);

        // Party "Formation" skills -- collapsed to self-only (see class remarks).
        AddPartyBuffAsSelf(entries, 76, [(2, SkillValueKind.AttackSuccessUp, false)]);
        AddPartyBuffAsSelf(entries, 77, [(3, SkillValueKind.AttackBlockUp, false)]);
        AddPartyBuffAsSelf(entries, 79, [(9, SkillValueKind.ShieldLifeUp, true)]);
        AddPartyBuffAsSelf(entries, 81, [(10, SkillValueKind.CriticalUp, false)]);

        // Targeted HP heal, flat amount = RecoverInfo1.
        foreach (var skillId in (int[])[106, 108, 110])
            entries[skillId] = SkillEffectDefinition.HealLife;

        // Targeted MP heal, flat amount = RecoverInfo2.
        foreach (var skillId in (int[])[107, 109, 111])
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

/// <summary>One BUFF_INFO slot a skill writes on cast: value from Kind, duration always from SkillValueKind.RunTime.</summary>
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
