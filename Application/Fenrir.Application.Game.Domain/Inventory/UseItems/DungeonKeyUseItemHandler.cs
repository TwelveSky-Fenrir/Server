using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 item 1048 (a dungeon-access key, distinct from Taiyan Key/1049) -- adds a fixed 1 to the
///     character's own <see cref="PlayerRuntimeState.DungeonKeyTime" /> counter, ceiling-checked via
///     <see cref="DungeonAccessTicketResolver" />, then consumes exactly one unit.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3696-3726 (item 1048 branch: the fixed +1 amount, the
///     before/after log, the single-unit consume). Every rejection collapses to a clean Result=1 reply rather
///     than a disconnect, same op23 simplification as this file's sibling handlers.
///     <para>
///         KNOWN, DELIBERATE GAP -- no level precondition is enforced, for the same reason
///         <see cref="EliteDungeonTicketUseItemHandler" />'s own remarks document (the originating contract
///         states a per-item level threshold exists for this id but does not supply its concrete value).
///         Flagged in this workstream's openQuestions.
///     </para>
/// </remarks>
public sealed class DungeonKeyUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<DungeonKeyUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 1048.</summary>
    public const int ItemId = DungeonAccessTicketResolver.DungeonKeyItemId;

    /// <summary>
    ///     game.EventLog.EventCode for a Dungeon-Key counter grant -- scoped within
    ///     <see cref="EventLogCategory.ItemUse" />, distinct from
    ///     <see cref="EliteDungeonTicketUseItemHandler" />'s own 30 in the same category.
    /// </summary>
    private const short DungeonKeyGrantEventCode = 31;

    private const byte SuccessOutcome = 1;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var resolved = DungeonAccessTicketResolver.Resolve(state.DungeonKeyTime,
            DungeonAccessTicketResolver.DungeonKeyAmount, context.Item.Quantity);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Dungeon-Key (1048) rejected: {Outcome}, quantity {Quantity}",
                context.CharacterId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(DungeonKeyGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"DungeonKeyTime={state.DungeonKeyTime}->{resolved.NewCounterValue}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, DungeonKeyTime: resolved.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Dungeon-Key mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Dungeon-Key (1048) applied: DungeonKeyTime {Old}->{New}",
            context.CharacterId, state.DungeonKeyTime, resolved.NewCounterValue);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewCounterValue);
    }
}
