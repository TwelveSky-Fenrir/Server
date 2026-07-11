using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     The tribe-bank WITHDRAW / LOAD app caller (behavior contract C17, Part A sort 2; the caller the C17
///     finding reports as missing). Drains the whole chosen tribe-bank slot into the requesting player's own
///     money via <see cref="ITribeRepository.WithdrawBankAsync" /> (game.usp_TribeBank_Withdraw) once the
///     withdraw permission gate passes: the requester must hold tribe role exactly 1 (Force Leader / master),
///     the tribe must currently have at least <see cref="RequiredSubMasterCount" /> appointed sub-masters, and
///     the slot index must be in range -- failing any one disconnects the session (returns
///     <see cref="TribeBankResult.Aborted" />), matching the legacy's undifferentiated <c>Quit()</c>
///     (Server/ts25zone/S04_MyWork02.cpp:11578-11582).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11578-11600 -- CZ_TRIBE_BANK_SEND (opcode 82) tSort 2 calls
///     <c>U_ZONE_TRIBE_BANK_LOAD_FOR_PLAYUSER_SEND(aTribe, tValue, aMoney)</c> (line 11584), then on result 0
///     sets <c>tMoney = mRecv_Money - aMoney</c> (a POSITIVE delta -- money moves bank-&gt;player), logs
///     GL_618_TRIBE_MONEY, sets <c>aMoney = mRecv_Money</c>, and broadcasts the delta to center opcode 62.
///     Confirmed by direct read of the legacy source this session: tSort 2 is a WITHDRAW, not a deposit.
///     <para>
///         <b>Wiring (resolved):</b> <c>TribeBankHandler</c> now routes opcode 82 tSort 2 to this service's
///         <see cref="WithdrawAsync" />, superseding an earlier revision that routed tSort 2 to
///         <c>TribeBankService.DepositAsync</c> (player-&gt;bank). A fresh full read of
///         Server/ts25zone/S04_MyWork02.cpp:11560-11607 and Server/ts25playuser/S04_MyWork02.cpp:269-377
///         definitively confirms tSort 2 is exclusively this withdraw and that legacy has no client-invocable
///         deposit path anywhere -- deposits only happen via the automatic 10-minute tax-skim sweep.
///         <c>TribeBankService.DepositAsync</c> is therefore no longer reachable from any opcode; it is kept
///         (not deleted) pending a decision on whether it should be removed outright, since it has no legacy
///         basis as a client-invoked action. The center opcode-62 broadcast and the GL_618 game-log delta (A5)
///         are gameplay/handler concerns not owned here; the DB-side game-log row is already written by
///         game.usp_TribeBank_Withdraw itself.
///     </para>
/// </remarks>
public sealed class TribeBankWithdrawService(ITribeRepository tribes, ILogger<TribeBankWithdrawService> logger)
{
    private const int SlotCount = 50;
    private const int RequiredSubMasterCount = 3;

    /// <summary>
    ///     Attempt the withdraw. On success returns a sort-2 <see cref="TribeBankResult" /> carrying the
    ///     player's new money total and the refreshed 50-slot balances; on any gate failure or any procedure
    ///     THROW (empty slot 50210 / money-cap 50261 -- both surface to the client as a disconnect, per the
    ///     contract) returns <see cref="TribeBankResult.Aborted" />.
    ///     <para>
    ///         Deliberate divergence from legacy: the legacy response echoes a 50-slot bank-info array off a
    ///         shared buffer that is often stale (only refreshed by the separate VIEW call) -- a confirmed
    ///         legacy display bug, not a design choice. This method always re-fetches the tribe's bank state
    ///         via <see cref="ITribeRepository.GetBankAsync" /> immediately after the withdraw completes, so
    ///         the array returned here is never stale. Fenrir intentionally does not reproduce the legacy bug.
    ///     </para>
    /// </summary>
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
            // Empty slot (50210) or money-cap breach (50261): the contract disconnects the client on any
            // non-zero result -- so any THROW here collapses to the same Aborted/disconnect outcome.
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
