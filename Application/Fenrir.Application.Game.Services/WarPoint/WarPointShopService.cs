using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.WarPoint;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.WarPoint;

/// <summary>
///     Orchestrates the War-Point (WP/CP dual-currency) NPC-shop purchase (<c>USE_WAR_POINT_SYSTEM</c>,
///     <c>Server/ts25zone/S04_MyWork05.cpp:1798-1988</c>): pure resolution via <see cref="WarPointShopPolicy" />,
///     then the atomic War-Point debit + item grant via <see cref="IWarPointRepository" />, then the in-memory
///     mirrors, the audit row, and the balance-update packet(s).
/// </summary>
/// <remarks>
///     PACKET NOTE. Legacy <c>S905UPDATE_WAR_POINT</c> (=905) and <c>S003CONTRIBUTION_POINT</c> (=3) are NOT
///     distinct wire opcodes -- they are <c>Sort</c> codes carried inside the single avatar-stat-update opcode
///     (<c>Opcodes.Zone.Outgoing.AvatarStatUpdate</c> = 22), the same "S9xxUPDATE_..." family as
///     <c>S904UPDATE_HERO_POINT</c>. Fenrir already emits War-Point balance updates through
///     <see cref="AvatarStatUpdateResponse" /> (see <c>Zone.GrantWarPoints</c>); this service reuses that exact
///     carrier rather than minting a new packet, which would desync the wire (905 is application-level Sort data,
///     not an opcode). The War-Point balance in <see cref="AvatarStatUpdateResponse" /> is the authoritative
///     post-debit value returned by the SQL purchase, so the client is correct regardless of the in-memory
///     War-Point mirror's own (currently non-hydrated) state -- see this workstream's notes.
/// </remarks>
public sealed class WarPointShopService(
    IWarPointRepository warPoints,
    IEventLogRepository eventLog,
    WarPointShopCatalog catalog,
    WorldDataCache worldData,
    ILogger<WarPointShopService> logger) : IWarPointShopService
{
    /// <summary><c>S905UPDATE_WAR_POINT</c> (<c>STRUCT.h:1651</c>) -- Sort on <see cref="AvatarStatUpdateResponse" />.</summary>
    private const int WarPointBalanceUpdateSort = 905;

    /// <summary><c>S003CONTRIBUTION_POINT</c> (<c>STRUCT.h:1518</c>) -- Sort on <see cref="AvatarStatUpdateResponse" />.</summary>
    private const int ContributionPointBalanceUpdateSort = 3;

    /// <summary>
    ///     game.EventLog.EventCode for a War-Point purchase, scoped within
    ///     <see cref="EventLogCategory.NpcShopTrade" /> -- distinct from the ordinary-sell (1) and ordinary-buy
    ///     (2) codes <c>GenericActionService</c> already uses in the same category (EventCode is app-owned within
    ///     its Category, see that enum's own remarks).
    /// </summary>
    private const short WarPointShopBuyEventCode = 3;

    private const byte NpcShopTradeOutcome = 1;

    public async ValueTask<WarPointBuyServiceResult> TryBuyAsync(Zone zone, PlayerRuntimeState state, int accountId,
        int characterId, int npcId, int itemId, int requestedQuantity, byte destinationPage, byte destinationSlot,
        CancellationToken ct)
    {
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            return WarPointBuyServiceResult.NotHandled;

        var destination = state.Inventory.GetSlot(destinationPage, destinationSlot);
        var resolution = WarPointShopPolicy.ResolveBuy(catalog, npcId, itemDefinition, requestedQuantity, destination,
            state.ContributionPoints);

        switch (resolution.Outcome)
        {
            case WarPointShopPolicy.BuyOutcome.NotWarPointItem:
                return WarPointBuyServiceResult.NotHandled;

            case WarPointShopPolicy.BuyOutcome.WrongNpc:
            case WarPointShopPolicy.BuyOutcome.DestinationConflict:
                logger.LogInformation(
                    "Character {CharacterId} War-Point buy aborted: {Outcome} (NPC {NpcId}, item {ItemId})",
                    characterId, resolution.Outcome, npcId, itemId);
                return WarPointBuyServiceResult.Aborted;

            case WarPointShopPolicy.BuyOutcome.InsufficientContributionPoints:
                logger.LogInformation(
                    "Character {CharacterId} War-Point buy soft-rejected: insufficient Contribution Points (item {ItemId})",
                    characterId, itemId);
                return WarPointBuyServiceResult.SoftRejected;
        }

        // Proceed: the atomic SQL purchase re-checks War-Point affordability server-side.
        var projected = state.Inventory.GetContainer(destinationPage)
            .SetItem(destinationSlot, resolution.NewDestinationStack!.Value);

        WarPointPurchaseResult purchase;
        try
        {
            purchase = await warPoints.BuyWarPointItemAsync(characterId, resolution.WarPointCost, destinationPage,
                ToTvps(projected), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} War-Point buy BuyWarPointItemAsync failed (item {ItemId})", characterId,
                itemId);
            return WarPointBuyServiceResult.Aborted;
        }

        if (!purchase.Purchased)
        {
            // Server-authoritative insufficient-War-Point: soft reject, no state changed, no packet -- the
            // legacy explicitly does NOT disconnect for this (S04_MyWork05.cpp:1867-1873).
            logger.LogInformation(
                "Character {CharacterId} War-Point buy soft-rejected: insufficient War-Points (item {ItemId}, cost {WarPointCost})",
                characterId, itemId, resolution.WarPointCost);
            return WarPointBuyServiceResult.SoftRejected;
        }

        var newWarPoint = purchase.NewWarPointBalance;
        var beforeWarPoint = newWarPoint + resolution.WarPointCost;
        var purchasedQuantity = resolution.NewDestinationStack!.Value.Quantity - (destination?.Quantity ?? 0);

        // Logged only once the atomic War-Point debit + item grant durably committed. WP/CP before/after go into
        // Payload (no dedicated column carries them), same posture as this codebase's other balance audits.
        await eventLog.LogAsync(WarPointShopBuyEventCode, EventLogCategory.NpcShopTrade, accountId, characterId,
            null, null, null, null, null, itemId, purchasedQuantity, NpcShopTradeOutcome,
            $"WarPointBefore={beforeWarPoint};WarPointAfter={newWarPoint};WarPointCost={resolution.WarPointCost};CpCost={resolution.ContributionPointCost}",
            ct);

        // Mirror the granted item into PlayerRuntimeState (SQL is already durable; a dropped/timed-out mirror
        // self-heals on next world entry).
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(destinationPage, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null), ct))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped War-Point buy mirror for character {CharacterId} -- SQL is durable",
                zone.MapId, characterId);

        // Contribution-Point deduction: mirrored through the same tribe-progress write-behind channel the
        // ordinary NPC-buy path uses. Dead in the shipped build (every live catalogue row sets CP price 0), but
        // reproduced for fidelity to the dual-currency mechanism.
        var newContributionPoints = state.ContributionPoints;
        if (resolution.ContributionPointCost > 0)
        {
            newContributionPoints = state.ContributionPoints - resolution.ContributionPointCost;
            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(characterId, newContributionPoints), ct))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped War-Point CP mirror for character {CharacterId}",
                    zone.MapId, characterId);
        }

        // Balance-update packets (deductions above, then broadcasts): S905 always; S003 only when CP was charged.
        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = WarPointBalanceUpdateSort, Value = newWarPoint, Value2 = 0 });

        if (resolution.ContributionPointCost > 0)
            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = ContributionPointBalanceUpdateSort, Value = newContributionPoints, Value2 = 0 });

        logger.LogInformation(
            "Character {CharacterId} War-Point buy applied: item {ItemId} x{Quantity}, WP {Before}->{After}, CP -{CpCost}",
            characterId, itemId, purchasedQuantity, beforeWarPoint, newWarPoint, resolution.ContributionPointCost);

        return WarPointBuyServiceResult.Succeeded;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
