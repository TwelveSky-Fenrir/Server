using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_ANIMAL_STATE_SEND (op87). Only Sort 1-4 (Select/Deselect/Mount/Dismount) are wired -- see
///     <see cref="MountStateResolver" />'s remarks for why Sort 5+ (Delete Mount, attribute training, tier
///     upgrade) is an out-of-scope disconnect rather than the legacy's own varied per-case behavior.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11819-11866 (sorts 1-4 shared range-validation/silent-no-op
///     shape), :11839-11861 (Sort 3 Mount -- animal-id assignment, ability/HP-MP recompute, ordered state-12
///     then state-26 broadcasts, absorb-state force-clear, one-minute decay-tick baseline reset, confirmation
///     echo sent last), :11862-11888 (Sort 4 Dismount -- conditional absorb-clear notice, animal-id reset,
///     ability/HP-MP recompute, confirmation echo, then state-13 notice). The AOI broadcasts (state 12/26/13)
///     and the absorb-toggle notice are applied by <c>Zone.ApplyMountCommand</c>
///     (World/Zone.CosmeticMirrors.cs) once the posted <c>MountZoneCommand</c> is drained on the next zone
///     tick, not synchronously on this handler's calling thread -- see that method's own remarks for the
///     resulting ordering relative to this handler's own direct confirmation echo below. The one-minute
///     decay-tick baseline reset (Mount side effect 6) and the mount-activity-decay/PvP-kill-exp-grant systems
///     that read it are not implemented anywhere in Fenrir yet -- out of scope for this wire-layer handler,
///     see the mount-runtime-growth finding for the Domain/Simulation-layer gap.
/// </remarks>
public sealed class MountStateHandler(IMountStateService service) : IInlinePacketHandler<MountStateRequest>
{
    public void Handle(in MountStateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = service.Apply(zone, state, characterId, packet.Sort, packet.Value);

        switch (result.Outcome)
        {
            case MountStateOutcome.NoReply:
                return;

            case MountStateOutcome.Disconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case MountStateOutcome.Select:
            case MountStateOutcome.Deselect:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Mount:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Dismount:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = 0 });
                return;
        }
    }
}
