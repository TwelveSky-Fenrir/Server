using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     See <see cref="ITribeBankService" />. Sort 1 (view) requires any tribe role at all: Fenrir has no
///     GM-rank concept (see <see cref="Chat.GlobalAnnouncementHandler" />), so the legacy's
///     <c>uUserSort &lt; 1</c> GM bypass is always taken and every caller falls through to the
///     <c>ReturnTribeRole != 0</c> check. Sort 2 (withdraw) is Force Leader-only and additionally requires at
///     least 3 appointed sub-masters. Sort 3 (deposit) is a Fenrir-only addition, not a legacy client sort:
///     the legacy deposit path (ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND) was a periodic system-side tax sweep
///     gated on a WorldState/territory-ownership model Fenrir has not ported yet, so it has no client trigger
///     at all -- "the bank can be emptied but never filled" is fixed here instead by letting any tribe member
///     donate their own money (never someone else's, so no privileged-role gate is needed).
/// </summary>
public sealed class TribeBankService(ITribeRepository tribes, ILogger<TribeBankService> logger) : ITribeBankService
{
    private const int SlotCount = 50;
    private const int RequiredSubMasterCount = 3;

    public async ValueTask<TribeBankResult> ViewAsync(PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.TribeRole == 0)
            return TribeBankResult.Aborted;

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        return new TribeBankResult(true, 1, BuildBankArray(slots), 0);
    }

    public async ValueTask<TribeBankResult> WithdrawAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (slotValue < 0 || slotValue >= SlotCount || state.TribeRole != 1)
            return TribeBankResult.Aborted;

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (subMasters.Count < RequiredSubMasterCount)
            return TribeBankResult.Aborted;

        long newMoney;
        try
        {
            newMoney = await tribes.WithdrawBankAsync(state.Tribe, (byte)slotValue, characterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} tribe-bank withdrawal (tribe {Tribe} slot {Slot}) failed", characterId,
                state.Tribe, slotValue);
            return TribeBankResult.Aborted;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        return new TribeBankResult(true, 2, BuildBankArray(slots), (int)newMoney);
    }

    public async ValueTask<TribeBankResult> DepositAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (slotValue < 0 || slotValue >= SlotCount)
            return TribeBankResult.Aborted;

        long newMoney;
        try
        {
            newMoney = await tribes.DepositBankAsync(state.Tribe, (byte)slotValue, characterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} tribe-bank deposit (tribe {Tribe} slot {Slot}) failed", characterId,
                state.Tribe, slotValue);
            return TribeBankResult.Aborted;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        return new TribeBankResult(true, 3, BuildBankArray(slots), (int)newMoney);
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
