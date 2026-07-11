using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Data.Abstractions.Inventory;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Inventory;

/// <inheritdoc cref="IBigMoneyTransferService" />
public sealed class BigMoneyTransferService(IBigMoneyRepository bigMoney, ILogger<BigMoneyTransferService> logger)
    : IBigMoneyTransferService
{
    public async ValueTask<GenericActionResult> TransferStoreAsync(int sort, byte[] data, int characterId,
        CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Store-transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (move.Quantity1 < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Store-transfer aborted: non-positive amount {Quantity1}",
                characterId, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        var isDeposit = sort == 241;
        var deltaInventoryBigMoney = isDeposit ? -move.Quantity1 : move.Quantity1;
        var deltaStoreBigMoney = isDeposit ? move.Quantity1 : -move.Quantity1;

        try
        {
            await bigMoney.AdjustInventoryStoreAsync(characterId, deltaInventoryBigMoney, deltaStoreBigMoney,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} BigMoney-Store-transfer AdjustInventoryStoreAsync failed (treated as insufficient balance/cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} BigMoney-Store-transfer applied: isDeposit={IsDeposit}, amount {Quantity1}",
            characterId, isDeposit, move.Quantity1);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> TransferSaveAsync(int sort, byte[] data, int accountId,
        int characterId, CancellationToken cancellationToken)
    {
        if (!DefaultPData.TryRead(data, out var move))
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Save-transfer aborted: malformed payload (sort {Sort})",
                characterId, sort);
            return GenericActionResult.Aborted;
        }

        if (move.Quantity1 < 1)
        {
            logger.LogInformation(
                "Character {CharacterId} BigMoney-Save-transfer aborted: non-positive amount {Quantity1}",
                characterId, move.Quantity1);
            return GenericActionResult.Aborted;
        }

        var isDeposit = sort == 242;
        var deltaInventoryBigMoney = isDeposit ? -move.Quantity1 : move.Quantity1;
        var deltaVaultBigMoney = isDeposit ? move.Quantity1 : -move.Quantity1;

        try
        {
            await bigMoney.AdjustInventorySaveAsync(characterId, deltaInventoryBigMoney, accountId,
                deltaVaultBigMoney, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} BigMoney-Save-transfer AdjustInventorySaveAsync failed (treated as insufficient balance/cap breach)",
                characterId);
            return GenericActionResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} BigMoney-Save-transfer applied: isDeposit={IsDeposit}, amount {Quantity1}",
            characterId, isDeposit, move.Quantity1);

        return GenericActionResult.Succeeded;
    }
}
