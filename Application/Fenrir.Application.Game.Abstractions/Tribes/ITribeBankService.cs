using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Tribes;

/// <summary>
///     Outcome of one <see cref="ITribeBankService" /> call: whether to abort, and (if not) the wire payload to echo
///     back.
/// </summary>
public readonly record struct TribeBankResult(bool Success, int Sort, int[]? TribeBankInfo, int Money)
{
    public static readonly TribeBankResult Aborted = new(false, 0, null, 0);
}

/// <summary>
///     Business logic behind CZ_TRIBE_BANK_SEND (opcode 82), extracted out of <see cref="TribeBankHandler" />.
///     Legacy recognizes exactly two live sub-commands, view (sort 1) and deposit (sort 2) -- there is no
///     legacy sub-command that withdraws bank funds to a player via this opcode (see
///     Server/ts25zone/S04_MyWork02.cpp:11560-11607), so this interface has no withdraw member.
/// </summary>
public interface ITribeBankService
{
    /// <summary>
    ///     View is the one sub-command with a staff/GM bypass of the tribe-role gate -- see the implementing
    ///     type (<c>TribeBankService</c>) for the full citation. <paramref name="zoneSession" /> is needed
    ///     only to evaluate that bypass; the bank returned is still scoped to whatever tribe id is recorded
    ///     on the caller's own <paramref name="state" />, never a tribe named by the caller.
    /// </summary>
    public ValueTask<TribeBankResult> ViewAsync(ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken ct);

    public ValueTask<TribeBankResult> DepositAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct);
}
