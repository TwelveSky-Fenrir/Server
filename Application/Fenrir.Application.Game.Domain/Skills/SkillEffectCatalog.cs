using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Which BUFF_INFO slot(s) a non-attack skill cast (op15 CZ_AVATAR_ACTION_SEND, action-category Sort
///     resolving to the real skill-cast category -- Sorts 32, 33, 38-90, NOT action-Sort 30, which is the
///     unrelated stand-up-from-death request) writes, and with which SkillValueKind supplies the
///     value/duration -- ported verbatim from MyUtil::ProcessForCreateBuff (S07_MyGame03.cpp:9315-9631).
/// </summary>
/// <remarks>
///     Not reproduced: skill 82's zone-124 cooldown and the dead MG5ORIGIN_ECAPE block.
///     <para>
///         <c>mSupportSkillTimeUpRatio</c> (behavior contract "buff-application-stacking-decay"): every
///         <c>AddSelfBuff</c>-registered entry below is one of the 14 genuine self-buff duration-write sites
///         (S07_MyGame03.cpp:9329,9337,9341,9354,9367,9380,9384,9399,9430,9439,9445,9579,9585,9591) and marks
///         <see cref="SkillEffectDefinition.AppliesSupportSkillTimeUpRatio" /> true; the 4
///         <c>AddPartyBuffAsSelf</c> entries (76/77/79/81, S07_MyGame03.cpp:9595-9622) use the identical
///         duration lookup but their write lines (9597/9604/9611/9618) never multiply by the ratio --
///         confirmed by direct citation, not an oversight, so they keep the flag at its default false. See
///         <see cref="SkillCastResolver.TryCast" />'s own remarks for where the flag is consumed.
///     </para>
///     <para>
///         Formation skills 76/77/79/81 (behavior contract "Formation Party-Buff Exact-Five-Member Gate"):
///         <c>MyUtil::ProcessForCreateBuff</c> only ever writes the evaluating avatar's own buff slot (the
///         <c>wBuff</c> macro always resolves to the passed-in avatar, Server/Header/Protocol/DEFINE.h:715), but
///         <c>AVATAR_OBJECT::ProcessForCreateEffectValue</c> (S07_MyGame04.cpp:1333-1383) gates that write on the
///         caster's own party having exactly <see cref="Social.Party.PartyRegistry.MaxMembers" /> ready members
///         sharing the same party -- short of 5 (including solo), no buff is written even to the caster. Fenrir
///         marks these 4 entries <see cref="SkillEffectDefinition.RequiresFullParty" /> true; the actual
///         zone-local presence count runs in <c>Zone.HasFullPartyPresent</c> (<c>Zone.PlayerLifecycle.cs</c>),
///         mirroring the same <see cref="Social.Party.PartyRegistry" />-based pattern already used by the
///         team-stun exact-5 gate (<c>Zone.Stun.cs</c>'s <c>ApplyTeamStunSubMechanic</c>). Not reproduced: the
///         two-step <c>mParty_Buff_Act</c> CAST/DONE cast-lifecycle marker the legacy also requires
///         (S04_MyWork02.cpp:1684-1699) -- Fenrir's existing op15-then-matching-op16 skill/grade staleness check
///         (<c>Zone.ApplySkillEffectConfirm</c>) already enforces the same "a cast must have started before this
///         confirm applies" ordering the marker exists for, so this is treated as the structural equivalent
///         rather than a separately tracked field; and whether all 5 party members are independently buffed
///         (each would need their own client to independently satisfy the same gate) is explicitly unverified
///         by that contract from server-side code alone -- only the evaluating avatar's own write is reproduced
///         here, matching what the cited code actually does.
///     </para>
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

        // Party "Formation" skills -- self-only write, gated on an exactly-full 5-member party (see class remarks).
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
            requiredWeaponSorts?.ToImmutableArray() ?? ImmutableArray<int>.Empty,
            true);

        foreach (var skillId in skillIds)
            entries[skillId] = definition;
    }

    private static void AddPartyBuffAsSelf(Dictionary<int, SkillEffectDefinition> entries, int skillId,
        (int Slot, SkillValueKind Kind, bool IsPercentOfMaxLife)[] slots)
    {
        // AppliesSupportSkillTimeUpRatio deliberately left at its default false -- see this class's own
        // remarks for the citation confirming these 4 formation skills never receive the ratio.
        entries[skillId] = new SkillEffectDefinition(
            SkillEffectKind.SelfBuff,
            slots.Select(s => new BuffEffectSlot(s.Slot, s.Kind, s.IsPercentOfMaxLife)).ToImmutableArray(),
            ImmutableArray<int>.Empty,
            RequiresFullParty: true);
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
    ImmutableArray<int> RequiredWeaponSorts,
    // True for the 14 genuine self-buff sites (SkillEffectCatalog.AddSelfBuff), false (default) for the 4
    // formation/party-support skills collapsed to self-only (SkillEffectCatalog.AddPartyBuffAsSelf) and for
    // HealLife/HealMana (irrelevant there -- neither has a duration). See SkillEffectCatalog's own class
    // remarks for the citation backing this split.
    bool AppliesSupportSkillTimeUpRatio = false,
    // True only for the 4 Formation party-buff skills (76/77/79/81, SkillEffectCatalog.AddPartyBuffAsSelf).
    // The caller (Zone.ApplySkillEffectConfirm) must additionally confirm the caster's own party has exactly
    // PartyRegistry.MaxMembers ready members present in this same zone before writing any buff slot at all --
    // see SkillEffectCatalog's own class remarks for the citation backing this gate.
    bool RequiresFullParty = false)
{
    public static readonly SkillEffectDefinition HealLife =
        new(SkillEffectKind.HealLife, ImmutableArray<BuffEffectSlot>.Empty, ImmutableArray<int>.Empty);

    public static readonly SkillEffectDefinition HealMana =
        new(SkillEffectKind.HealMana, ImmutableArray<BuffEffectSlot>.Empty, ImmutableArray<int>.Empty);
}
