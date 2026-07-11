using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     op23 items 8153/8154 -- the Faction Transfer scroll (TChangeTribe): a client-chosen conversion between
///     the three playable tribes (0-2), gated on 13 sequential server-authoritative preconditions
///     (<see cref="TribeScrollTransferGate" />), then an atomic best-effort equip/skill/hotkey remap + tribe
///     flip + scroll consumption (<see cref="ITribeConversionRepository.ApplyTribeScrollConversionAsync" />),
///     and finally a signal telling the client to leave the current zone.
/// </summary>
/// <remarks>
///     Réf. C++ : "Faction Transfer scroll (items 8153/8154) runtime gates + TChangeTribe mutation" behavior
///     contract (legacy-behavior-translator), itself citing Server/ts25zone/S04_MyWork03.cpp:4740-4899 and
///     Server/ts25zone/S07_MyGame03.cpp:11400-11749 throughout. Several deliberate translation decisions:
///     <list type="bullet">
///         <item>
///             <b>The atomic mutation lives in a NEW, distinct stored procedure.</b>
///             <see cref="ITribeConversionRepository.ApplyTribeScrollConversionAsync" />
///             (game.usp_Character_ApplyTribeScrollConversion) is a genuinely separate script from the
///             tribe-conversion BOOK path's own game.usp_Character_ApplyTribeConversion -- the scroll's
///             equipment remap is best-effort (an un-mappable slot is left alone, never an abort) where the
///             book's is all-or-nothing, and the scroll's target tribe is client-supplied where the book's is
///             derived from the consumed item id. The two must never be conflated, and neither script was
///             edited to build the other -- see the scroll procedure's own header for the full split of what
///             it durably persists.
///         </item>
///         <item>
///             <b>Deferred, NOT rebuilt: the live in-memory mirror of the equip/skill remap.</b> This handler
///             mirrors the tribe flip and the scroll's own consumption into <see cref="World.PlayerRuntimeState" />
///             immediately (cheap, and other systems read <see cref="World.PlayerRuntimeState.Tribe" />
///             constantly), and DOES mirror the best-effort equipment + learned-skill remap too (reusing
///             <see cref="TribeConversionResolver" /> -- the prior session's own equivalence-lookup
///             infrastructure that had a schema, seed data, and a repository read, but no C# consumer anywhere
///             until this handler and its accompanying <see cref="TribeConversionCatalogLoader" /> boot-time
///             wiring). Hotkey-slot remap mirroring is deliberately NOT modeled in-memory here: no
///             hotkey-bulk-replace <see cref="World.ZoneCommand" /> exists yet, hotkeys are purely cosmetic
///             (no stat/combat consequence), and the imminent "return to auto zone" signal (see below) means
///             any residual staleness is self-healing within moments -- the character's very next zone
///             (re-)entry rehydrates every one of these fields fresh from SQL, where the durable mutation has
///             already committed correctly regardless of what this session's live mirror shows in the interim.
///         </item>
///         <item>
///             <b>Gate 4 (the ts25extra cross-process per-destination-tribe admin toggle) is NOT modeled.</b>
///             See <see cref="TribeScrollTransferGate" />'s own remarks -- no Fenrir-side equivalent process or
///             persisted toggle table exists; flagged as an open item for a future contract/product decision,
///             not silently invented here.
///         </item>
///         <item>
///             <b>Two gates are genuine disconnects, not clean Result=1 failures</b> -- the client-supplied
///             destination-tribe range check (hardening a legacy check that could never itself trip) and the
///             same-as-previous-tribe rejection (a real legacy <c>Quit()</c>). Reused the
///             <c>state.Session is ClientSession client</c> / <c>client.Abort(reason)</c> idiom
///             <see cref="Simulation.PersonalShopRegionEnforcementSystem" />/the C9 costume-stellar-whitelist
///             workstream's <c>CostumeStellarCoreUseItemHandler</c> already established for a request-thread
///             op23 handler needing a real disconnect without widening <see cref="IUseItemHandler" />'s return
///             shape.
///         </item>
///         <item>
///             <b>"Return to auto zone" reuses the existing header-only wire packet.</b>
///             <see cref="ReturnToHomeZoneResponse" /> already exists and is already sent from
///             <c>World.ZoneWar.AntiCampingForcedReturnSystem</c> for the unrelated
///             <c>MyTransfer::B_RETURN_TO_AUTO_ZONE</c> legacy call -- the exact same wire shape this scroll's
///             own step 4 needs (Server/ts25zone/S04_MyWork03.cpp:4853-4854, Server/Header/Protocol/ZONE.h:
///             942-944,1587-1588). Sent from inside <see cref="HandleAsync" /> before it returns, which means
///             the WIRE order ends up [return-to-auto-zone signal, then the item-use ack] -- the reverse of the
///             legacy's own [ack, then signal] order -- because <see cref="IUseItemHandler" />'s own shape has
///             the caller (<c>UseInventoryItemHandler</c>) send whatever this method returns only AFTER it
///             returns. Documented here as a deliberate, interface-shape-driven simplification: the behavior
///             contract itself flags that what the client concretely does upon receiving this signal is
///             unknown (outside the scope of every file read for it), so no observable consequence of this
///             reordering could be identified.
///         </item>
///         <item>
///             <b>No audit/game-log record</b> -- faithfully not reproduced; the originating contract confirms
///             neither the case body nor the mutation function itself writes one anywhere.
///         </item>
///     </list>
/// </remarks>
public sealed class TribeScrollTransferUseItemHandler(
    ITribeConversionRepository tribeConversion,
    TribeConversionResolver resolver,
    PartyRegistry partyRegistry,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    WorldDataCache worldData,
    GameServerOptions options,
    ILogger<TribeScrollTransferUseItemHandler> logger) : IUseItemHandler
{
    /// <summary>world.Items 8153/8154 -- the Faction Transfer scroll, identical treatment for both ids.</summary>
    public static readonly ImmutableArray<int> HandledItemIds = [8153, 8154];

    public async ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context,
        CancellationToken cancellationToken)
    {
        var state = context.State;

        // Gate 1's client-supplied value can be any wire int -- clamp anything outside byte range to a
        // sentinel that TribeScrollTransferGate.Evaluate's own IsPlayableTribe check will also reject, so
        // every out-of-range value (negative, or >2, or unrepresentable as a byte) funnels through the same
        // single gate rather than a separate ad hoc pre-check.
        var toTribe = context.Value is >= 0 and <= 255 ? (byte)context.Value : (byte)255;

        var homeZoneOnline = await IsFactionTransferHomeZoneOnlineAsync(cancellationToken);
        var cape = state.Inventory.GetSlot(ContainerMatrix.Equipment, (byte)SkillGradeAuthority.CapeSlotIndex);

        var outcome = TribeScrollTransferGate.Evaluate(new TribeScrollTransferEligibilityContext(
            toTribe, state.Tribe, state.PreviousTribe, state.Level, state.TribeRole, state.GuildId,
            state.TeacherCharacterId, state.StudentCharacterId, !state.Friends.IsEmpty,
            partyRegistry.IsInParty(context.CharacterId), cape is not null, context.Zone.MapId, homeZoneOnline));

        if (outcome != TribeScrollTransferOutcome.Success)
        {
            logger.LogDebug(
                "Character {CharacterId} op23 faction-transfer scroll ({ItemId}) rejected: {Outcome} (tribe {Tribe}->{ToTribe}, level {Level})",
                context.CharacterId, context.Item.ItemId, outcome, state.Tribe, toTribe, state.Level);

            switch (outcome)
            {
                case TribeScrollTransferOutcome.InvalidDestinationTribe:
                {
                    if (state.Session is ClientSession client)
                        client.Abort(DisconnectReason.Malformed);
                    break;
                }
                case TribeScrollTransferOutcome.AlreadyTargetTribe:
                {
                    if (state.Session is ClientSession client)
                        client.Abort(DisconnectReason.StateViolation);
                    break;
                }
            }

            // Never actually transmitted for the two disconnect cases above -- ClientSession.Send<TPacket>
            // no-ops once Abort has run. Every other rejection reason gets this same clean Result=1 reply.
            return UseItemResponses.Fail(context.Page, context.Index);
        }

        var fromTribe = state.PreviousTribe;

        // Best-effort equipment remap (mirrors TChangeTribe's own INNER JOIN posture -- an un-mappable slot is
        // left exactly as-is, never an abort), excluding the pet slot exactly like the SQL procedure.
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var newEquipmentBuilder = equipmentContainer.ToBuilder();
        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot == PetSlots.EquipmentSlot)
                continue;

            if (resolver.TryRemapItem(fromTribe, toTribe, stack.ItemId, out var newItemId) &&
                newItemId != stack.ItemId)
                newEquipmentBuilder[slot] = stack with { ItemId = newItemId };
        }

        var newEquipment = newEquipmentBuilder.ToImmutable();

        // Best-effort learned-skill remap, same silent-pass-through-on-no-match posture.
        var skillChanges = new List<(byte Slot, LearnedSkill NewSkill)>();
        foreach (var (slot, skill) in state.LearnedSkills)
            if (resolver.TryRemapSkill(fromTribe, toTribe, skill.SkillId, out var newSkillId) &&
                newSkillId != skill.SkillId)
                skillChanges.Add((slot, skill with { SkillId = newSkillId }));

        // The scroll's own slot is consumed in full -- a stacked quantity is entirely destroyed by one use,
        // never decremented, matching the atomic procedure's own whole-container-replace convention.
        var projectedPage = state.Inventory.GetContainer(context.Page).Remove(context.Index);
        var items = ToTvps(projectedPage);

        await tribeConversion.ApplyTribeScrollConversionAsync(context.CharacterId, context.Item.ItemId, toTribe,
            context.Page, items, cancellationToken);

        // Mirror the tribe flip first -- TChangeTribe sets BOTH PreviousTribe and Tribe to the target, and the
        // stat recompute below needs the FRESH values (Tribe/PreviousTribe feed CharacterBaseAttributes).
        if (!await context.Zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(context.CharacterId, Tribe: toTribe, PreviousTribe: toTribe),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped op23 faction-transfer tribe mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);

        var updatedStats = UseItemStatRecompute.WithEquipment(state, worldData, newEquipment);

        var containers = ImmutableArray.Create(
            new InventoryContainerSnapshot(context.Page, projectedPage),
            new InventoryContainerSnapshot(ContainerMatrix.Equipment, newEquipment));

        if (!await context.Zone.PostInventoryCommandAndWaitAsync(
                new InventoryZoneCommand(context.CharacterId, containers, updatedStats), cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped op23 faction-transfer inventory mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                context.Zone.MapId, context.CharacterId);

        // Skill-slot mirrors are fire-and-forget, matching ResolveSkillGrimoireAsync's own precedent -- each
        // slot is independent and low-frequency, no caller needs to await any individual one.
        foreach (var (slot, newSkill) in skillChanges)
            if (!context.Zone.PostSkillCommand(new SkillZoneCommand(context.CharacterId, slot, newSkill,
                    state.SkillPoints)))
                logger.LogError(
                    "Zone {MapId} skill inbox full: dropped op23 faction-transfer skill-remap mirror (slot {Slot}) for character {CharacterId}",
                    context.Zone.MapId, context.CharacterId, slot);

        logger.LogInformation(
            "Character {CharacterId} op23 faction-transfer scroll ({ItemId}) applied: tribe {FromTribe}->{ToTribe}",
            context.CharacterId, context.Item.ItemId, fromTribe, toTribe);

        // Step 4: signal the client to leave this zone -- see this class's own remarks for the resulting wire
        // order relative to the ack this method returns.
        state.Session.Send(new ReturnToHomeZoneResponse());

        return UseItemResponses.Success(context.Page, context.Index);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }

    /// <summary>
    ///     Resolves gate 5's "specific numbered zone/server slot must be marked online" precondition against
    ///     Fenrir's own map-sharded topology, the same live-directory-cross-shard-claim shape
    ///     <see cref="ForcedNeutralTribeResetUseItemHandler" />'s own private
    ///     <c>IsNeutralHomeZoneOnlineAsync</c> helper already uses. See
    ///     <see cref="GameServerOptions.FactionTransferHomeZoneMapId" />'s own remarks for why the map id is
    ///     operator-configured (defaulting to permanently offline) rather than hardcoded to the
    ///     corroborated-but-not-directly-cited candidate of 37.
    /// </summary>
    private async ValueTask<bool> IsFactionTransferHomeZoneOnlineAsync(CancellationToken cancellationToken)
    {
        var mapId = options.FactionTransferHomeZoneMapId;
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
