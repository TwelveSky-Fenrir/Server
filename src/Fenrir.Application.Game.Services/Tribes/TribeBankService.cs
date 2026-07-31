using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

public sealed class TribeBankService(ITribeRepository tribes, ILogger<TribeBankService> logger) : ITribeBankService
{
    private const int SlotCount = 50;

    public async ValueTask<TribeBankResult> ViewAsync(IZoneSession zoneSession, PlayerRuntimeState state,
        CancellationToken ct)
    {
        if (state.TribeRole == 0 && !zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogDebug("Character {CharacterId} tribe-bank view rejected: caller holds no tribe role",
                state.CharacterId);
            return TribeBankResult.Aborted;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        return new TribeBankResult(true, 1, BuildBankArray(slots), 0);
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
