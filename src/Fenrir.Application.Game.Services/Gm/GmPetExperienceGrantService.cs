using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Application.Game.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmPetExperienceGrantService(
    WorldDataCache worldData,
    IEventLogRepository eventLog,
    ILogger<GmPetExperienceGrantService> logger) : IGmPetExperienceGrantService
{
    private const byte EligiblePetFoundOutcome = 1;

    private const byte NoEligiblePetOutcome = 0;

    private const int NoPetSentinel = -1;

    private const int AcceptedResult = 0;

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
