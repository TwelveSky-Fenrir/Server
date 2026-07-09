using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmPetExperienceGrantService" />'s own remarks for the wire-level contract summary.
///     Citations: Server/ts25zone/S04_MyWork04.cpp:2062-2083 (full case 700 body: privilege gate,
///     unconditional result-code assignment, unconditional overwrite of the pet-identifier field to the "no
///     pet" sentinel, the equipped-pet-slot/catalog-sort-code checks gating the grant call, the conditional
///     write of the echoed experience field) ; Server/Header/Protocol/STRUCT.h:1296-1300 (payload shape: two
///     whole-number fields, PetId/PetExperience) ; Server/Header/Protocol/STRUCT.h:1662-1676 (equipment-slot
///     enumeration confirming the pet slot's position, ported as <see cref="PetSlots.EquipmentSlot" />) ;
///     Server/Header/Protocol/STRUCT.h:1696 (pet-item catalog sort-code constant used by the outer
///     eligibility check -- item catalog Sort 22, the same constant <see cref="PetGrowthCalculator.Compute" />
///     already gates its own stat-contribution computation on) ; Server/ts25zone/H08_MyGameSystem.h:212 (the
///     grant-routine declaration; its packet-notification parameter defaults to send) ;
///     Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:1804-1894 (item-id-to-growth-tier lookup switch),
///     :1895-1918 (unrecognized-item-id zero-growth default, already-at-cap early return, growth-rate-based
///     recomputation, clamp to tier maximum), :1920-1971 (grant routine full body: pet re-resolution from the
///     equipped slot, force-activation-on-inactive side effect, proactive packet, growth-stage-crossing
///     second broadcast) -- the actual growth-grant subsystem this command calls into, NOT ported by this
///     type; see this type's own remarks for why -- and :8-15 (per-tier maximum-growth constants).
/// </summary>
/// <remarks>
///     This type deliberately implements only the OUTER shape of case 700 -- the privilege gate, the
///     unconditional accepted result code, and the equipped-pet eligibility check (slot 8 occupied, catalog
///     Sort == 22) that decides which sentinel/item-id value to echo back in the PetId field. It does NOT
///     port the inner growth-grant computation itself: the per-item-id tier-bucket lookup switch
///     (GameSystem_07_Pet.cpp:1804-1894), the at-cap-skip/recompute-toward-tier-maximum/clamp logic
///     (:1895-1918), or the force-activation/proactive-packet/growth-stage-threshold-rebroadcast side effects
///     of the grant routine itself (:1920-1971). A second, more detailed behavior contract pass (this type's
///     own citations above) still only points at those line ranges and the four base tier CAP constants
///     (GameSystem_07_Pet.cpp:8-15, 40M/80M/160M/320M -- the same four values
///     <see cref="PetGrowthCalculator" />'s own <c>MaxRangeValue</c> array already uses for the *unrelated*
///     stat-contribution formula) -- it does not transcribe the concrete item-id -&gt; tier-bucket table for
///     *this* function, nor the exact recompute formula ("drives the counter to at or very near the tier
///     maximum in one call" is a qualitative description, not a formula). Inferring that table from
///     <see cref="PetGrowthCalculator" />'s own <c>LifeFamily</c>/<c>ManaFamily</c>/<c>AttackFamily</c>/
///     <c>DefenseFamily</c> tables would be guessing at one legacy function's behavior from a
///     similar-looking-but-distinct sibling function, which this project's own engineering rules forbid.
///     Porting a fabricated mapping or formula would silently ship wrong per-pet growth amounts (and a
///     fabricated "pet activation" state transition -- Fenrir has no modeled boolean pet-active/inactive
///     state anywhere yet, only the decayable <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.PetActivity" />
///     meter, a different concept) rather than a wire-format or gating defect. Every request that finds an
///     eligible pet therefore still reports the accepted result code and echoes the equipped item's real
///     catalog id in PetId (both confirmed, cited behaviors), but never actually grants any experience,
///     never force-activates a pet, never fires the activation/growth-stage rebroadcasts, and always echoes
///     PetExperience = 0 -- flagged here explicitly rather than silently approximated. A dedicated
///     <c>legacy-behavior-translator</c>/<c>cpp-zone-gameplay-analyst</c> follow-up transcribing the concrete
///     per-item-id table, the exact recompute formula, and how "pet active state" should be modeled in
///     <see cref="PlayerRuntimeState" /> is required before this gap can be closed.
///     <para>
///         Legacy's own case 700 body writes no game.EventLog row either -- the
///         <see cref="IEventLogRepository.LogAsync" /> call below is a Fenrir-authored addition (project
///         decision: every Admin-tier GM command must leave a durable audit trail), written once per
///         invocation regardless of whether an eligible pet was actually found, since "eligible pet
///         found/not found" is itself the only outcome this partially-ported command can currently produce.
///         See <see cref="GmActionEventCodes.PetExperienceGrant" /> for the locally-scoped EventCode this
///         uses.
///     </para>
/// </remarks>
public sealed class GmPetExperienceGrantService(
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<GmPetExperienceGrantService> logger) : IGmPetExperienceGrantService
{
    /// <summary>Outcome byte for the audit row: an eligible equipped pet was found (even though growth is not yet ported).</summary>
    private const byte EligiblePetFoundOutcome = 1;

    /// <summary>Outcome byte for the audit row: no eligible pet was equipped (no-op).</summary>
    private const byte NoEligiblePetOutcome = 0;

    /// <summary>
    ///     "No pet" sentinel echoed in the response's PetId field. Confirmed against legacy:
    ///     Server/ts25zone/S04_MyWork04.cpp:2071 (<c>r-&gt;tPetID = -1;</c>, executed unconditionally right
    ///     after the tResult=0 assignment, before the equipped-pet-slot check) ; Server/ts25zone/S04_MyWork04.cpp:2078
    ///     (<c>r-&gt;tPetID = tITEM_INFO-&gt;iIndex;</c>, only reached once an eligible equipped pet is found) ;
    ///     Server/Header/Protocol/STRUCT.h:1296-1300 (GM_PETEXP.tPetID is a signed int, so -1 is representable
    ///     on the wire and distinct from 0). See <see cref="GmPetExperienceGrantPayload" />'s own remarks for
    ///     the prior unconfirmed-value history this citation supersedes.
    /// </summary>
    private const int NoPetSentinel = -1;

    /// <summary>Unconditional once the privilege gate passes -- see this type's own remarks.</summary>
    private const int AcceptedResult = 0;

    /// <summary>world.Items.Sort for a pet-classified item (STRUCT.h:1696).</summary>
    private const byte PetItemCatalogSort = 22;

    public async ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Admin))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Admin-tier grant-pet-experience command without sufficient privilege -- forcing logout, no reply",
                zoneSession.CharacterId);
            zoneSession.Abort(DisconnectReason.GmCommandLogout);
            return;
        }

        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = NoPetSentinel;
        byte outcome;

        if (equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack) &&
            worldData.ItemsById.TryGetValue(petStack.ItemId, out var petDefinition) &&
            petDefinition.Item.Sort == PetItemCatalogSort)
        {
            petItemId = petStack.ItemId;
            outcome = EligiblePetFoundOutcome;
            logger.LogInformation(
                "Character {CharacterId} grant-pet-experience: eligible pet {PetItemId} found, but the growth-grant subsystem is not ported yet (missing per-item-id tier table citation) -- echoing accepted with PetExperience=0, no growth applied",
                zoneSession.CharacterId, petItemId);
        }
        else
        {
            outcome = NoEligiblePetOutcome;
            logger.LogInformation(
                "Character {CharacterId} grant-pet-experience: no eligible pet equipped -- echoing accepted with the no-pet sentinel",
                zoneSession.CharacterId);
        }

        // Project-decision audit trail (this type's own remarks) -- written once per invocation, immediately
        // after the eligibility check above. No admin.ErrorCatalog/DB migration needed: game.EventLog.EventCode
        // is an app-owned numbering scheme with no FK'd catalog table (game.EventLog.sql's own comment).
        await eventLog.LogAsync(GmActionEventCodes.PetExperienceGrant, EventLogCategory.GmAction,
            zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null,
            petItemId == NoPetSentinel ? null : petItemId, null, outcome,
            "GrowthGrantSubsystemNotPorted;PetExperience=0", cancellationToken);

        Span<byte> responseData = stackalloc byte[data.Length];
        data.CopyTo(responseData);
        var echoPayload = new GmPetExperienceGrantPayload { PetId = petItemId, PetExperience = 0 };
        echoPayload.Write(responseData);

        zoneSession.Send(new GenericActionResponse
        {
            Result = AcceptedResult, Sort = 700, Data = responseData.ToArray(), RuneValue = 0
        });
    }
}
