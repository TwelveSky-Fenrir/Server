using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public interface ITribeBankWithdrawService
{
    public ValueTask<TribeBankResult> WithdrawAsync(Zone zone, int slotValue, PlayerRuntimeState state,
        int characterId, CancellationToken ct);
}
