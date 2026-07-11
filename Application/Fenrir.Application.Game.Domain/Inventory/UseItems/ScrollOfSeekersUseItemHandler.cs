using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 "Scroll of Seekers" family (world.Items 1124/1187/7016/8409/8410) -- adds a fixed per-item amount
///     to the character's shared <see cref="PlayerRuntimeState.ScrollOfSeekersTime" /> counter (180 for
///     1124/8409, 900 for 1187/7016/8410, per <see cref="ScrollOfSeekersResolver.AmountFor" />), ceiling-checked
///     via <see cref="ScrollOfSeekersResolver" />, then consumes exactly one unit -- never bulk-aware, unlike
///     the neighboring CP-Ticket family in this same op23 slice. Unlike its Elite-Dungeon/Dungeon-Key/Ivy-Hall
///     siblings in this file, the success response never echoes the new counter total: the cited legacy branch
///     never writes to <c>r-&gt;tValue</c> for this family (see <see cref="UseItemResponses.Success" />'s
///     <c>value</c> parameter, left at its default 0 here).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3847-3867 (Scroll of Seekers branch: the outer five-id
///     dispatch, the 180 default/900 override split, the counter add, the cash-item-use log, and the
///     single-unit, non-bulk-aware consume with no <c>r-&gt;tValue</c> echo -- confirmed this session per the
///     recovered <c>scroll-of-seekers-per-id-split</c> contract; see <see cref="ScrollOfSeekersResolver" /> for
///     the full citation set, including the overflow guard and the <c>WUSE_ITEM_1124</c> live-build check).
///     Every rejection collapses to a clean Result=1 reply rather than a disconnect, the same op23
///     simplification this file's sibling handlers already use.
/// </remarks>
public sealed class ScrollOfSeekersUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<ScrollOfSeekersUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 1124/1187/7016/8409/8410.</summary>
    public static IEnumerable<int> HandledItemIds => ScrollOfSeekersResolver.HandledItemIds;

    /// <summary>
    ///     game.EventLog.EventCode for a Scroll-of-Seekers counter grant -- scoped within
    ///     <see cref="EventLogCategory.ItemUse" />, distinct from <see cref="EliteDungeonTicketUseItemHandler" />'s
    ///     30, <see cref="DungeonKeyUseItemHandler" />'s 31, and <see cref="IvyHallTicketUseItemHandler" />'s 32
    ///     in the same category.
    /// </summary>
    private const short ScrollOfSeekersGrantEventCode = 33;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var resolved = ScrollOfSeekersResolver.Resolve(context.Item.ItemId, state.ScrollOfSeekersTime);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Scroll-of-Seekers ({ItemId}) rejected: {Outcome}",
                context.CharacterId, context.Item.ItemId, resolved.Outcome);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(ScrollOfSeekersGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, resolved.CreditedAmount,
            SuccessOutcome, $"ScrollOfSeekersTime={state.ScrollOfSeekersTime}->{resolved.NewZoneTime}",
            cancellationToken);

        // Not bulk-aware: exactly one unit is consumed regardless of the client-supplied Value/bulk count,
        // matching the cited branch's own asymmetry with the neighboring bulk-capable item 828/837 case.
        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, ScrollOfSeekersTime: resolved.NewZoneTime),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Scroll-of-Seekers mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Scroll-of-Seekers ({ItemId}) applied: ScrollOfSeekersTime {Old}->{New}",
            context.CharacterId, context.Item.ItemId, state.ScrollOfSeekersTime, resolved.NewZoneTime);

        // No Value echo for this family -- the cited legacy branch never writes r->tValue for it.
        return UseItemResponses.Success(context.Page, context.Index);
    }
}
