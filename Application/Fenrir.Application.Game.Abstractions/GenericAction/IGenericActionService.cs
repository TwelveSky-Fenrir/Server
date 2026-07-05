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
/// </summary>
public readonly record struct GenericActionResult(GenericActionStatus Status, bool NotifyQuestProgress = false)
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

    public ValueTask<GenericActionResult> SellToNpcShopAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);

    public ValueTask<GenericActionResult> BuyFromNpcShopAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken);
}
