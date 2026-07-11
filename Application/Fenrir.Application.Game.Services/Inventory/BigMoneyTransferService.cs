using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Inventory;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Inventory;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Inventory;

/// <inheritdoc cref="IBigMoneyTransferService" />
/// <remarks>
///     Writes <c>EventLogCategory.BigMoneyConversion</c> (legacy GL_994/GL_995/GL_996/GL_997) via
///     <c>EventLogEmitters.LogBigMoneyConversionAsync</c> after each successful transfer -- the tSort/GL/
///     EventCode mapping for this whole family (all eight siblings, including the still-undispatched
///     240/243/246/247) was confirmed by a re-read pass; see <c>EventLogEmitters</c>'s own remarks table
///     (Infrastructure/Fenrir.Data.Abstractions/Game/EventLogEmitters.cs) for the full citation set.
///     <see cref="TransferStoreAsync" /> has no account id in scope (only a character id), so it logs with
///     a <see langword="null" /> account id -- <see cref="TransferSaveAsync" /> already receives one and
///     passes it through.
/// </remarks>
public sealed class BigMoneyTransferService(
    IBigMoneyRepository bigMoney,
    IEventLogRepository eventLog,
    ILogger<BigMoneyTransferService> logger)
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

        // GL_994_STORE_MONEY (tSort 241 deposit) / GL_995_STORE_MONEY (tSort 244 withdraw) -- see
        // EventLogEmitters.BigMoneyConversionEventCode5/6's own remarks. No account id is threaded through
        // to this method, so this call logs with accountId: null (see this class's own remarks).
        var storeEventCode = isDeposit
            ? EventLogEmitters.BigMoneyConversionEventCode5
            : EventLogEmitters.BigMoneyConversionEventCode6;
        var (storeFromDelta, storeToDelta) = isDeposit
            ? (deltaInventoryBigMoney, deltaStoreBigMoney)
            : (deltaStoreBigMoney, deltaInventoryBigMoney);
        await eventLog.LogBigMoneyConversionAsync(storeEventCode, accountId: null, characterId, storeFromDelta,
            storeToDelta, cancellationToken);

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

        // GL_996_SAVE_MONEY (tSort 242 deposit) / GL_997_SAVE_MONEY (tSort 245 withdraw) -- see
        // EventLogEmitters.BigMoneyConversionEventCode7/8's own remarks.
        var saveEventCode = isDeposit
            ? EventLogEmitters.BigMoneyConversionEventCode7
            : EventLogEmitters.BigMoneyConversionEventCode8;
        var (saveFromDelta, saveToDelta) = isDeposit
            ? (deltaInventoryBigMoney, deltaVaultBigMoney)
            : (deltaVaultBigMoney, deltaInventoryBigMoney);
        await eventLog.LogBigMoneyConversionAsync(saveEventCode, accountId, characterId, saveFromDelta, saveToDelta,
            cancellationToken);

        return GenericActionResult.Succeeded;
    }
}
