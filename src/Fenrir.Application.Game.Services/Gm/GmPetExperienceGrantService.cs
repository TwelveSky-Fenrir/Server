using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Fenrir.Domain.Game.Stats;
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

    private const int PetExperienceStatSort = 14;

    private const float GmGrowthPercent = 200.0f;

    public async ValueTask HandleAsync(byte[] data, IZoneSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Admin))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Admin-tier grant-pet-experience command without sufficient privilege -- forcing logout, no reply",
                zoneSession.CharacterId);
            await eventLog.LogAsync(GmActionEventCodes.PetExperienceGrant, EventLogCategory.GmAction,
                zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null,
                GmCommandCatalog.OutcomeDenied, $"Command=PETEXP;Sort=700;RequiredTier={(short)GmCommandTier.Admin}",
                cancellationToken);
            zoneSession.Abort(DisconnectReason.GmCommandLogout);
            return;
        }

        if (!GmPetExperienceGrantPayload.TryRead(data, out var requestPayload))
        {
            zoneSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = NoPetSentinel;
        var reportedPetExperience = requestPayload.PetExperience;
        byte outcome;
        var creditedAmount = 0;
        var tierIncreased = false;
        var growthBefore = state.PetGrowth;

        if (equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack) &&
            worldData.ItemsById.TryGetValue(petStack.ItemId, out var petDefinition) &&
            petDefinition.Item.Sort == PetItemCatalogSort)
        {
            petItemId = petStack.ItemId;
            outcome = EligiblePetFoundOutcome;

            var credited = PetExperienceCreditResolver.Resolve(petItemId, state.PetGrowth, state.PetActivity,
                PetGrowthCaps.Values[7], worldData.ItemsById, GmGrowthPercent);
            reportedPetExperience = credited.IsEligible ? credited.NewGrowth : state.PetGrowth;
            creditedAmount = credited.CreditedAmount;
            tierIncreased = credited.TierIncreased;

            if (credited.CreditedAmount > 0)
            {
                EffectiveStats? updatedStats = credited.TierIncreased
                    ? RecomputePetStats(state, petItemId, credited.NewGrowth)
                    : null;

                if (await zone.PostTribeProgressCommandAndWaitAsync(
                        new TribeProgressZoneCommand(state.CharacterId, PetGrowth: credited.NewGrowth,
                            UpdatedStats: updatedStats), cancellationToken))
                {
                    zoneSession.Send(new AvatarStatUpdateResponse
                        { Sort = PetExperienceStatSort, Value = credited.NewGrowth, Value2 = 0 });

                    if (credited.TierIncreased &&
                        !await zone.PostTribeProgressCommandAndWaitAsync(
                            new TribeProgressZoneCommand(state.CharacterId, PetGrowStepBroadcast: true,
                                FullActionRebroadcast: true), cancellationToken))
                        logger.LogError(
                            "Zone {MapId} tribe-progress inbox full: applied GM PETEXP growth for character {CharacterId}, but dropped the grow-step/full-action broadcasts",
                            zone.MapId, state.CharacterId);
                }
                else
                {
                    logger.LogError(
                        "Zone {MapId} tribe-progress inbox full: dropped GM PETEXP growth mirror for character {CharacterId} (pet {PetItemId})",
                        zone.MapId, state.CharacterId, petItemId);
                }
            }

            logger.LogInformation(
                "Character {CharacterId} grant-pet-experience: pet {PetItemId}, growth {GrowthBefore}->{GrowthAfter}, credited {CreditedAmount}, tier increased {TierIncreased}",
                zoneSession.CharacterId, petItemId, growthBefore, reportedPetExperience,
                creditedAmount, tierIncreased);
        }
        else
        {
            outcome = NoEligiblePetOutcome;
            logger.LogInformation(
                "Character {CharacterId} grant-pet-experience: no eligible pet equipped -- echoing accepted with the no-pet sentinel and preserving the request PetExperience field",
                zoneSession.CharacterId);
        }

        await eventLog.LogAsync(GmActionEventCodes.PetExperienceGrant, EventLogCategory.GmAction,
            zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null,
            petItemId == NoPetSentinel ? null : petItemId, null, outcome,
            $"GrowthPercent={GmGrowthPercent};SeedExperience={PetGrowthCaps.Values[7]};CreditedAmount={creditedAmount};PetExperience={reportedPetExperience};TierIncreased={tierIncreased}",
            cancellationToken);

        Span<byte> responseData = stackalloc byte[data.Length];
        data.CopyTo(responseData);
        var echoPayload = new GmPetExperienceGrantPayload
            { PetId = petItemId, PetExperience = reportedPetExperience };
        echoPayload.Write(responseData);

        zoneSession.Send(new GenericActionResponse
        {
            Result = AcceptedResult, Sort = 700, Data = responseData.ToArray(), RuneValue = 0
        });
    }

    private EffectiveStats RecomputePetStats(PlayerRuntimeState state, int petItemId, int newPetGrowth)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petContribution = PetGrowthCalculator.Compute(petItemId, newPetGrowth, state.PetActivity,
            worldData.ItemsById);
        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, state);
    }
}
