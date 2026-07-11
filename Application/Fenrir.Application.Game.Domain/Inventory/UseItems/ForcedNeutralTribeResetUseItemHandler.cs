using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 item 8100 — the forced-neutral-tribe-reset consumable. Once every precondition in
///     <see cref="ForcedNeutralTribeResetGate" /> passes, this unconditionally routes the character into the
///     neutral faction (tribe 3): a pure faction flip, with NO equipment remap, NO skill remap, and NO
///     hotkey/costume remap of any kind — unlike the tribe-change book/scroll family
///     (<see cref="TribeConversionResolver" />), which performs all three. The character's "previous tribe"
///     (race/starter-kit template) field is also left untouched.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:7225-7281 (the item-8100 case body in full; personally
///     re-opened and confirmed character-for-character by the translating behavior contract this workstream
///     implements). Several deliberate translation decisions, each matching an established sibling pattern in
///     this same file family:
///     <list type="bullet">
///         <item>
///             <b>Consume-then-mirror ordering.</b> The legacy source consumes the item LAST, after both
///             tribe-field writes. This deliberately reorders to consume the item FIRST (durable), then apply
///             the tribe write, then mirror — the same hardening <see cref="TitleUpgradeUseItemHandler" />
///             already documents on itself ("a dropped mirror can only lose the ticket, never grant an unpaid
///             [benefit]"). Legacy has no transaction boundary around any of this at all; Fenrir's own
///             ordering choice is the closest analogue available without a dedicated new stored procedure.
///         </item>
///         <item>
///             <b>Two legacy write locations collapse into one.</b> The legacy source writes the character's
///             tribe to two separate in-memory locations (an avatar-record field and an independently-tracked
///             session-mirror field, S04_MyWork03.cpp:7278-7279). Fenrir's <see cref="World.PlayerRuntimeState" />
///             has exactly one <c>Tribe</c> field — there is no second location to keep in sync — so a single
///             <see cref="TribeProgressZoneCommand.Tribe" /> mirror covers both legacy writes at once.
///         </item>
///         <item>
///             <b>Reuses the existing atomic Tribe-write procedure instead of adding a new one.</b>
///             <see cref="ICharacterRepository.ApplyTribeFourConversionAsync" />
///             (game.usp_Character_ApplyTribeFourConversion) already atomically writes
///             game.Characters.Tribe plus the 5-slot quest-state columns in one transaction. This action never
///             touches quest state, so the character's own current quest-progress fields
///             (<see cref="World.PlayerRuntimeState.QuestStepPermanent" />/<c>QuestActiveFlag</c>/
///             <c>QuestSort</c>/<c>QuestTargetPhase</c>/<c>QuestKillCounter</c>) are written back unchanged — a
///             genuine no-op for that half of the procedure, not a quest-state side effect of this action.
///         </item>
///         <item>
///             <b>The center broadcast (code 51) is deliberately NOT reproduced.</b> The legacy source sends a
///             cross-process broadcast carrying the pre-reset tribe and the character's name
///             (S04_MyWork03.cpp:7276) before the tribe fields are overwritten. The behavior contract's own
///             research found this code is an explicit no-op on the legacy center process (empty case body,
///             Server/ts25center/S04_MyWork02.cpp:474) that falls through to a generic relay
///             (:1214-1221) with no live receiving handler anywhere on the zone side (only a commented-out
///             placeholder, Server/ts25zone/S07_MyGame08.cpp:981) — i.e. a confirmed dead broadcast with no
///             observable effect anywhere in the current source. Per the contract's own explicit guidance, no
///             consumer-side behavior should be invented for it; if a future finding discovers an actual
///             purpose, wire it then.
///         </item>
///         <item>
///             <b>No gamelog/audit record.</b> Unlike most other op23 branches in the same legacy handler, the
///             item-8100 branch writes no audit record — faithfully not reproduced here either.
///         </item>
///         <item>
///             <b>The neutral-home-zone-online precondition</b> is resolved via
///             <see cref="IsNeutralHomeZoneOnlineAsync" />, deliberately NOT a hardcoded map id — see that
///             method's own remarks.
///         </item>
///     </list>
/// </remarks>
public sealed class ForcedNeutralTribeResetUseItemHandler(
    ICharacterRepository characters,
    UseItemInventoryWriter inventoryWriter,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    GameServerOptions options,
    ILogger<ForcedNeutralTribeResetUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 8100 — the forced-neutral-tribe-reset consumable.</summary>
    public const int ItemId = 8100;

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        if (context.Item.Quantity < 1)
            return UseItemResponses.Fail(context.Page, context.Index);

        var neutralHomeZoneOnline = await IsNeutralHomeZoneOnlineAsync(cancellationToken);

        var outcome = ForcedNeutralTribeResetGate.Evaluate(new ForcedNeutralTribeResetEligibilityContext(
            state.Level, state.Tribe, state.TribeRole, state.GuildId, state.TeacherCharacterId,
            state.StudentCharacterId, !state.Friends.IsEmpty, neutralHomeZoneOnline));

        if (outcome != ForcedNeutralTribeResetOutcome.Success)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 forced-neutral tribe reset (8100) rejected: {Outcome} (tribe {Tribe}, level {Level})",
                context.CharacterId, outcome, state.Tribe, state.Level);
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var oldTribe = state.Tribe;

        // Consume the item first (durable) -- see this class's own remarks for why this deliberately reorders
        // the legacy's own consume-last sequence.
        var remaining = CashItemStackConsumption.RemainingQuantity(context.Item.ItemId, context.Item.Quantity);
        await inventoryWriter.ConsumeAndMirrorAsync(context.Zone, state, context.CharacterId, context.Page,
            context.Index, context.Item, remaining, null, cancellationToken);

        // Durable tribe write, reusing the existing atomic Tribe+quest-state procedure with the character's
        // own current quest-progress fields written back unchanged -- see this class's own remarks.
        await characters.ApplyTribeFourConversionAsync(context.CharacterId, ForcedNeutralTribeResetGate.NeutralTribe,
            state.QuestStepPermanent, state.QuestActiveFlag, state.QuestSort, state.QuestTargetPhase,
            state.QuestKillCounter, cancellationToken);

        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, Tribe: ForcedNeutralTribeResetGate.NeutralTribe),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 forced-neutral tribe reset mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);

        logger.LogInformation(
            "Character {CharacterId} op23 forced-neutral tribe reset (8100) applied: tribe {OldTribe} -> {NewTribe}",
            context.CharacterId, oldTribe, ForcedNeutralTribeResetGate.NeutralTribe);

        return UseItemResponses.Success(context.Page, context.Index);
    }

    /// <summary>
    ///     Resolves the "neutral faction's designated home zone/server process is currently connected"
    ///     precondition against Fenrir's own map-sharded topology instead of a legacy numbered-server-process
    ///     connection: true only when <see cref="GameServerOptions.ForcedNeutralResetHomeMapId" /> is a
    ///     positive map id AND that map id is currently claimed by at least one live shard in
    ///     <see cref="IGameServerDirectoryRepository.GetDirectoryAsync(CancellationToken)" /> per
    ///     <see cref="IShardMapAssignmentRepository.GetHostedMapsAsync" /> — the same live-directory-cross-
    ///     shard-claim shape <c>ZoneMoveService.HandleCrossShardAsync</c> already uses to resolve cross-shard
    ///     zone-transfer reachability.
    ///     <para>
    ///         The legacy identification of "server number 140" as the neutral faction's home zone is only
    ///         circumstantially corroborated (a dead, unreachable packet handler that happens to share the
    ///         same tribe-to-server-number pattern — see the behavior contract's own Edge cases), and Fenrir
    ///         has no server-number space to translate 140 into directly (map-sharded, not one-process-per-
    ///         tribe). <see cref="GameServerOptions.ForcedNeutralResetHomeMapId" /> is therefore left
    ///         operator-configured rather than hardcoded, defaulting to 0 ("never a real hosted map" — the
    ///         same fail-safe convention <c>VoteTribeMapId</c>/<c>HolyStoneMapId</c> already use) so this
    ///         precondition stays permanently unmet, and item 8100 permanently unusable, until an operator
    ///         confirms the real Fenrir map id and configures it.
    ///     </para>
    /// </summary>
    private async ValueTask<bool> IsNeutralHomeZoneOnlineAsync(CancellationToken cancellationToken)
    {
        var mapId = options.ForcedNeutralResetHomeMapId;
        if (mapId <= 0)
            return false;

        var shards = await directory.GetDirectoryAsync(cancellationToken);
        foreach (var shard in shards)
        {
            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(shard.ShardId, cancellationToken);
            if (hostedMaps.Contains(mapId))
                return true;
        }

        return false;
    }
}
