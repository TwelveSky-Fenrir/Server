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
///     Business logic behind CZ_TRIBE_BANK_SEND (opcode 82) sort 1 (view), extracted out of
///     <see cref="TribeBankHandler" />.
///     <para>
///         <b>Correction:</b> a fresh, definitive full read of Server/ts25zone/S04_MyWork02.cpp:11560-11607
///         and Server/ts25playuser/S04_MyWork02.cpp:269-377 resolved a 3-way contradiction: sort 2 is
///         exclusively a WITHDRAW (bank slot -&gt; player money), not the deposit this interface previously
///         claimed it was -- legacy has no client-invocable deposit path at all, on this opcode or anywhere
///         else. <see cref="TribeBankHandler" /> now routes sort 2 to <c>TribeBankWithdrawService.WithdrawAsync</c>
///         directly rather than through this interface. <see cref="DepositAsync" /> below is consequently no
///         longer reachable from any opcode; it is kept on the interface for now (not removed) pending a
///         separate decision, since it turns out to have no legacy basis as a client-invoked action at all.
///     </para>
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

    /// <summary>
    ///     Player money -&gt; tribe-bank slot. No longer reachable from any opcode -- see this interface's own
    ///     summary. Kept, not removed, pending a separate decision.
    /// </summary>
    public ValueTask<TribeBankResult> DepositAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct);
}
