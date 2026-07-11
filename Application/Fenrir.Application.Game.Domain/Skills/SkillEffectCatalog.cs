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
    /// <summary>
    ///     No registered buff/heal effect exists for the cast skill id (e.g. starter skills 1/20/39,
    ///     2/3/21/22/40/41, 4/5/23/24/42/43 -- see <see cref="SkillCastResolver.TryCast" />'s own remarks).
    ///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:1509-1564 (buff-dispatch switch has no case, and no
    ///     default branch, for any of these ids -- control falls straight through to the function's own
    ///     return with no write and no broadcast) and Server/ts25zone/S07_MyGame03.cpp:9315-9631
    ///     (<c>ProcessForCreateBuff</c>'s own switch, same absence). The op15 mana charge still applies for
    ///     this kind -- legacy's mana debit is unconditional on invested grade points alone, independent of
    ///     whether the skill id has any recognized effect (S04_MyWork02.cpp:1640-1650,1680-1683).
    /// </summary>
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
