namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     One surviving entry in a Zone175-type boss's per-acquisition-tick aggro list (legacy's fixed 50-slot
///     <c>SelectAvatarIndexForPossibleAttackForZone175TypeBoss</c> candidate array,
///     <c>Server/ts25zone/S07_MyGame05.cpp:218-265</c>). A value type so filling the scratch list costs no
///     per-candidate heap allocation on the boss decision path.
/// </summary>
/// <param name="CharacterId">The candidate avatar's character id.</param>
/// <param name="UniqueNumber">Its wire unique number (paired with the id when a target is committed).</param>
/// <param name="DistanceSquared">
///     Squared HORIZONTAL (X/Z, no vertical component) distance from the boss to this candidate at acquisition
///     time -- the "recorded engagement distance" the far/near branch re-tests against the squared melee radius
///     (contract's Zone175 branch + its squared-horizontal distance-test note).
/// </param>
/// <param name="PosX">Candidate position at acquisition time -- committed as the boss's target location.</param>
/// <param name="PosY">See <paramref name="PosX" />.</param>
/// <param name="PosZ">See <paramref name="PosX" />.</param>
public readonly record struct MonsterAggroCandidate(
    int CharacterId,
    uint UniqueNumber,
    float DistanceSquared,
    float PosX,
    float PosY,
    float PosZ);
