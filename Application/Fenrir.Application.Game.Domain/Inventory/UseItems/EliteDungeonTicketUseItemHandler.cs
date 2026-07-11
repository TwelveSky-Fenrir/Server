using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 Elite Dungeon Ticket family (world.Items 1047 Large/1097 Medium/1098 Small) -- adds a fixed
///     per-item amount (180/120/60) to the character's shared <see cref="PlayerRuntimeState.EliteDungeonTime" />
///     counter, ceiling-checked via <see cref="DungeonAccessTicketResolver" />, then consumes exactly one unit.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3672-3694 (Elite Dungeon Ticket branch: the fixed 180/120/60
///     amounts, the counter add, the before/after log, the single-unit consume). Every rejection collapses to
///     a clean Result=1 reply rather than a disconnect, the same op23 simplification
///     <see cref="TitleUpgradeUseItemHandler" />/<see cref="PalaceRankUpgradeUseItemHandler" /> already use.
///     <para>
///         KNOWN, DELIBERATE GAP -- no level precondition is enforced. The originating C9-tickets-tower
///         contract states items 1047/1097/1098 each require the character's level to already meet "a
///         distinct per-item threshold" (disconnect-worthy in the legacy source) but does not supply the
///         concrete value for any of the three ids -- see <see cref="DungeonAccessTicketResolver" />'s own
///         remarks for why this project's policy against inventing a legacy magnitude from analogy alone
///         means that gate is intentionally left unmodeled here rather than guessed. Flagged in this
///         workstream's openQuestions for a follow-up legacy-behavior-translator pass.
///     </para>
/// </remarks>
public sealed class EliteDungeonTicketUseItemHandler(
    UseItemInventoryWriter inventoryWriter,
    IEventLogRepository eventLog,
    ILogger<EliteDungeonTicketUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>
    ///     game.EventLog.EventCode for an Elite-Dungeon-Ticket counter grant -- an app-owned value scoped
    ///     within <see cref="EventLogCategory.ItemUse" />, distinct from that category's existing skill-grimoire
    ///     (2/3), teleport-scroll (1), stat-potion (4/10-21), and loot-box (5/6) codes.
    /// </summary>
    private const short EliteDungeonTicketGrantEventCode = 30;

    private const byte SuccessOutcome = 1;

    public static IEnumerable<int> HandledItemIds { get; } =
    [
        DungeonAccessTicketResolver.EliteDungeonTicketLargeItemId,
        DungeonAccessTicketResolver.EliteDungeonTicketMediumItemId,
        DungeonAccessTicketResolver.EliteDungeonTicketSmallItemId
    ];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var amount = AmountFor(context.Item.ItemId);
        var resolved = DungeonAccessTicketResolver.Resolve(state.EliteDungeonTime, amount, context.Item.Quantity);

        if (!resolved.Succeeded)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 Elite-Dungeon-Ticket ({ItemId}) rejected: {Outcome}, quantity {Quantity}",
                context.CharacterId, context.Item.ItemId, resolved.Outcome, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        await eventLog.LogAsync(EliteDungeonTicketGrantEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"EliteDungeonTime={state.EliteDungeonTime}->{resolved.NewCounterValue}",
            cancellationToken);

        var remaining = context.Item.Quantity - 1;
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, EliteDungeonTime: resolved.NewCounterValue),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 Elite-Dungeon-Ticket mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 Elite-Dungeon-Ticket ({ItemId}) applied: EliteDungeonTime {Old}->{New}",
            context.CharacterId, context.Item.ItemId, state.EliteDungeonTime, resolved.NewCounterValue);

        return UseItemResponses.Success(context.Page, context.Index, resolved.NewCounterValue);
    }

    private static int AmountFor(int itemId)
    {
        return itemId switch
        {
            DungeonAccessTicketResolver.EliteDungeonTicketLargeItemId => DungeonAccessTicketResolver
                .EliteDungeonTicketLargeAmount,
            DungeonAccessTicketResolver.EliteDungeonTicketMediumItemId => DungeonAccessTicketResolver
                .EliteDungeonTicketMediumAmount,
            DungeonAccessTicketResolver.EliteDungeonTicketSmallItemId => DungeonAccessTicketResolver
                .EliteDungeonTicketSmallAmount,
            _ => 0
        };
    }
}
