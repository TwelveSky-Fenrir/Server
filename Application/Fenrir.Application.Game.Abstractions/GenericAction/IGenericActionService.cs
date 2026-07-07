using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.GenericAction;

/// <summary>Outcome discriminator for a <see cref="GenericActionService" /> operation.</summary>
public enum GenericActionStatus
{
    /// <summary>Malformed/impossible input -- the caller should abort the session (anti-fuzzing).</summary>
    Aborted,

    /// <summary>A well-formed request the domain rules cleanly rejected -- the caller replies with a failure code.</summary>
    Failed,

    /// <summary>The action was applied -- the caller replies with a success code.</summary>
    Succeeded
}

/// <summary>
///     Result of a <see cref="GenericActionService" /> operation. <see cref="NotifyQuestProgress" /> is only ever
///     set on a successful ground-item pickup that also happens to satisfy the character's active qSort-2 quest.
///     <see cref="GrantedPetExperienceGrowth" /> is only ever set by
///     <see cref="IGenericActionService.TimeExchangeAsync" />
///     when its pet-experience credit was actually positive -- the caller sends the self-addressed
///     <c>AvatarStatUpdateResponse</c> (Sort=14, S014PET_EXP) carrying this value, same "service computes,
///     handler sends" split <see cref="NotifyQuestProgress" /> already established.
/// </summary>
public readonly record struct GenericActionResult(
    GenericActionStatus Status,
    bool NotifyQuestProgress = false,
    int? GrantedPetExperienceGrowth = null)
{
    public static readonly GenericActionResult Aborted = new(GenericActionStatus.Aborted);
    public static readonly GenericActionResult Failed = new(GenericActionStatus.Failed);
    public static readonly GenericActionResult Succeeded = new(GenericActionStatus.Succeeded);
}

/// <summary>
///     Business logic behind <see cref="GenericActionHandler" />'s tSort-dispatched actions: container moves,
///     ground pickup, NPC teleport toll, skill learn/upgrade, and NPC shop buy/sell.
/// </summary>
public interface IGenericActionService
{
    public ValueTask<GenericActionResult> MoveContainerAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> PickupGroundItemAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> PayTeleportTollAsync(byte[] data, int characterId,
        CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> LearnSkillAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> UpgradeSkillAsync(byte[] data, Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken);

    /// <param name="accountId">
    ///     The acting player's account id -- carried only for the game.EventLog audit row written once the
    ///     sale has durably persisted (Category=NpcShopTrade); not used for any validation or persistence
    ///     decision.
    /// </param>
    public ValueTask<GenericActionResult> SellToNpcShopAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, DefaultPData move, CancellationToken cancellationToken);

    /// <param name="accountId">
    ///     The acting player's account id -- carried only for the game.EventLog audit row written once the
    ///     purchase has durably persisted (Category=NpcShopTrade); not used for any validation or persistence
    ///     decision.
    /// </param>
    public ValueTask<GenericActionResult> BuyFromNpcShopAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, DefaultPData move, CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 206 -- spend unspent stat points (aStatPoint) to raise Strength/Dexterity/Vitality/Intelligence.
    ///     Not yet reachable from <c>GenericActionHandler</c>'s dispatch switch; wiring it up (including reading
    ///     <c>tStatSort</c>/<c>tAddValue</c> off the wire payload) is a separate integration step.
    /// </summary>
    /// <param name="statSort">tStatSort, the wire category code -- legal range 1-12 (see StatAllocationResolver).</param>
    /// <param name="addValue">tAddValue, only meaningful for category codes 9-12.</param>
    public ValueTask<GenericActionResult> AllocateStatPointAsync(int statSort, int addValue, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken);

    /// <summary>
    ///     tSort 237 -- TimeExchange: converts every accrued play-time-event minute
    ///     (<see cref="PlayerRuntimeState.PlayTimeEvent" />, itself produced by
    ///     <c>PlayTimeAccrualSystem</c>'s per-real-minute tick) into 694 teacher points and 400 pet experience
    ///     per minute, then resets the accrued counter to 0. A no-op (still a success echo, per the source
    ///     contract) when fewer than 1 minute has accrued -- no precondition beyond that guard. The
    ///     pet-experience portion silently drops if no valid pet is equipped; the teacher-point portion still
    ///     grants normally either way (not an atomic pair).
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork04.cpp:916-920 (dispatch case) ;
    ///     Server/ts25zone/S04_MyWork05.cpp:4808-4826 (guard, formulas, counter reset, audit log ordering,
    ///     reward grants) ; Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:1920-1971 (the general
    ///     pet-experience-grant routine, ported here via <c>PetExperienceCreditResolver</c> -- its
    ///     reactivation/tier-crossing ability-recalculation broadcast is NOT sent here, the same documented
    ///     gap <c>Zone.CreditPetGrowthFromMonsterKill</c>'s own remarks already carry for the same routine).
    /// </remarks>
    public ValueTask<GenericActionResult> TimeExchangeAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, CancellationToken cancellationToken);
}
