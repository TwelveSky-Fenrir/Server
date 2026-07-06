using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Port of <c>PETSYSTEM::ProcessForExperience</c> (GameSystem_07_Pet.cpp:1920-1971) for the monster-kill
///     call shape (<c>tSendPacket</c> always true at that call site -- see
///     <c>Server/ts25zone/H08_MyGameSystem.h:212</c>'s default and <c>S07_MyGame03.cpp:320</c>'s omitted
///     argument). Pure: takes the avatar's current pet-slot state and the already-computed pet-experience
///     amount, returns what changed and which notifications the caller should send -- performs no mutation
///     and no I/O itself.
/// </summary>
/// <remarks>
///     The pet-experience amount fed into <see cref="Resolve" /> is expected to already be the monster's
///     <c>mPatExperience</c> scaled by the global/personal pet-experience-rate settings and the
///     double-time/premium-status multipliers (<c>MONSTER_OBJECT::ProcessForExp</c>,
///     S07_MyGame05.cpp:3855-3863) -- that scaling chain has no concrete rate/threshold values in the source
///     contract and depends on buff/premium state not yet modeled on <c>PlayerRuntimeState</c>, so it is out
///     of scope here; callers should pass the monster's raw <c>PatExperience</c> value until it exists.
/// </remarks>
public static class PetExperienceCreditResolver
{
    /// <summary>Legacy <c>IPET</c> (Server/Header/Protocol/STRUCT.h:1696) -- item category checked against the equipped item.</summary>
    private const byte PetItemCategory = 22;

    /// <summary>
    ///     <paramref name="requestedPetExperience" /> should already be gated positive by the caller (the
    ///     shared crediting routine only ever dispatches here when it is, S07_MyGame03.cpp:318) -- passing a
    ///     non-positive amount here simply credits 0 through the same cap logic
    ///     <see cref="PetExperienceCreditCalculator" /> already applies.
    /// </summary>
    public static PetExperienceCreditResult Resolve(
        int petItemId,
        int currentGrowth,
        int currentActivity,
        int requestedPetExperience,
        FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (petItemId == 0 || !itemsById.TryGetValue(petItemId, out var definition) ||
            definition.Item.Sort != PetItemCategory)
            return PetExperienceCreditResult.Ineligible;

        var reactivationApplied = currentActivity < 1;
        var newActivity = reactivationApplied ? 1 : currentActivity;

        var creditedAmount =
            PetExperienceCreditCalculator.ComputeCreditedAmount(petItemId, currentGrowth, requestedPetExperience);
        var newGrowth = currentGrowth + creditedAmount;

        var tierIncreased = creditedAmount > 0 &&
                            PetGrowthTierCalculator.HasTierIncreased(petItemId, currentGrowth, newGrowth);

        return new PetExperienceCreditResult(true, reactivationApplied, newActivity, creditedAmount, newGrowth,
            tierIncreased);
    }
}
