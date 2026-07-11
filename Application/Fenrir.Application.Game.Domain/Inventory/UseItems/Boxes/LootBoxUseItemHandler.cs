using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

/// <summary>
///     The op23 loot-box / chest open handler (workstream C10): one <see cref="IUseItemHandler" /> registered
///     under every box id <see cref="LootBoxCatalog" /> populates. On use it rolls a reward from that box's
///     table, clamps the reward quantity by sort, places it (merging or into a free slot), consumes exactly one
///     box, persists the reward placement and the box consumption as one atomic write, mirrors both onto the
///     zone, writes the legacy "double GL_606" before/after audit pair, and -- for a whitelisted reward -- would
///     fire the cluster-wide elite notice. A shift+right-click request (value &gt; 1 on a bulk-whitelisted box)
///     opens up to N boxes one at a time, stopping the moment an open cannot make progress.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:1855-2030 (the op23 box dispatch, the bulk call site) ;
///     :1783-1854 (<c>BulkOpenBoxNoStellar</c>) ; Server/ts25zone/S04_MyWork05.cpp:5150-5431
///     (<c>SetItemQuantity</c>, <c>MyWork__SetBoxChangeInventoryItem99</c>, the double GL_606 pair) ; :5080-5147
///     (<c>NoticeForBox</c>). All roll/placement/quantity decisions live in the pure
///     <see cref="LootBoxOpenResolver" /> / <see cref="BoxRewardPlacementResolver" /> /
///     <see cref="NoticeForBoxResolver" />; this handler owns only the durable write, the mirror, and the audit.
///     <para>
///         Reward+box atomicity is real: on success both the reward slot and the (decremented) box slot are
///         persisted in ONE call -- a single <see cref="ICharacterRepository.ReplaceContainerAsync" /> when both
///         land on the same inventory page, or <see cref="ICharacterRepository.ReplaceTwoContainersAsync" /> when
///         they straddle the two pages -- so a reward is never granted without exactly one box consumed and
///         never the reverse. The rare-tier cluster-notice BROADCAST is deferred (log only): there is no in-scope
///         cross-shard center-relay seam here, the same posture the log-only <c>CenterRelayNoticeLog</c> and
///         <c>Zone.AnnounceEliteBossDefeated</c> already take. As of workstream C10-petbox,
///         <see cref="LootBoxCatalog.NoticeRewardWhitelist" /> is populated (item 602's two special-tier
///         rare-pet ids, 1012/1016) so <c>AttemptNoticeAsync</c>'s decision is live for that box; every other
///         box's own notice-worthy ids remain un-enumerated. The pity counter for box 2249 (C10-cloakbox2249)
///         IS exercised here via <see cref="ResolveRewardIdOverride" />; as of workstream
///         C10-remaining-box-pools, 8111/8114/8115's own pity counters are exercised the same way (see that
///         workstream's own paragraph below).
///     </para>
///     <para>
///         Workstream C10-remaining-boxes-tribe-keyed (extended by C10-remaining-box-pools): 76543 (Limited
///         Costume Chest), 1378/1379 (Sky/Earth Warlord Chest), 1236 (Heavenly Jade Chest), 8005 (Wing Lucky
///         Box), 8108 (Loy Krathong Box), and 720 (Chest / Reward Box) each have a fully-recovered reward table
///         (<see cref="CostumeChest76543RewardTable" />,
///         <see cref="WarlordChestRewardTable" />, <see cref="HeavenlyJadeChest1236RewardTable" />,
///         <see cref="WingLuckyBox8005RewardTable" />, <see cref="LoyKrathongBox8108RewardTable" />,
///         <see cref="ChestBox720RewardTable" />) but no reward pool any <see cref="BoxRewardKind" /> can
///         express -- their pool (or one band of it) is selected by the opening character's own stored
///         previous-tribe code (and, for 1378/1379, gated on a G12 <c>Level2</c> precondition), neither of which
///         <see cref="BoxRewardSpec.RollRewardId(Random)" /> has a parameter to receive. Rather than extending
///         that shared union, these 7 ids are dispatched through the SAME <see cref="LootBoxOpenResolver" />
///         placement/consumption/rental machinery every other box already uses, via two small seams: 1)
///         <see cref="ResolveSpec" /> layers a RentalDays-only placeholder <see cref="BoxRewardSpec" /> on top
///         of <see cref="LootBoxCatalog.TryGetSpec" /> for exactly these 7 ids (their
///         <see cref="BoxRewardSpec.UniformIds" /> is intentionally empty and provably unreachable -- see point
///         2), and 2) <see cref="ResolveRewardIdOverride" /> supplies a non-null reward-id override for them
///         (the same hook <see cref="LootBoxOpenResolver.OpenSingle" />/<see cref="LootBoxOpenResolver.OpenBulk" />
///         already prefer over <see cref="BoxRewardSpec.RollRewardId(Random)" /> for box 2249's pity roll), so
///         <see cref="BoxRewardSpec.RollRewardId(Random)" /> is never actually invoked for these 7 placeholders.
///         <see cref="LootBoxCatalog" /> itself is untouched: <see cref="LootBoxCatalog.TryGetSpec" /> still
///         returns null for all 7 (see that type's own remarks), only this handler's <see cref="ResolveSpec" />
///         layers them on top. Every box's own precondition failure (unrecognized previous tribe; for
///         1378/1379, an unmet G12 level gate) reuses the existing
///         <see cref="LootBoxOpenResolver.Outcome.RewardNotFound" /> fallback (reward id 0), producing the same
///         client-visible "reject, nothing granted" outcome the contract describes without adding a new enum
///         case for it.
///     </para>
///     <para>
///         <b>Workstream C10-remaining-box-pools, pity-gated boxes 8111/8114/8115:</b> unlike the tribe-keyed
///         boxes above, these three fit <see cref="BoxRewardKind.RareBandThenPools" /> for their POST-pity roll
///         (registered directly in <see cref="LootBoxCatalog" />, like box 2249) but still need the SAME
///         reward-id-override seam as 2249 to thread their own per-avatar pity counter
///         (<see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" />/<c>CloakVariantBoxPity</c>/
///         <c>MountVariantBoxPity</c>) through <see cref="ResolveRewardIdOverride" />. No placeholder spec is
///         needed for these three (<see cref="ResolveSpec" /> already resolves them via
///         <see cref="LootBoxCatalog.TryGetSpec" /> since they ARE registered there).
///     </para>
///     <para>
///         <b>Confirmation-pass follow-up -- box 8111 persistence gap closed:</b>
///         <see cref="ResolveRewardIdOverride" />'s box-8111 branch still writes the freshly-rolled counter
///         directly onto <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" /> synchronously (so a bulk
///         open's later iterations see the incremented/reset value immediately -- unchanged), but
///         <see cref="OpenSingleAsync" />/<see cref="OpenBulkAsync" /> now ALSO post a
///         <see cref="TribeProgressZoneCommand" /> mirror of that same value (see
///         <c>MirrorM15PetLuckyBoxPityAsync</c>) once per open request so <c>Zone.ApplyTribeProgressCommand</c>
///         marks Progression dirty and the write-behind flush actually persists it -- see
///         <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" />'s own remarks for the full citation
///         trail. Boxes 2249/8114/8115 are deliberately NOT touched by this pass and remain session-scoped
///         only.
///     </para>
///     <para>
///         <b>Workstream C10-remaining-box-pools, box 8108 security hardening:</b> the legacy single-open code
///         path for item 8108 contains a confirmed arbitrary-item-grant exploit (32% of single-open rolls
///         silently grant whatever item id the CLIENT itself supplied in the original request -- see
///         <see cref="LoyKrathongBox8108RewardTable" />'s own SECURITY remarks for the full citation). Fenrir
///         does not implement that legacy code path at all: both the single-open and bulk-open dispatch of item
///         8108 resolve through the exact same <see cref="ResolveRewardIdOverride" /> branch, which calls
///         <see cref="LoyKrathongBox8108RewardTable.Roll" /> -- a server-authoritative, fully-bounded table (the
///         legacy BULK-path's own 100%-covered table) -- so there is no code path in Fenrir where this box's
///         reward id is ever read from the client's request.
///     </para>
/// </remarks>
public sealed class LootBoxUseItemHandler(
    WorldDataCache worldData,
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<LootBoxUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>
    ///     game.EventLog.EventCode for the box-open "before" audit (GL_606 marker 1) -- an app-owned value
    ///     scoped within <see cref="EventLogCategory.ItemUse" />, distinct from that category's existing codes
    ///     (teleport 1, skill-grimoire 2/3, stat-potion 4). A future central event-code registry should reconcile
    ///     these constants.
    /// </summary>
    private const short BoxOpenBeforeEventCode = 5;

    /// <summary>game.EventLog.EventCode for the box-open reward "after" audit (GL_606 marker 2), same scoping as above.</summary>
    private const short BoxOpenRewardEventCode = 6;

    private const byte SuccessOutcome = 1;

    /// <summary>
    ///     The box ids this handler must be registered under in <c>UseItemHandlerRegistry</c> (wiring manifest):
    ///     <see cref="LootBoxCatalog" />'s generic-union ids (which, as of workstream C10-remaining-box-pools,
    ///     already include 1240/8111/8114/8115) plus -- workstream C10-remaining-boxes-tribe-keyed, extended by
    ///     C10-remaining-box-pools -- the 7 previous-tribe/level-keyed ids <see cref="ResolveSpec" />/
    ///     <see cref="ResolveRewardIdOverride" /> dispatch entirely outside that union (76543, 1378, 1379, 1236,
    ///     8005, 8108, 720). <c>UseItemHandlerRegistry</c>'s constructor loop (<c>foreach (var id in
    ///     LootBoxUseItemHandler.HandledItemIds) byId[id] = lootBox;</c>) already iterates this property, so
    ///     adding these ids here is the only edit needed to route them through the real registry -- no change to
    ///     <c>UseItemHandlerRegistry.cs</c> itself.
    /// </summary>
    public static ImmutableArray<int> HandledItemIds { get; } = LootBoxCatalog.Default.RegisteredBoxIds
        .Add(CostumeChest76543RewardTable.BoxItemId)
        .Add(WarlordChestRewardTable.SkyChestBoxItemId)
        .Add(WarlordChestRewardTable.EarthChestBoxItemId)
        .Add(HeavenlyJadeChest1236RewardTable.BoxId)
        .Add(WingLuckyBox8005RewardTable.BoxId)
        .Add(LoyKrathongBox8108RewardTable.BoxId)
        .Add(ChestBox720RewardTable.BoxId);

    /// <summary>Placeholder spec for 76543 -- see this type's own remarks, point 1. RentalDays-only; UniformIds unreachable.</summary>
    private static readonly BoxRewardSpec CostumeChestPlaceholderSpec = BoxRewardSpec.Uniform(
        CostumeChest76543RewardTable.BoxItemId, ImmutableArray<int>.Empty, CostumeChest76543RewardTable.RentalDays);

    /// <summary>Placeholder spec for 1378 -- no rental (all 87xxx ids fall outside the rentable-id range).</summary>
    private static readonly BoxRewardSpec SkyWarlordChestPlaceholderSpec =
        BoxRewardSpec.Uniform(WarlordChestRewardTable.SkyChestBoxItemId, ImmutableArray<int>.Empty);

    /// <summary>Placeholder spec for 1379 -- no rental, same reasoning as <see cref="SkyWarlordChestPlaceholderSpec" />.</summary>
    private static readonly BoxRewardSpec EarthWarlordChestPlaceholderSpec =
        BoxRewardSpec.Uniform(WarlordChestRewardTable.EarthChestBoxItemId, ImmutableArray<int>.Empty);

    /// <summary>Placeholder spec for 1236 -- no rental recorded by the contract for this box's reward.</summary>
    private static readonly BoxRewardSpec HeavenlyJadeChestPlaceholderSpec =
        BoxRewardSpec.Uniform(HeavenlyJadeChest1236RewardTable.BoxId, ImmutableArray<int>.Empty);

    /// <summary>Placeholder spec for 8005 -- no rental recorded by the contract for this box's reward.</summary>
    private static readonly BoxRewardSpec WingLuckyBoxPlaceholderSpec =
        BoxRewardSpec.Uniform(WingLuckyBox8005RewardTable.BoxId, ImmutableArray<int>.Empty);

    /// <summary>Placeholder spec for 8108 -- no rental recorded by the contract for this box's reward.</summary>
    private static readonly BoxRewardSpec LoyKrathongBoxPlaceholderSpec =
        BoxRewardSpec.Uniform(LoyKrathongBox8108RewardTable.BoxId, ImmutableArray<int>.Empty);

    /// <summary>Placeholder spec for 720 -- no rental recorded by the contract for this box's reward.</summary>
    private static readonly BoxRewardSpec ChestBox720PlaceholderSpec =
        BoxRewardSpec.Uniform(ChestBox720RewardTable.BoxId, ImmutableArray<int>.Empty);

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var boxId = context.Item.ItemId;

        if (ResolveSpec(boxId) is not { } spec || context.Item.Quantity < 1)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 loot-box ({BoxId}) rejected: no populated reward table or empty stack (quantity {Quantity})",
                context.CharacterId, boxId, context.Item.Quantity);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var isBulk = LootBoxCatalog.BulkOpenWhitelist.Contains(boxId) && context.Value > 1;
        return isBulk
            ? await OpenBulkAsync(context, spec, cancellationToken)
            : await OpenSingleAsync(context, spec, cancellationToken);
    }

    /// <summary>
    ///     Resolves <paramref name="boxId" />'s <see cref="BoxRewardSpec" />: <see cref="LootBoxCatalog" />'s
    ///     generic union first, then -- workstream C10-remaining-boxes-tribe-keyed, extended by
    ///     C10-remaining-box-pools -- the RentalDays-only placeholder for whichever of the 7 previous-tribe/
    ///     level-keyed boxes matches (see this type's own remarks, point 1). Null for any other unpopulated id,
    ///     exactly as before these workstreams.
    /// </summary>
    private static BoxRewardSpec? ResolveSpec(int boxId)
    {
        if (LootBoxCatalog.Default.TryGetSpec(boxId) is { } spec)
            return spec;

        return boxId switch
        {
            CostumeChest76543RewardTable.BoxItemId => CostumeChestPlaceholderSpec,
            WarlordChestRewardTable.SkyChestBoxItemId => SkyWarlordChestPlaceholderSpec,
            WarlordChestRewardTable.EarthChestBoxItemId => EarthWarlordChestPlaceholderSpec,
            HeavenlyJadeChest1236RewardTable.BoxId => HeavenlyJadeChestPlaceholderSpec,
            WingLuckyBox8005RewardTable.BoxId => WingLuckyBoxPlaceholderSpec,
            LoyKrathongBox8108RewardTable.BoxId => LoyKrathongBoxPlaceholderSpec,
            ChestBox720RewardTable.BoxId => ChestBox720PlaceholderSpec,
            _ => null
        };
    }

    private async ValueTask<UseInventoryItemResponse> OpenSingleAsync(UseItemContext context, BoxRewardSpec spec,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        // C1-vault-expiry-enforcement (trigger 2): the reward-placement search must not consider an expired
        // dated-vault last page a valid merge/empty-slot target, independent of whether the BOX ITSELF sits
        // on page0 or page1 -- see LootBoxOpenResolver.OpenSingle's own remarks.
        var today = GameDate.Today();
        var plan = LootBoxOpenResolver.OpenSingle(spec, context.Page, context.Index, context.Item, page0, page1,
            ResolveRewardSort, Random.Shared, today, ResolveRewardIdOverride(context),
            state.InventoryDate >= today);

        // Confirmation-pass follow-up: mirrors box 8111's pity counter for persistence -- see
        // MirrorM15PetLuckyBoxPityAsync's own remarks. A no-op for every other box id. Fires regardless of
        // plan.Outcome below: the override delegate above already wrote the counter in-memory before this
        // point, and a subsequent placement failure never rolls it back (see
        // M15PetLuckyBox8111RewardTable.Roll's own remarks).
        await MirrorM15PetLuckyBoxPityAsync(context, cancellationToken);

        switch (plan.Outcome)
        {
            case LootBoxOpenResolver.Outcome.RewardNotFound:
                logger.LogDebug(
                    "Character {CharacterId} op23 loot-box ({BoxId}) rolled unknown reward {RewardId}: box kept",
                    context.CharacterId, context.Item.ItemId, plan.RewardItemId);
                return UseItemResponses.Fail(context.Page, context.Index);

            case LootBoxOpenResolver.Outcome.InventoryFull:
                logger.LogDebug(
                    "Character {CharacterId} op23 loot-box ({BoxId}) reward {RewardId} could not be placed (inventory full): box kept",
                    context.CharacterId, context.Item.ItemId, plan.RewardItemId);
                return UseItemResponses.InventoryFull(context.Page, context.Index);
        }

        // Before-state audit (GL_606 marker 1): the box's pre-open slot state.
        await LogBoxOpenBeforeAsync(context, cancellationToken);

        await PersistAndMirrorAsync(context, page0, page1, plan.ProjectedPage0, plan.ProjectedPage1,
            cancellationToken);

        // After-state audit (GL_606 marker 2): the granted reward and its resulting slot.
        await LogBoxRewardAsync(context, plan.RewardItemId, plan.RewardQuantity, plan.RewardContainer,
            plan.RewardSlot, cancellationToken);

        await AttemptNoticeAsync(context, plan.RewardItemId, plan.RewardQuantity, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} op23 loot-box ({BoxId}) opened: reward {RewardId} x{Quantity} -> {Container}:{Slot} ({Placement}), box remaining {Remaining}",
            context.CharacterId, context.Item.ItemId, plan.RewardItemId, plan.RewardQuantity, plan.RewardContainer,
            plan.RewardSlot, plan.PlacementOutcome, plan.BoxRemainingQuantity);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private async ValueTask<UseInventoryItemResponse> OpenBulkAsync(UseItemContext context, BoxRewardSpec spec,
        CancellationToken cancellationToken)
    {
        var state = context.State;
        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        // C1-vault-expiry-enforcement (trigger 2): see OpenSingleAsync's own comment.
        var today = GameDate.Today();
        var plan = LootBoxOpenResolver.OpenBulk(spec, context.Page, context.Index, context.Item, page0, page1,
            ResolveRewardSort, Random.Shared, today, context.Value, ResolveRewardIdOverride(context),
            state.InventoryDate >= today);

        // Confirmation-pass follow-up: one mirror for the whole bulk request (not one per box opened) -- see
        // MirrorM15PetLuckyBoxPityAsync's own remarks. A no-op for every other box id.
        await MirrorM15PetLuckyBoxPityAsync(context, cancellationToken);

        if (plan.OpenedCount == 0)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 loot-box bulk ({BoxId}) opened nothing (inventory full or empty stack): box kept",
                context.CharacterId, context.Item.ItemId);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        // One before-state audit for the whole bulk request, then one reward audit per opened box.
        await LogBoxOpenBeforeAsync(context, cancellationToken);

        await PersistAndMirrorAsync(context, page0, page1, plan.ProjectedPage0, plan.ProjectedPage1,
            cancellationToken);

        foreach (var reward in plan.Rewards)
        {
            await LogBoxRewardAsync(context, reward.RewardItemId, reward.RewardQuantity, reward.RewardContainer,
                reward.RewardSlot, cancellationToken);
            await AttemptNoticeAsync(context, reward.RewardItemId, reward.RewardQuantity, cancellationToken);
        }

        logger.LogInformation(
            "Character {CharacterId} op23 loot-box bulk ({BoxId}) opened {Opened} box(es), box remaining {Remaining}",
            context.CharacterId, context.Item.ItemId, plan.OpenedCount, plan.BoxRemainingQuantity);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    /// <summary>
    ///     Persists whichever inventory page(s) the open actually changed and mirrors them onto the zone. The box
    ///     consumption always changes the box's own page, so at least one page differs; when the reward landed on
    ///     the other page too, both are written in one atomic <see cref="ICharacterRepository.ReplaceTwoContainersAsync" />
    ///     call so the reward and the box decrement can never half-commit.
    /// </summary>
    private async ValueTask PersistAndMirrorAsync(UseItemContext context,
        ImmutableDictionary<byte, ItemStack> originalPage0, ImmutableDictionary<byte, ItemStack> originalPage1,
        ImmutableDictionary<byte, ItemStack> projectedPage0, ImmutableDictionary<byte, ItemStack> projectedPage1,
        CancellationToken cancellationToken)
    {
        var page0Changed = !ReferenceEquals(projectedPage0, originalPage0);
        var page1Changed = !ReferenceEquals(projectedPage1, originalPage1);

        ImmutableArray<InventoryContainerSnapshot> containers;
        if (page0Changed && page1Changed)
        {
            await characters.ReplaceTwoContainersAsync(context.CharacterId,
                ContainerMatrix.InventoryPage0, ToTvps(projectedPage0),
                ContainerMatrix.InventoryPage1, ToTvps(projectedPage1), cancellationToken);
            containers = ImmutableArray.Create(
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, projectedPage0),
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, projectedPage1));
        }
        else
        {
            var container = page1Changed ? ContainerMatrix.InventoryPage1 : ContainerMatrix.InventoryPage0;
            var projected = page1Changed ? projectedPage1 : projectedPage0;
            await characters.ReplaceContainerAsync(context.CharacterId, container, ToTvps(projected),
                cancellationToken);
            containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        }

        if (!await context.Zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(context.CharacterId, containers, null), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 loot-box mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);
    }

    /// <summary>
    ///     The cluster-notice attempt (deferred broadcast, log only -- see this handler's own remarks). Live for
    ///     item 602's two special-tier rare-pet rewards (1012/1016) now that
    ///     <see cref="LootBoxCatalog.NoticeRewardWhitelist" /> is populated; a no-op broadcast-wise for every
    ///     other reward id. The elite-gain audit branch (for the 1035/1036/1037 boxes) is honored where it
    ///     applies, independent of the broadcast decision.
    /// </summary>
    private async ValueTask AttemptNoticeAsync(UseItemContext context, int rewardItemId, int rewardQuantity,
        CancellationToken cancellationToken)
    {
        var rewardType = worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Type : (byte)0;
        var decision = NoticeForBoxResolver.Decide(context.Item.ItemId, rewardItemId, rewardType);

        if (decision.WriteEliteGainAudit)
            await eventLog.LogBoxOpenEliteGainAsync(context.AccountId, context.CharacterId, context.Item.ItemId,
                rewardItemId, rewardQuantity, cancellationToken);

        if (decision.ShouldBroadcast)
            logger.LogInformation(
                "Loot-box elite notice (legacy NoticeForBox center relay, not client-broadcast -- deferred, see LootBoxUseItemHandler remarks): character {CharacterId} box {BoxId} reward {RewardId}",
                context.CharacterId, context.Item.ItemId, rewardItemId);
    }

    private byte? ResolveRewardSort(int rewardItemId)
    {
        return worldData.ItemsById.TryGetValue(rewardItemId, out var def) ? def.Item.Sort : null;
    }

    /// <summary>
    ///     Builds the reward-id override for a box whose reward id cannot be rolled through its own
    ///     <see cref="BoxRewardSpec" />: a per-avatar pity counter for 2249/8111/8114/8115
    ///     (<see cref="CloakBoxRewardTable" />/<see cref="M15PetLuckyBox8111RewardTable" />/
    ///     <see cref="CloakVariantBox8114RewardTable" />/<see cref="MountVariantBox8115RewardTable" />), or --
    ///     workstream C10-remaining-boxes-tribe-keyed, extended by C10-remaining-box-pools -- a previous-tribe/
    ///     level2-keyed external table for 76543/1378/1379/1236/8005/8108/720 (see this type's own remarks).
    ///     Null for every other box, which rolls through <see cref="BoxRewardSpec.RollRewardId(Random)" /> as
    ///     before. Every branch reads <see cref="PlayerRuntimeState" /> fields directly from the closure --
    ///     safe because <see cref="IUseItemHandler.HandleAsync" /> always runs on this character's own zone
    ///     tick thread, the single-writer contract every other <see cref="PlayerRuntimeState" /> field already
    ///     relies on. For a bulk open, <see cref="LootBoxOpenResolver.OpenBulk" /> invokes the returned delegate
    ///     once per box actually attempted -- for every pity counter, that means pity increments once per open
    ///     exactly as the contract's side effect 1 requires (including for a box whose open subsequently fails
    ///     placement, since this closure's counter write runs before <see cref="LootBoxOpenResolver.OpenSingle" />'s
    ///     own lookup/placement steps); for the tribe-keyed boxes, the tribe/level2 gate is re-evaluated once
    ///     per box too, even though it cannot actually change mid-bulk-request.
    ///     <para>
    ///         Every tribe-keyed box here returns reward id 0 for a failed precondition (an unrecognized
    ///         previous tribe; for 1378/1379, additionally an unmet G12 level gate) -- 0 is not a known item id,
    ///         so <see cref="LootBoxOpenResolver.OpenSingle" />'s own catalog lookup rejects it as
    ///         <see cref="LootBoxOpenResolver.Outcome.RewardNotFound" /> (box kept, nothing granted), the same
    ///         client-visible "reject, nothing granted" outcome the contract describes without a new
    ///         <see cref="LootBoxOpenResolver.Outcome" /> case. 76543's own independently-rolled "enchant grade"
    ///         (30-60, <see cref="CostumeChest76543RewardTable.Roll" />) is deliberately discarded here, not
    ///         stamped onto the granted item -- see that type's own remarks for why the byte-level mapping is an
    ///         open question, not invented here.
    ///     </para>
    ///     <para>
    ///         Box 8108 is additionally the security-hardened case of this hook -- see this type's own class
    ///         remarks and <see cref="LoyKrathongBox8108RewardTable" />'s own SECURITY remarks: BOTH the single-
    ///         open and bulk-open dispatch of 8108 resolve through this ONE branch, so the client's own request
    ///         value is never consulted for this box's reward id in Fenrir, unlike the legacy single-open code
    ///         path this codebase deliberately does not reproduce.
    ///     </para>
    /// </summary>
    private static Func<int>? ResolveRewardIdOverride(UseItemContext context)
    {
        var boxId = context.Item.ItemId;

        if (boxId == CloakBoxRewardTable.BoxId)
        {
            return () =>
            {
                var result = CloakBoxRewardTable.Roll(context.State.CloakLuckyBoxPity, Random.Shared);
                context.State.CloakLuckyBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };
        }

        if (boxId == CostumeChest76543RewardTable.BoxItemId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = CostumeChest76543RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        if (boxId is WarlordChestRewardTable.SkyChestBoxItemId or WarlordChestRewardTable.EarthChestBoxItemId)
        {
            var previousTribe = context.State.PreviousTribe;
            var level2 = context.State.Level2;
            return () =>
            {
                if (!WarlordChestRewardTable.MeetsLevelGate(level2))
                    return 0;

                return WarlordChestRewardTable.TryRollReward(boxId, previousTribe, Random.Shared, out var rewardId)
                    ? rewardId
                    : 0;
            };
        }

        // Workstream C10-remaining-box-pools -- pity-gated boxes 8111/8114/8115 (registered directly in
        // LootBoxCatalog for their post-pity roll; only the pity-counter threading needs this hook, same
        // shape as box 2249 above).
        if (boxId == M15PetLuckyBox8111RewardTable.BoxId)
        {
            return () =>
            {
                var result = M15PetLuckyBox8111RewardTable.Roll(context.State.M15PetLuckyBoxPity, Random.Shared);
                context.State.M15PetLuckyBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };
        }

        if (boxId == CloakVariantBox8114RewardTable.BoxId)
        {
            return () =>
            {
                var result = CloakVariantBox8114RewardTable.Roll(context.State.CloakVariantBoxPity, Random.Shared);
                context.State.CloakVariantBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };
        }

        if (boxId == MountVariantBox8115RewardTable.BoxId)
        {
            return () =>
            {
                var result = MountVariantBox8115RewardTable.Roll(context.State.MountVariantBoxPity, Random.Shared);
                context.State.MountVariantBoxPity = result.NewPityCounter;
                return result.RewardItemId;
            };
        }

        // Workstream C10-remaining-box-pools -- tribe-keyed boxes 1236/8005/720 (same placeholder-spec +
        // override-hook shape as 76543/1378/1379 above).
        if (boxId == HeavenlyJadeChest1236RewardTable.BoxId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = HeavenlyJadeChest1236RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        if (boxId == WingLuckyBox8005RewardTable.BoxId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = WingLuckyBox8005RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        if (boxId == ChestBox720RewardTable.BoxId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = ChestBox720RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        // Workstream C10-remaining-box-pools -- box 8108 SECURITY HARDENING: the legacy single-open code path
        // for this item contains a confirmed arbitrary-item-grant exploit (see LoyKrathongBox8108RewardTable's
        // own remarks). Both single-open and bulk-open dispatch of 8108 resolve through this ONE override,
        // which is the legacy bulk-path's own fully-bounded, server-authoritative table -- never the
        // vulnerable single-path code, and the client's own request value is never consulted here at all.
        if (boxId == LoyKrathongBox8108RewardTable.BoxId)
        {
            var previousTribe = context.State.PreviousTribe;
            return () =>
            {
                var result = LoyKrathongBox8108RewardTable.Roll(previousTribe, Random.Shared);
                return result.Success ? result.RewardItemId : 0;
            };
        }

        return null;
    }

    /// <summary>
    ///     Confirmation-pass follow-up: mirrors box 8111's freshly-updated
    ///     <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" /> onto this character's write-behind
    ///     progress flush. <see cref="ResolveRewardIdOverride" />'s own closure already wrote the rolled
    ///     counter directly onto <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" /> -- synchronously,
    ///     so a bulk-open's later iterations see the incremented/reset value immediately (unchanged by this
    ///     method). That direct write alone never reaches game.Characters: <c>DirtyTracker</c> is Zone-internal
    ///     state, so the sanctioned way to flag <c>DirtyFlags.Progression</c> dirty from a handler is the same
    ///     <see cref="TribeProgressZoneCommand" /> round trip every other counter mirror in this codebase uses
    ///     (see <c>EliteDungeonTicketUseItemHandler</c>/<c>DungeonKeyUseItemHandler</c>). Posted once per open
    ///     request (single or the whole bulk run), mirroring the FINAL post-roll value -- <c>Zone
    ///     .ApplyTribeProgressCommand</c>'s own apply arm is a plain field overwrite either way, so mirroring
    ///     the already-current value here is a safe no-op mutation-wise; only its <c>changed=true</c>/
    ///     <c>MarkProgressDirty</c> side effect is the actual payload. Fires unconditionally -- even on a
    ///     RewardNotFound/InventoryFull outcome for THIS box -- matching the contract's "pity increment is
    ///     never rolled back by a subsequent placement failure" rule (see
    ///     <see cref="M15PetLuckyBox8111RewardTable" />'s own remarks). A no-op (no command posted)
    ///     for every other box id. Scoped to box 8111 only for this workstream; boxes 2249/8114/8115's own
    ///     pity counters remain the pre-existing session-scoped-only gap -- see
    ///     <see cref="World.PlayerRuntimeState.M15PetLuckyBoxPity" />'s own remarks.
    /// </summary>
    private async ValueTask MirrorM15PetLuckyBoxPityAsync(UseItemContext context, CancellationToken cancellationToken)
    {
        if (context.Item.ItemId != M15PetLuckyBox8111RewardTable.BoxId)
            return;

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId,
                    M15PetLuckyBoxPity: context.State.M15PetLuckyBoxPity),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped M15 Pet Lucky Box (8111) pity-counter persistence mirror for character {CharacterId}",
                context.Zone.MapId, context.CharacterId);
    }

    private ValueTask LogBoxOpenBeforeAsync(UseItemContext context, CancellationToken cancellationToken)
    {
        return eventLog.LogAsync(BoxOpenBeforeEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, context.Item.ItemId, context.Item.Quantity,
            SuccessOutcome, $"Page={context.Page};Index={context.Index};Serial={context.Item.Serial}",
            cancellationToken);
    }

    private ValueTask LogBoxRewardAsync(UseItemContext context, int rewardItemId, int rewardQuantity,
        byte rewardContainer, byte rewardSlot, CancellationToken cancellationToken)
    {
        return eventLog.LogAsync(BoxOpenRewardEventCode, EventLogCategory.ItemUse, context.AccountId,
            context.CharacterId, null, null, null, null, null, rewardItemId, rewardQuantity, SuccessOutcome,
            $"Box={context.Item.ItemId};Reward={rewardItemId}x{rewardQuantity};Slot={rewardContainer}:{rewardSlot}",
            cancellationToken);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
