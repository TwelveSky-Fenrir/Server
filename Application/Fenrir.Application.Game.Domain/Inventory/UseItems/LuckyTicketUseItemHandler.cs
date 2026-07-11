using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 Lucky Ticket family (world.Items 1035/1036/1037) -- a lottery box: draws a single roll against
///     fixed per-item thresholds to pick a rarity tier (gated further by character level and, for the top
///     tier, an operator-configurable deployment-stage flag), resolves one matching equipment reward from the
///     general item catalog, and grants it tagged with a fixed per-ticket-family serial.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:3342-3481 (the whole Lucky Ticket case block). A textually
///     adjacent sub-case for item 17124 in the same legacy switch is gated <c>#ifndef LNW33</c>, which is
///     never true in the only real buildable configuration (<c>LNW33</c> is always active there) -- confirmed
///     dead code, deliberately not ported here at all (not even as an unreachable branch).
///     <para>
///         All roll/tier/level-window/serial decisions live in the pure <see cref="LuckyTicketRewardResolver" />;
///         this handler owns only resolving the reward's world.Items row and delegating placement/consumption
///         to the SAME shared primitives every other op23 box family already uses --
///         <see cref="LootBoxOpenResolver.OpenSingle" /> (roll, quantity clamp, stackable strip, merge-vs-
///         empty-slot placement, ticket consumption, all folded into one projected-pages plan) via a
///         reward-id override (the draw itself) and a reward-serial override (the fixed family tag) -- the
///         same two-hook shape <c>LootBoxUseItemHandler</c> already uses for its own previous-tribe/pity-gated
///         boxes whose reward id cannot come from a plain <see cref="BoxRewardSpec" />. The projected pages are
///         then persisted via <see cref="UseItemInventoryWriter.ReplaceProjectedPagesAndMirrorAsync" />, the
///         same "write only what changed, atomically when both pages changed" tail <c>LootBoxUseItemHandler</c>
///         uses for every other box.
///     </para>
///     <para>
///         NOT wired here (out of scope of the lucky-ticket-handler-thresholds contract this handler
///         implements, and deliberately not invented): a separate, earlier-recovered citation
///         (<c>Server/ts25zone/S04_MyWork05.cpp:5408-5428</c>, see <see cref="NoticeForBoxResolver" />'s
///         own remarks) documents a distinct "elite-typed reward" gain-audit entry specific to 1035/1036/1037,
///         and <see cref="LootBoxCatalog.EliteOnlyNoticeBoxIds" /> already lists these three ids for exactly
///         that purpose -- but that whole mechanism is only ever invoked from
///         <c>LootBoxUseItemHandler.AttemptNoticeAsync</c>, which this handler does not call (Lucky Ticket is
///         its own <see cref="IUseItemHandler" />, not dispatched through <c>LootBoxUseItemHandler</c>). This is
///         a known, currently-orphaned piece of scaffolding, not something this contract resolves -- a future
///         pass should either thread an equivalent notice/audit call through this handler or fold Lucky Ticket
///         into the shared box-handler dispatch outright.
///     </para>
/// </remarks>
public sealed class LuckyTicketUseItemHandler(
    WorldDataCache worldData,
    UseItemInventoryWriter inventoryWriter,
    ILogger<LuckyTicketUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 1035/1036/1037 -- world.Item 17124's own sub-case is dead code, deliberately excluded.</summary>
    public static IEnumerable<int> HandledItemIds { get; } = [1035, 1036, 1037];

    /// <summary>
    ///     Placeholder spec: the reward id always comes from the reward-id override below (the actual draw),
    ///     never from this deliberately empty/unreachable uniform pool -- the same shape
    ///     <c>LootBoxUseItemHandler</c>'s own previous-tribe-keyed placeholder specs use. No rental applies to
    ///     any Lucky Ticket reward (not cited anywhere in the behavior contract).
    /// </summary>
    private static readonly BoxRewardSpec PlaceholderSpec = BoxRewardSpec.Uniform(0, ImmutableArray<int>.Empty);

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var ticketItemId = context.Item.ItemId;

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        // C1-vault-expiry-enforcement: the reward-placement search must not consider an expired dated-vault
        // last page a valid merge/empty-slot target -- same gate every other box-opening handler applies.
        var today = GameDate.Today();
        var secondPageAccessible = state.InventoryDate >= today;

        var plan = LootBoxOpenResolver.OpenSingle(PlaceholderSpec, context.Page, context.Index, context.Item,
            page0, page1, ResolveRewardSort, Random.Shared, today,
            () => DrawReward(ticketItemId, state),
            secondPageAccessible,
            _ => LuckyTicketRewardResolver.ResolveFamilySerial(ticketItemId));

        switch (plan.Outcome)
        {
            case LootBoxOpenResolver.Outcome.RewardNotFound:
                logger.LogDebug(
                    "Character {CharacterId} op23 Lucky Ticket ({TicketId}) drew no eligible reward within budget: ticket kept",
                    context.CharacterId, ticketItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case LootBoxOpenResolver.Outcome.InventoryFull:
                logger.LogDebug(
                    "Character {CharacterId} op23 Lucky Ticket ({TicketId}) reward {RewardId} could not be placed (inventory full): ticket kept",
                    context.CharacterId, ticketItemId, plan.RewardItemId);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
        }

        await inventoryWriter.ReplaceProjectedPagesAndMirrorAsync(context.Zone, context.CharacterId, page0, page1,
            plan.ProjectedPage0, plan.ProjectedPage1, null, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} op23 Lucky Ticket ({TicketId}) opened: reward {RewardId} -> {Container}:{Slot} ({Placement})",
            context.CharacterId, ticketItemId, plan.RewardItemId, plan.RewardContainer, plan.RewardSlot,
            plan.PlacementOutcome);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    /// <summary>
    ///     The Lucky Ticket draw itself (<see cref="LuckyTicketRewardResolver.TryDraw" />, using the currently-
    ///     shipped production deployment-stage stance -- see
    ///     <see cref="LuckyTicketRewardResolver.ShippedProductionEliteTierEnabled" />'s own remarks). A 0
    ///     return means "no eligible reward found after every retry," which
    ///     <see cref="LootBoxOpenResolver.OpenSingle" />'s own catalog lookup rejects as
    ///     <see cref="LootBoxOpenResolver.Outcome.RewardNotFound" /> (ticket kept, nothing granted) -- the same
    ///     generic failure signal legacy gives for this same exhausted-retry case.
    /// </summary>
    private int DrawReward(int ticketItemId, PlayerRuntimeState state)
    {
        return LuckyTicketRewardResolver.TryDraw(worldData, Random.Shared, ticketItemId, state.PreviousTribe,
            state.Level, state.Level2, LuckyTicketRewardResolver.ShippedProductionEliteTierEnabled,
            out var rewardItemId)
            ? rewardItemId
            : 0;
    }

    private byte? ResolveRewardSort(int rewardItemId)
    {
        return worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Sort : null;
    }
}
