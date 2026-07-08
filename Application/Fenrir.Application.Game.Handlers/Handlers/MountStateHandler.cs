using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_ANIMAL_STATE_SEND (op87) -- all eight live sub-actions (Select/Deselect/Mount/Dismount/Delete
///     Mount/Convert Attribute/Delete Rolled Attribute/Transfer Rolled Attribute). Sort 5/7 need real I/O (a
///     CP mirror, an inventory item scan/consumption, a durable audit row), so this handler is asynchronous
///     and serializes on <see cref="PlayerRuntimeState.EconomyActionLock" /> for its whole dispatch, matching
///     <c>TribeActionHandler</c>/<c>CraftPetHandler</c>'s own posture.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11819-11866 (sorts 1-4 shared range-validation/silent-no-op
///     shape), :11839-11861 (Sort 3 Mount -- animal-id assignment, ability/HP-MP recompute, ordered state-12
///     then state-26 broadcasts, absorb-state force-clear, one-minute decay-tick baseline reset, confirmation
///     echo sent last), :11862-11888 (Sort 4 Dismount -- conditional absorb-clear notice, animal-id reset,
///     ability/HP-MP recompute, confirmation echo, then state-13 notice), :11890-11919 (Sort 5 Delete Mount --
///     +250 CP compensation grant, garage-slot clear), :11920-11946 (Sort 6 Convert Attribute -- full bounds
///     chain implemented, its own roll left an explicit gap, see <see cref="MountStateResolver" />'s remarks),
///     :11959-11991 (Sort 7 Delete Rolled Attribute -- item 1225 consumed), :11993-12044 (Sort 8 Transfer
///     Rolled Attribute -- silent-no-op primary index check, its own transfer mechanic left an explicit gap,
///     same remarks). The AOI broadcasts (state 12/26/13) and the absorb-toggle notice are applied by
///     <c>Zone.ApplyMountCommand</c> (World/Zone.CosmeticMirrors.cs) once the posted <c>MountZoneCommand</c>
///     is drained on the next zone tick, not synchronously on this handler's calling thread -- see that
///     method's own remarks for the resulting ordering relative to this handler's own direct confirmation
///     echo below. The one-minute decay-tick baseline reset (Mount side effect 6) and the mount-activity-decay/
///     PvP-kill-exp-grant systems that read it are not implemented anywhere in Fenrir yet -- out of scope for
///     this wire-layer handler, see the mount-runtime-growth finding for the Domain/Simulation-layer gap.
/// </remarks>
public sealed class MountStateHandler(IMountStateService service, ILogger<MountStateHandler> logger)
    : IAsyncPacketHandler<MountStateRequest>
{
    public async ValueTask HandleAsync(MountStateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: MountStateRequest (op87) received for character {CharacterId}, sort {Sort} value {Value}",
            session.SessionId, characterId, packet.Sort, packet.Value);

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window --
        // Sort 5/7 touch inventory/CP, Sort 1-4 don't but are wrapped uniformly for simplicity, matching
        // TribeActionHandler's own posture.
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        MountStateResult result;
        try
        {
            result = await service.ApplyAsync(zone, state, characterId, accountId, packet.Sort, packet.Value,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        switch (result.Outcome)
        {
            case MountStateOutcome.NoReply:
                return;

            case MountStateOutcome.Disconnect:
                logger.LogWarning(
                    "Mount-state rejected for character {CharacterId}: sort {Sort} value {Value} -- aborting session",
                    characterId, packet.Sort, packet.Value);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case MountStateOutcome.Select:
            case MountStateOutcome.Deselect:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Mount:
                logger.LogInformation("Character {CharacterId} mounted animal slot {Value}", characterId,
                    packet.Value);
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Dismount:
                logger.LogInformation("Character {CharacterId} dismounted", characterId);
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = 0 });
                return;

            case MountStateOutcome.DeleteMount:
            case MountStateOutcome.DeleteAttribute:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;
        }
    }
}
