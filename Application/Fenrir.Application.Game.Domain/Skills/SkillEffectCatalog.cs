using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Skills;

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

        AddSelfBuff(entries, [6, 25, 44], [(8, SkillValueKind.ChargingDamageUp, false)]);

        AddSelfBuff(entries, [7, 26, 45],
            [(4, SkillValueKind.ElementAttackUp, false), (6, SkillValueKind.AttackSpeedUp, false)]);

        AddSelfBuff(entries, [11, 34, 49], [(1, SkillValueKind.DefensePowerUp, false)], [13, 17, 19]);
        AddSelfBuff(entries, [15, 30, 53], [(0, SkillValueKind.AttackPowerUp, false)], [14, 16, 20]);

        AddSelfBuff(entries, [19, 38, 57],
            [(3, SkillValueKind.AttackBlockUp, false), (7, SkillValueKind.RunSpeedUp, false)], [15, 18, 21]);

        AddSelfBuff(entries, [82],
            [(9, SkillValueKind.ShieldLifeUp, true)]);
        AddSelfBuff(entries, [83], [(10, SkillValueKind.CriticalUp, false)]);
        AddSelfBuff(entries, [84], [(11, SkillValueKind.LuckUp, false)]);

        AddSelfBuff(entries, [103], [(12, SkillValueKind.ReturnSuccessUp, false)]);
        AddSelfBuff(entries, [104], [(13, SkillValueKind.StunDefenseUp, false)]);
        AddSelfBuff(entries, [105], [(14, SkillValueKind.DestroySuccessUp, false)]);

        AddPartyBuffAsSelf(entries, 76, [(2, SkillValueKind.AttackSuccessUp, false)]);
        AddPartyBuffAsSelf(entries, 77, [(3, SkillValueKind.AttackBlockUp, false)]);
        AddPartyBuffAsSelf(entries, 79, [(9, SkillValueKind.ShieldLifeUp, true)]);
        AddPartyBuffAsSelf(entries, 81, [(10, SkillValueKind.CriticalUp, false)]);

        foreach (var skillId in (int[])[106, 108, 110])
            entries[skillId] = SkillEffectDefinition.HealLife;

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
            requiredWeaponSorts?.ToImmutableArray() ?? ImmutableArray<int>.Empty,
            true);

        foreach (var skillId in skillIds)
            entries[skillId] = definition;
    }

    private static void AddPartyBuffAsSelf(Dictionary<int, SkillEffectDefinition> entries, int skillId,
        (int Slot, SkillValueKind Kind, bool IsPercentOfMaxLife)[] slots)
    {
        entries[skillId] = new SkillEffectDefinition(
            SkillEffectKind.SelfBuff,
            slots.Select(s => new BuffEffectSlot(s.Slot, s.Kind, s.IsPercentOfMaxLife)).ToImmutableArray(),
            ImmutableArray<int>.Empty,
            RequiresFullParty: true);
    }
}

public enum SkillEffectKind
{

        None,

    SelfBuff,
    HealLife,
    HealMana
}

public readonly record struct BuffEffectSlot(int Slot, SkillValueKind Kind, bool IsPercentOfMaxLife);

public sealed record SkillEffectDefinition(
    SkillEffectKind Kind,
    ImmutableArray<BuffEffectSlot> BuffSlots,
    ImmutableArray<int> RequiredWeaponSorts,
    bool AppliesSupportSkillTimeUpRatio = false,
    bool RequiresFullParty = false)
{
    public static readonly SkillEffectDefinition HealLife =
        new(SkillEffectKind.HealLife, ImmutableArray<BuffEffectSlot>.Empty, ImmutableArray<int>.Empty);

    public static readonly SkillEffectDefinition HealMana =
        new(SkillEffectKind.HealMana, ImmutableArray<BuffEffectSlot>.Empty, ImmutableArray<int>.Empty);
}
