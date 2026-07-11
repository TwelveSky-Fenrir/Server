using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 "Scroll of Seekers" family (world.Items 1124/1187/7016/8409/8410) -- adds 180 (or 900 for a
///     documented subset) to a banked zone-time counter.
///     <para>
///         TODO(C9-tickets-tower): the originating behavior contract confirms the family exists and that the
///         per-use amount is 180 for most ids but 900 for "a documented subset," WITHOUT stating which
///         specific ids fall in that 900 subset -- carried forward, not independently re-opened, from the
///         source finding. Applying 180 to every id here would risk silently under- or over-crediting whichever
///         ids actually belong to the 900 subset (the exact "guessing which id maps to which mechanic would
///         risk a silent wrong-amount/wrong-counter bug" concern <see cref="Consumables.CashTimerResolver" />'s
///         own remarks already flag for this identical situation). This project's policy forbids inventing
///         that split, so this is a clearly-marked stub -- the same "reject cleanly rather than guess" posture
///         other still-incomplete reward-table stubs in this workstream use: it rejects with a clean Result=1
///         reply and does NOT consume
///         the scroll. Flagged in this workstream's openQuestions for a follow-up legacy-behavior-translator
///         pass supplying the exact per-id amount table.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3847-3867 (Scroll of Seekers branch, carried forward from
///     the originating contract, not independently re-opened this session).
/// </remarks>
public sealed class ScrollOfSeekersUseItemHandler(ILogger<ScrollOfSeekersUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 1124/1187/7016/8409/8410.</summary>
    public static IEnumerable<int> HandledItemIds { get; } = [1124, 1187, 7016, 8409, 8410];

    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Character {CharacterId} used Scroll of Seekers ({ItemId}) but the 180-vs-900 per-id amount split is not yet cataloged: scroll left unconsumed, result 1 (see ScrollOfSeekersUseItemHandler TODO)",
            context.CharacterId, context.Item.ItemId);
        return ValueTask.FromResult(UseItemResponses.Fail(context.Page, context.Index));
    }
}
