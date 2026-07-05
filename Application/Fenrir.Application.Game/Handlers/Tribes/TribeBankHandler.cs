using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Tribes;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_BANK_SEND (opcode 82). Sort 1 (view) requires any tribe role at all: Fenrir has no GM-rank
///     concept (see <see cref="Chat.GlobalAnnouncementHandler" />), so the legacy's <c>uUserSort &lt; 1</c>
///     GM bypass is always taken and every caller falls through to the <c>ReturnTribeRole != 0</c> check.
///     Sort 2 (withdraw) is Force Leader-only and additionally requires at least 3 appointed sub-masters.
///     Sort 3 (deposit) is a Fenrir-only addition, not a legacy client sort: the legacy deposit path
///     (ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND) was a periodic system-side tax sweep gated on a
///     WorldState/territory-ownership model Fenrir has not ported yet, so it has no client trigger at all --
///     "the bank can be emptied but never filled" is fixed here instead by letting any tribe member donate
///     their own money (never someone else's, so no privileged-role gate is needed). Any gate failure
///     disconnects (matches legacy <c>Quit()</c>), never a graceful error reply.
/// </summary>
public sealed class TribeBankHandler(ITribeRepository tribes, ILogger<TribeBankHandler> logger)
    : IAsyncPacketHandler<TribeBankRequest>
{
    private const int SlotCount = 50;
    private const int RequiredSubMasterCount = 3;

    public async ValueTask HandleAsync(TribeBankRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            switch (packet.Sort)
            {
                case 1:
                    await HandleViewAsync(session, zoneSession, state, cancellationToken);
                    return;
                case 2:
                    await HandleWithdrawAsync(packet, session, zoneSession, state, characterId, cancellationToken);
                    return;
                case 3:
                    await HandleDepositAsync(packet, session, zoneSession, state, characterId, cancellationToken);
                    return;
                default:
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask HandleViewAsync(IPacketSession session, ZoneClientSession zoneSession,
        PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.TribeRole == 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        session.Send(new TribeBankResponse { Result = 0, Sort = 1, TribeBankInfo = BuildBankArray(slots), Money = 0 });
    }

    private async ValueTask HandleWithdrawAsync(TribeBankRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (packet.Value < 0 || packet.Value >= SlotCount || state.TribeRole != 1)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (subMasters.Count < RequiredSubMasterCount)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        long newMoney;
        try
        {
            newMoney = await tribes.WithdrawBankAsync(state.Tribe, (byte)packet.Value, characterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} tribe-bank withdrawal (tribe {Tribe} slot {Slot}) failed", characterId,
                state.Tribe, packet.Value);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        session.Send(new TribeBankResponse
            { Result = 0, Sort = 2, TribeBankInfo = BuildBankArray(slots), Money = (int)newMoney });
    }

    private async ValueTask HandleDepositAsync(TribeBankRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (packet.Value < 0 || packet.Value >= SlotCount)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        long newMoney;
        try
        {
            newMoney = await tribes.DepositBankAsync(state.Tribe, (byte)packet.Value, characterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} tribe-bank deposit (tribe {Tribe} slot {Slot}) failed", characterId,
                state.Tribe, packet.Value);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slots = await tribes.GetBankAsync(state.Tribe, ct);
        session.Send(new TribeBankResponse
            { Result = 0, Sort = 3, TribeBankInfo = BuildBankArray(slots), Money = (int)newMoney });
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
