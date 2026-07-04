using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Skills;

/// <summary>
///     Pure resolver for CZ_PROCESS_DATA_SEND tSort 202/233 (learn a new skill from an NPC's skill-tree
///     offer -- <c>ProcessForLearnSkill1</c>/<c>ProcessForLearnSkill2</c>, verified against
///     <c>Server/ts25zone/S04_MyWork05.cpp:375/3343</c>: two DIFFERENT legacy arrays,
///     <c>nSkillInfo1[3][8]</c>/<c>nSkillInfo2[3][3][3][8]</c> -- <c>world.NpcSkillOffers.ArrayKind</c>
///     distinguishes them -- but otherwise IDENTICAL slot-finding logic) and tSort 203
///     (<c>ProcessForSkillUpgrade</c>, S04_MyWork05.cpp:555). No I/O, no <see cref="World.Zone" />
///     dependency.
/// </summary>
/// <remarks>
///     EVERY failure branch in all three legacy functions is a <c>Quit()</c> -- neither has a graceful
///     <c>*tResult=1</c> path at all (unlike, say, <c>ProcessForNPCShopToInventory</c>'s buy side). The
///     caller must therefore <c>Abort</c> on ANY non-success result from this type, never send a clean
///     failure reply.
/// </remarks>
public static class SkillLearnResolver
{
    /// <summary><c>MAX_SKILL_SLOT_NUM</c> (<c>aSkill[40]</c>, <c>CK_CharacterSkills_SlotIndex</c>).</summary>
    public const int MaxSlots = 40;

    /// <summary><c>NpcSkillOfferRowDto.ArrayKind</c> -- tSort 202, <c>nSkillInfo1</c>.</summary>
    public const byte SkillTree1 = 1;

    /// <summary><c>NpcSkillOfferRowDto.ArrayKind</c> -- tSort 233, <c>nSkillInfo2</c>.</summary>
    public const byte SkillTree2 = 2;

    public enum LearnFailure
    {
        None,
        NotOfferedByThisNpc,
        UnknownSkill,
        InsufficientSkillPoints,
        AlreadyLearned,
        NoFreeSlot
    }

    public readonly record struct LearnResult(bool Success, LearnFailure Failure, byte Slot, int Cost)
    {
        public static LearnResult Fail(LearnFailure failure)
        {
            return new LearnResult(false, failure, 0, 0);
        }
    }

    /// <summary>
    ///     <paramref name="arrayKind" /> selects which of the NPC's two offer arrays to search
    ///     (<see cref="SkillTree1" />/<see cref="SkillTree2" />) -- everything else is identical between
    ///     tSort 202 and 233. Order of checks mirrors the verified source EXACTLY (offered-by-this-NPC,
    ///     unknown-skill, insufficient-points, already-learned-anywhere, no-free-slot-for-this-skill-type).
    /// </summary>
    public static LearnResult ResolveLearn(
        ImmutableArray<NpcSkillOfferRowDto> npcSkillOffers,
        byte arrayKind,
        int requestedSkillId,
        SkillDefinition? skillDefinition,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        int currentSkillPoints)
    {
        var offered = false;
        foreach (var offer in npcSkillOffers)
            if (offer.ArrayKind == arrayKind && offer.SkillId == requestedSkillId)
            {
                offered = true;
                break;
            }

        if (!offered)
            return LearnResult.Fail(LearnFailure.NotOfferedByThisNpc);

        if (skillDefinition is not { } skill)
            return LearnResult.Fail(LearnFailure.UnknownSkill);

        if (currentSkillPoints < skill.Skill.LearnSkillPoint)
            return LearnResult.Fail(LearnFailure.InsufficientSkillPoints);

        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == requestedSkillId)
                return LearnResult.Fail(LearnFailure.AlreadyLearned);

        return TryFindFreeSlotForType(skill.Skill.Type, learnedSkills, out var slot)
            ? new LearnResult(true, LearnFailure.None, slot, skill.Skill.LearnSkillPoint)
            : LearnResult.Fail(LearnFailure.NoFreeSlot);
    }

    public enum UpgradeFailure
    {
        None,
        InvalidSlot,
        SlotEmpty,
        UnknownSkill,
        InsufficientSkillPoints,
        AlreadyMaxed
    }

    public readonly record struct UpgradeResult(bool Success, UpgradeFailure Failure, int NewGrade)
    {
        public static UpgradeResult Fail(UpgradeFailure failure)
        {
            return new UpgradeResult(false, failure, 0);
        }
    }

    /// <summary>tSort 203 -- verified: NO <c>CheckNPCFunction</c> call site exists for this action, unlike its 202/233 siblings.</summary>
    public static UpgradeResult ResolveUpgrade(int slot, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        SkillDefinition? skillDefinition, int currentSkillPoints)
    {
        if (slot is < 0 or >= MaxSlots)
            return UpgradeResult.Fail(UpgradeFailure.InvalidSlot);

        if (!learnedSkills.TryGetValue((byte)slot, out var learned))
            return UpgradeResult.Fail(UpgradeFailure.SlotEmpty);

        if (skillDefinition is not { } skill || skill.Skill.SkillId != learned.SkillId)
            return UpgradeResult.Fail(UpgradeFailure.UnknownSkill);

        if (currentSkillPoints < 1)
            return UpgradeResult.Fail(UpgradeFailure.InsufficientSkillPoints);

        if (learned.Grade >= skill.Skill.MaxUpgradePoint)
            return UpgradeResult.Fail(UpgradeFailure.AlreadyMaxed);

        return new UpgradeResult(true, UpgradeFailure.None, learned.Grade + 1);
    }

    /// <summary>
    ///     Per-<c>sType</c> slot ranges, verified IDENTICAL for both offer arrays: 1 -&gt; [0,10),
    ///     2 -&gt; [20,30), 3/4 -&gt; try [10,20) then [30,40) (S04_MyWork05.cpp:436-544/3409-3501). Returns
    ///     the FIRST empty slot found in range, matching the legacy's own linear scan; any other
    ///     <c>sType</c> value has no valid range (legacy <c>default: Quit()</c>).
    /// </summary>
    private static bool TryFindFreeSlotForType(byte skillType, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        out byte slot)
    {
        switch (skillType)
        {
            case 1:
                return TryFindEmptyInRange(learnedSkills, 0, 10, out slot);
            case 2:
                return TryFindEmptyInRange(learnedSkills, 20, 30, out slot);
            case 3:
            case 4:
                return TryFindEmptyInRange(learnedSkills, 10, 20, out slot) ||
                       TryFindEmptyInRange(learnedSkills, 30, 40, out slot);
            default:
                slot = 0;
                return false;
        }
    }

    private static bool TryFindEmptyInRange(IReadOnlyDictionary<byte, LearnedSkill> learnedSkills, int startInclusive,
        int endExclusive, out byte slot)
    {
        for (var i = startInclusive; i < endExclusive; i++)
            if (!learnedSkills.ContainsKey((byte)i))
            {
                slot = (byte)i;
                return true;
            }

        slot = 0;
        return false;
    }
}
