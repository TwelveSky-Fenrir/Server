using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 Ivy Hall Ticket pair (world.Items 553 Small/1219 Large) -- adds a fixed per-item amount (180/360)
///     to the character's shared <see cref="PlayerRuntimeState.IvyHallTicketTime" /> counter, then consumes
///     exactly one unit. Unlike the Elite Dungeon Ticket/Dungeon Key siblings in this same op23 slice, this
///     family is NOT listed among the level-gated ids in the originating C9-tickets-tower contract, so no
///     level precondition is modeled or omitted here -- there is none to model.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:6787-6807 (Ivy Hall Ticket branch: the fixed 180/360
///     amounts, carried forward from the originating contract, not independently re-opened this session).
///     Every rejection collapses to a clean Result=1 reply rather than a disconnect, same op23 simplification
///     as this file's sibling handlers.
///     <para>
///         CEILING PLACEHOLDER -- the originating contract states this counter is checked against "a distinct,
///         harder [lower] numeric cap than the generic one" but does not supply the concrete value. This
///         handler uses <see cref="DungeonAccessTicketResolver" />'s default (the shared 2,000,000,000
///         ceiling, <see cref="BankedCounterMath.GlobalCeiling" />) as a documented, conservative interim
///         placeholder rather than an invented tighter number -- this permits banking MORE than legacy's real,
///         tighter cap would allow, a real (if low-severity, non-currency) parity gap flagged in this
///         workstream's openQuestions pending a follow-up contract supplying the real cap.
///     </para>
/// </remarks>
public sealed class IvyHallTicketUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<IvyHallTicketUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>
    ///     game.EventLog.EventCode for an Ivy-Hall-Ticket counter grant -- scoped within
    ///     <see cref="EventLogCategory.ItemUse" />, distinct from
    ///     <see cref="EliteDungeonTicketUseItemHandler" />'s 30 and <see cref="DungeonKeyUseItemHandler" />'s
    ///     31 in the same category.
    /// </summary>
    private const short IvyHallTicketGrantEventCode = 32;

    private const byte SuccessOutcome = 1;

    public static IEnumerable<int> HandledItemIds { get; } =
    [
        DungeonAccessTicketResolver.IvyHallTicketSmallItemId,
        DungeonAccessTicketResolver.IvyHallTicketLargeItemId
    ];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var amount = AmountFor(context.Item.ItemId);
        var resolved = DungeonAccessTicketResolver.Resolve(state.IvyHallTicketTime, amount, context.Item.Quantity);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Ivy-Hall-Ticket ({ItemId}) rejected: {Outcome}, quantity {Quantity}",
                context.CharacterId, context.Item.ItemId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(IvyHallTicketGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"IvyHallTicketTime={state.IvyHallTicketTime}->{resolved.NewCounterValue}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, IvyHallTicketTime: resolved.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Ivy-Hall-Ticket mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Ivy-Hall-Ticket ({ItemId}) applied: IvyHallTicketTime {Old}->{New}",
            context.CharacterId, context.Item.ItemId, state.IvyHallTicketTime, resolved.NewCounterValue);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewCounterValue);
    }

    private static int AmountFor(int itemId)
    {
        return itemId switch
        {
            DungeonAccessTicketResolver.IvyHallTicketSmallItemId => DungeonAccessTicketResolver
                .IvyHallTicketSmallAmount,
            DungeonAccessTicketResolver.IvyHallTicketLargeItemId => DungeonAccessTicketResolver
                .IvyHallTicketLargeAmount,
            _ => 0
        };
    }
}
