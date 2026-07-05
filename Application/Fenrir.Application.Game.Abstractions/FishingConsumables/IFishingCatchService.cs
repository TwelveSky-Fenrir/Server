using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;

namespace Fenrir.Application.Game.Abstractions.FishingConsumables;

/// <summary>
///     Business logic for <c>FishingCatchHandler</c> (CZ_FISHING_REWARD_SEND, opcode 105): rolls/grants the koi
///     item on step 4 (or mirrors an abort/failure), then always mirrors the current fishing state to the zone.
/// </summary>
/// <remarks>
///     Takes <paramref name="session"/ /* sic* /> and sends its own wire responses (rather than returning a result
///     for the Handler to translate) because the legacy interleaves each response send between the awaited
///     zone-mirror calls below -- the Zone's own tick can independently broadcast to this same session (the
///     action-sort broadcast) while an await is pending, so send order relative to those awaits is observable on
///     the wire and must be preserved exactly, not batched up and sent only once the method returns.
/// </remarks>
public interface IFishingCatchService
{
    public ValueTask ResolveAndApplyAsync(Zone zone, PlayerRuntimeState state, int characterId, IPacketSession session,
        CancellationToken cancellationToken);
}
