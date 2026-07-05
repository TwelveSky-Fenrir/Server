using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

/// <summary>
///     Outcome of one <see cref="ITribeBankService" /> call: whether to abort, and (if not) the wire payload to echo
///     back.
/// </summary>
public readonly record struct TribeBankResult(bool Success, int Sort, int[]? TribeBankInfo, int Money)
{
    public static readonly TribeBankResult Aborted = new(false, 0, null, 0);
}

/// <summary>Business logic behind CZ_TRIBE_BANK_SEND (opcode 82), extracted out of <see cref="TribeBankHandler" />.</summary>
public interface ITribeBankService
{
    public ValueTask<TribeBankResult> ViewAsync(PlayerRuntimeState state, CancellationToken ct);

    public ValueTask<TribeBankResult> WithdrawAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<TribeBankResult> DepositAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct);
}
