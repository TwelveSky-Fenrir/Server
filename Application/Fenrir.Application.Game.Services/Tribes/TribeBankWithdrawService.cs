using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

public sealed class TribeBankWithdrawService(ITribeRepository tribes, ILogger<TribeBankWithdrawService> logger)
{
    private const int SlotCount = 50;
    private const int RequiredSubMasterCount = 3;

        public async ValueTask<TribeBankResult> WithdrawAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (slotValue < 0 || slotValue >= SlotCount || state.TribeRole != 1)
        {
            logger.LogWarning(
                "Character {CharacterId} tribe-bank withdraw rejected: slot {Slot} out of range or caller is not Force Leader",
                characterId, slotValue);
            return TribeBankResult.Aborted;
        }

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (subMasters.Count < RequiredSubMasterCount)
        {
            logger.LogWarning(
                "Character {CharacterId} tribe-bank withdraw rejected: tribe {Tribe} has only {SubMasterCount}/{RequiredSubMasterCount} sub-masters",
                characterId, state.Tribe, subMasters.Count, RequiredSubMasterCount);
            return TribeBankResult.Aborted;
        }

        long newMoney;
        try
        {
            newMoney = await tribes.WithdrawBankAsync(state.Tribe, (byte)slotValue, characterId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} tribe-bank withdraw (tribe {Tribe} slot {Slot}) failed", characterId,
                state.Tribe, slotValue);
            return TribeBankResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} withdrew tribe {Tribe} bank slot {Slot} (new money {NewMoney})",
            characterId, state.Tribe, slotValue, newMoney);

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        return new TribeBankResult(true, 2, BuildBankArray(slots), (int)newMoney);
    }

    private static int[] BuildBankArray(IReadOnlyCollection<TribeBankSlotDto> slots)
    {
        var array = new int[SlotCount];
        foreach (var slot in slots)
            if (slot.SlotIndex < SlotCount)
                array[slot.SlotIndex] = slot.Amount;
        return array;
    }
}
