using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 Lucky Ticket family (world.Items 1035/1036/1037) -- a lottery box: rolls a random draw against
///     fixed per-item thresholds to pick a reward tier, then grants the matching reward item through a
///     box-opening helper on a hit.
///     <para>
///         TODO(C9-tickets-tower): the originating behavior contract does not supply the per-item draw
///         thresholds, the reward-tier item tables, or the family-specific serial number this family's own
///         box-opening helper stamps rewards with -- only that they exist. This project's policy forbids
///         inventing an id/magnitude for any of these, so this is a clearly-marked stub -- the same "reject
///         cleanly rather than guess" posture other still-incomplete reward-table stubs in this workstream
///         use: it rejects with a clean Result=1 reply and does NOT consume the ticket (never a silent success).
///         Wiring the real draw is a follow-up once a legacy-behavior-translator pass supplies the thresholds/
///         reward tables/serial number. Flagged in this workstream's openQuestions. World.Item 17124's own
///         sub-case is confirmed dead code (compiled only under a macro that is never absent in any real build
///         variant) and is deliberately NOT registered here at all.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3342-3481 (Lucky Ticket branch, including the dead 17124
///     sub-case).
/// </remarks>
public sealed class LuckyTicketUseItemHandler(ILogger<LuckyTicketUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 1035/1036/1037 -- world.Item 17124's own sub-case is dead code, deliberately excluded.</summary>
    public static IEnumerable<int> HandledItemIds { get; } = [1035, 1036, 1037];

    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Character {CharacterId} used Lucky Ticket ({ItemId}) but the draw thresholds/reward tables/serial number are not yet cataloged: ticket left unconsumed, result 1 (see LuckyTicketUseItemHandler TODO)",
            context.CharacterId, context.Item.ItemId);
        return ValueTask.FromResult(UseItemResponses.Fail(context.Page, context.Index));
    }
}
