using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Inventory;

public sealed class BigMoneyUnitConversionService(
    IBigMoneyRepository bigMoney,
    IEventLogRepository eventLog,
    ILogger<BigMoneyUnitConversionService> logger)
    : IBigMoneyUnitConversionService
{
    public ValueTask<GenericActionResult> ConvertAsync(int sort, byte[] data, Zone zone, PlayerRuntimeState state,
        int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-unit-conversion aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return ValueTask.FromResult(GenericActionResult.Aborted);
        }

        return sort == 246
            ? ConvertMoneyToBigMoneyAsync(move.Quantity1, zone, accountId, characterId, cancellationToken)
            : ConvertBigMoneyToMoneyAsync(move.Quantity1, zone, state, accountId, characterId, cancellationToken);
    }

    private async ValueTask<GenericActionResult> ConvertMoneyToBigMoneyAsync(int requestedQuantity, Zone zone,
        int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (requestedQuantity < BigMoneyUnitConversionPolicy.BigMoneyUnitValue)
        {
            logger.LogInformation(
                "Character {CharacterId} Money-to-BigMoney conversion aborted: requested {RequestedQuantity} below the {UnitValue} floor",
                characterId, requestedQuantity, BigMoneyUnitConversionPolicy.BigMoneyUnitValue);
            return GenericActionResult.Aborted;
        }

        const int deltaBigMoney = 1;
        long deltaMoney = -requestedQuantity;

        try
        {
            await bigMoney.AdjustBigMoneyConversionAsync(characterId, deltaMoney, deltaBigMoney, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} Money-to-BigMoney conversion aborted: AdjustBigMoneyConversionAsync failed (treated as insufficient Money or BigMoney-cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        await MirrorBigMoneyDeltaAsync(zone, characterId, deltaBigMoney, cancellationToken);

        await eventLog.LogBigMoneyConversionAsync(EventLogEmitters.BigMoneyConversionEventCode1, accountId,
            characterId, deltaMoney, deltaBigMoney, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} Money-to-BigMoney conversion applied: requested {RequestedQuantity}, Money {DeltaMoney}, BigMoney +{DeltaBigMoney}",
            characterId, requestedQuantity, deltaMoney, deltaBigMoney);

        return GenericActionResult.Succeeded;
    }

    private async ValueTask<GenericActionResult> ConvertBigMoneyToMoneyAsync(int requestedQuantity, Zone zone,
        PlayerRuntimeState state, int accountId, int characterId, CancellationToken cancellationToken)
    {
        if (requestedQuantity < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-to-Money conversion aborted: non-positive amount {RequestedQuantity}",
                characterId, requestedQuantity);
            return GenericActionResult.Aborted;
        }

        if (requestedQuantity > state.BigMoney)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-to-Money conversion aborted: requested {RequestedQuantity} exceeds current BigMoney {CurrentBigMoney}",
                characterId, requestedQuantity, state.BigMoney);
            return GenericActionResult.Aborted;
        }

        const int deltaBigMoney = -1;
        const long deltaMoney = BigMoneyUnitConversionPolicy.BigMoneyUnitValue;

        try
        {
            await bigMoney.AdjustBigMoneyConversionAsync(characterId, deltaMoney, deltaBigMoney, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} BigMoney-to-Money conversion aborted: AdjustBigMoneyConversionAsync failed (treated as Money-cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        await MirrorBigMoneyDeltaAsync(zone, characterId, deltaBigMoney, cancellationToken);

        await eventLog.LogBigMoneyConversionAsync(EventLogEmitters.BigMoneyConversionEventCode2, accountId,
            characterId, deltaMoney, deltaBigMoney, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} BigMoney-to-Money conversion applied: requested {RequestedQuantity}, BigMoney {DeltaBigMoney}, Money +{DeltaMoney}",
            characterId, requestedQuantity, deltaBigMoney, deltaMoney);

        return GenericActionResult.Succeeded;
    }

    private async ValueTask MirrorBigMoneyDeltaAsync(Zone zone, int characterId, int deltaBigMoney,
        CancellationToken cancellationToken)
    {
        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, BigMoneyDelta: deltaBigMoney), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped BigMoney-unit-conversion mirror for character {CharacterId}",
                zone.MapId, characterId);
    }
}
