using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>
///     See <see cref="ITribeBankService" />. Sort 1 (view) requires either any tribe role at all, or the
///     caller's session meeting <see cref="GmCommandTier.Basic" /> -- legacy's <c>uUserSort &lt; 1</c> gate
///     (Server/ts25zone/S04_MyWork02.cpp:11560-11607) bypasses the <c>ReturnTribeRole != 0</c> check entirely
///     for a staff/GM-tier caller, letting such an account view any tribe's bank scoped to whatever tribe id
///     is recorded on their own avatar (Server/ts25zone/H07_MyGame.h:796, tier field declaration ;
///     Server/ts25zone/S04_MyWork02.cpp:806, tier populated from the player-session-tracking process's own
///     per-account tier at login) -- the same <c>uUserSort</c>/<see cref="GmCommandTier" /> concept
///     <see cref="Chat.GlobalAnnouncementService" /> and <see cref="Gm.GmBlockAvatarService" /> already gate
///     on. Sort 2 (deposit) is legacy's one mutating tribe-bank operation,
///     client-invocable by an unmodified legacy client (Server/ts25zone/S04_MyWork02.cpp:11560-11607):
///     Force Leader role only (sub-masters cannot deposit even though they can view), plus a requirement
///     that the tribe already have at least 3 appointed sub-masters, plus the slot-range gate -- violating
///     any one of the three collapses to the same hard-disconnect outcome, matching legacy's undifferentiated
///     <c>Quit()</c>. There is no legacy sub-command on this opcode that moves money from the tribe bank to a
///     player; the previous revision of this service wired Sort 2 to a bank-to-player withdraw and invented a
///     Sort 3 "deposit" with no legacy counterpart -- both the wiring and the doc claims backing it were
///     wrong, corrected per a freshly re-derived behavior contract off the same citation.
/// </summary>
public sealed class TribeBankService(ITribeRepository tribes, ILogger<TribeBankService> logger) : ITribeBankService
{
    private const int SlotCount = 50;
    private const int RequiredSubMasterCount = 3;

    public async ValueTask<TribeBankResult> ViewAsync(ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken ct)
    {
        // uUserSort < 1 GM bypass (Server/ts25zone/S04_MyWork02.cpp:11560-11607): a staff/GM-tier caller
        // skips the tribe-role gate entirely and may view any tribe's bank, scoped to whatever tribe id is
        // recorded on their own avatar (state.Tribe) -- not a tribe of their choosing.
        if (state.TribeRole == 0 && !zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogDebug("Character {CharacterId} tribe-bank view rejected: caller holds no tribe role",
                state.CharacterId);
            return TribeBankResult.Aborted;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        return new TribeBankResult(true, 1, BuildBankArray(slots), 0);
    }

    public async ValueTask<TribeBankResult> DepositAsync(int slotValue, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (slotValue < 0 || slotValue >= SlotCount || state.TribeRole != 1)
        {
            logger.LogWarning(
                "Character {CharacterId} tribe-bank deposit rejected: slot {Slot} out of range or caller is not Force Leader",
                characterId, slotValue);
            return TribeBankResult.Aborted;
        }

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (subMasters.Count < RequiredSubMasterCount)
        {
            logger.LogWarning(
                "Character {CharacterId} tribe-bank deposit rejected: tribe {Tribe} has only {SubMasterCount}/{RequiredSubMasterCount} sub-masters",
                characterId, state.Tribe, subMasters.Count, RequiredSubMasterCount);
            return TribeBankResult.Aborted;
        }

        long newMoney;
        try
        {
            newMoney = await tribes.DepositBankAsync(state.Tribe, (byte)slotValue, characterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} tribe-bank deposit (tribe {Tribe} slot {Slot}) failed", characterId,
                state.Tribe, slotValue);
            return TribeBankResult.Aborted;
        }

        logger.LogInformation(
            "Character {CharacterId} deposited into tribe {Tribe} bank slot {Slot} (new balance {NewMoney})",
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
