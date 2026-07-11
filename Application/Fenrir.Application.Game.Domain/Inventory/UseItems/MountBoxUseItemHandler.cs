using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 item 635 — the "15% Mount Box", one of the two inlined pre-switch direct handlers in the legacy
///     dispatcher. On use it picks one of EIGHT mount rewards uniformly at random, grants it to inventory,
///     writes a gamelog entry, then consumes exactly one box and refreshes both the source and reward slots
///     (inventory full while placing the reward → result 3, and the box is NOT consumed).
///     <para>
///         TODO(C9): the eight mount reward item ids are not recoverable from the behavior contract handed to
///         this workstream, and this project's policy forbids inventing item ids. Until a legacy finding
///         supplies the eight ids, this handler cannot grant a reward, so it is a clearly-marked stub that
///         rejects with a clean result-1 reply and does NOT consume the box (never a silent success). Wiring
///         the grant is a one-method fill-in once the ids arrive: replace the body with the random-pick /
///         grant-to-first-free-slot / result-3-on-full / consume-one flow the contract already describes.
///         Flagged in openQuestions.
///     </para>
/// </summary>
/// <remarks>Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:1941-2024 (the inlined item-635 direct handler).</remarks>
public sealed class MountBoxUseItemHandler(ILogger<MountBoxUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 635 — the "15% Mount Box".</summary>
    public const int ItemId = 635;

    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Character {CharacterId} used mount box (635) but the eight mount reward ids are not yet cataloged: box left unconsumed, result 1 (see MountBoxUseItemHandler TODO)",
            context.CharacterId);
        return ValueTask.FromResult(UseItemResponses.Fail(context.Page, context.Index));
    }
}
